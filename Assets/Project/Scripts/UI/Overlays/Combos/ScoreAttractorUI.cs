using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves numeric score packets from their source canvas into the Screen Space
/// Overlay transfer root. It owns conversion and trajectory only.
/// </summary>
public class ScoreAttractorUI : MonoBehaviour
{
    private sealed class ActiveFlight
    {
        public RectTransform Rect;
        public BallScoreUI BallScore;
        public ComboScoreUI ComboScore;
        public int Points;
        public Color Color;
        public bool IsCombo;
        public float ArrivalTime;
        public float CurveSign;
        public Coroutine Routine;
        public Action<GameObject> OnFinished;
        public bool Completed;

        public GameObject Packet => Rect != null ? Rect.gameObject : null;
    }

    [Header("Overlay")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private RectTransform absorbTarget;

    [Header("Result")]
    [SerializeField] private ScoreFlushAbsorberUI flushAbsorber;

    [Header("Motion")]
    [SerializeField] private float attractDuration = 0.32f;
    [SerializeField] private float curvature = 70f;
    [SerializeField] private float ballTransferScaleMultiplier = 1f;
    [SerializeField] private float comboTransferScaleMultiplier = 1f;

    [Header("Fade")]
    [SerializeField] private float fadeOnArrivalDuration = 0.08f;

    private readonly List<ActiveFlight> activeFlights =
        new List<ActiveFlight>();

    private void OnDisable()
    {
        CancelAll();
    }

    public IEnumerator AbsorbScores(
        List<BallScoreUI> ballScores,
        List<ComboScoreUI> comboScores,
        Action<GameObject> onPacketFinished)
    {
        List<ActiveFlight> batch = new List<ActiveFlight>();

        if (!CanAbsorb())
        {
            CleanupUnabsorbable(ballScores, comboScores, onPacketFinished);
            yield break;
        }

        if (ballScores != null)
        {
            for (int i = 0; i < ballScores.Count; i++)
            {
                BallScoreUI score = ballScores[i];

                if (score == null)
                    continue;

                batch.Add(CreateBallFlight(score, onPacketFinished));
            }
        }

        if (comboScores != null)
        {
            for (int i = 0; i < comboScores.Count; i++)
            {
                ComboScoreUI score = comboScores[i];

                if (score == null)
                    continue;

                batch.Add(CreateComboFlight(score, onPacketFinished));
            }
        }

        for (int i = 0; i < batch.Count; i++)
        {
            ActiveFlight flight = batch[i];
            activeFlights.Add(flight);
            flight.Routine = StartCoroutine(AbsorbRoutine(flight));
        }

        bool waiting = true;

        while (waiting)
        {
            waiting = false;

            for (int i = 0; i < batch.Count; i++)
            {
                if (!batch[i].Completed)
                {
                    waiting = true;
                    break;
                }
            }

            if (waiting)
                yield return null;
        }
    }

    public void CancelAll()
    {
        if (activeFlights.Count == 0)
            return;

        ActiveFlight[] snapshot = activeFlights.ToArray();

        for (int i = 0; i < snapshot.Length; i++)
        {
            ActiveFlight flight = snapshot[i];

            if (flight == null || flight.Completed)
                continue;

            if (flight.Routine != null)
                StopCoroutine(flight.Routine);

            CompleteFlight(flight, arrived: false);
        }
    }

    private ActiveFlight CreateBallFlight(
        BallScoreUI score,
        Action<GameObject> onPacketFinished)
    {
        float earliestArrival = Time.time +
            Mathf.Max(0f, attractDuration);

        return new ActiveFlight
        {
            Rect = score.transform as RectTransform,
            BallScore = score,
            Points = score.Points,
            Color = score.CurrentColor,
            IsCombo = false,
            ArrivalTime = flushAbsorber.ReserveArrival(earliestArrival),
            CurveSign = UnityEngine.Random.value < 0.5f ? -1f : 1f,
            OnFinished = onPacketFinished
        };
    }

    private ActiveFlight CreateComboFlight(
        ComboScoreUI score,
        Action<GameObject> onPacketFinished)
    {
        score.FadeLabelOnly();

        float earliestArrival = Time.time +
            Mathf.Max(0f, score.LabelFadeDuration) +
            Mathf.Max(0f, attractDuration);

        return new ActiveFlight
        {
            Rect = score.transform as RectTransform,
            ComboScore = score,
            Points = score.Points,
            Color = score.CurrentScoreColor,
            IsCombo = true,
            ArrivalTime = flushAbsorber.ReserveArrival(earliestArrival),
            CurveSign = UnityEngine.Random.value < 0.5f ? -1f : 1f,
            OnFinished = onPacketFinished
        };
    }

    private IEnumerator AbsorbRoutine(ActiveFlight flight)
    {
        float duration = Mathf.Max(0.0001f, attractDuration);
        float launchTime = flight.ArrivalTime - duration;

        while (Time.time < launchTime)
        {
            if (!IsFlightValid(flight))
            {
                CompleteFlight(flight, arrived: false);
                yield break;
            }

            yield return null;
        }

        if (!IsFlightValid(flight) ||
            !TryConvertToOverlayPosition(flight.Rect, out Vector2 startPosition))
        {
            CompleteFlight(flight, arrived: false);
            yield break;
        }

        flight.BallScore?.StopMotion();

        if (flight.ComboScore != null)
        {
            flight.ComboScore.StopMotion();
            flight.ComboScore.HideLabelForAttraction();
        }

        RectTransform scoreRect = flight.Rect;
        CanvasGroup group = scoreRect.GetComponent<CanvasGroup>();

        scoreRect.SetParent(overlayRoot, false);
        Vector2 transferAnchor = overlayRoot.pivot;
        scoreRect.anchorMin = transferAnchor;
        scoreRect.anchorMax = transferAnchor;
        scoreRect.pivot = new Vector2(0.5f, 0.5f);
        scoreRect.anchoredPosition = startPosition;
        scoreRect.localRotation = Quaternion.identity;

        float transferScale = flight.IsCombo
            ? comboTransferScaleMultiplier
            : ballTransferScaleMultiplier;

        scoreRect.localScale =
            Vector3.one * Mathf.Max(0.01f, transferScale);

        Vector3 startScale = scoreRect.localScale;
        Vector3 endScale = startScale * 0.65f;

        if (!TryGetTargetOverlayPosition(out Vector2 initialTarget))
        {
            CompleteFlight(flight, arrived: false);
            yield break;
        }

        Vector2 path = initialTarget - startPosition;
        Vector2 normal = new Vector2(-path.y, path.x);

        if (normal.sqrMagnitude > 0.001f)
            normal.Normalize();

        Vector2 curveOffset =
            normal * curvature * flight.CurveSign;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsFlightValid(flight) ||
                !TryGetTargetOverlayPosition(out Vector2 targetPosition))
            {
                CompleteFlight(flight, arrived: false);
                yield break;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInCubic(t);
            Vector2 control =
                (startPosition + targetPosition) * 0.5f + curveOffset;

            scoreRect.anchoredPosition = QuadraticBezier(
                startPosition,
                control,
                targetPosition,
                eased
            );

            scoreRect.localScale =
                Vector3.LerpUnclamped(startScale, endScale, eased);

            yield return null;
        }

        if (!TryGetTargetOverlayPosition(out Vector2 finalTarget))
        {
            CompleteFlight(flight, arrived: false);
            yield break;
        }

        scoreRect.anchoredPosition = finalTarget;

        flushAbsorber.CommitArrival(
            flight.Points,
            flight.Color,
            flight.IsCombo
        );

        yield return FadeOnArrival(scoreRect, group);
        CompleteFlight(flight, arrived: true);
    }

    private IEnumerator FadeOnArrival(
        RectTransform scoreRect,
        CanvasGroup group)
    {
        if (scoreRect == null || group == null)
            yield break;

        float duration = Mathf.Max(0f, fadeOnArrivalDuration);
        float elapsed = 0f;
        float startAlpha = group.alpha;

        while (elapsed < duration && scoreRect != null)
        {
            elapsed += Time.deltaTime;

            float t = duration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);

            group.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }
    }

    private void CompleteFlight(ActiveFlight flight, bool arrived)
    {
        if (flight == null || flight.Completed)
            return;

        flight.Completed = true;
        activeFlights.Remove(flight);

        if (!arrived)
            flushAbsorber?.CancelArrival();

        GameObject packet = flight.Packet;
        flight.OnFinished?.Invoke(packet);

        if (packet != null)
            Destroy(packet);
    }

    private bool CanAbsorb()
    {
        return isActiveAndEnabled &&
            overlayRoot != null &&
            absorbTarget != null &&
            flushAbsorber != null &&
            overlayRoot.gameObject.activeInHierarchy &&
            absorbTarget.gameObject.activeInHierarchy;
    }

    private bool IsFlightValid(ActiveFlight flight)
    {
        return flight != null &&
            !flight.Completed &&
            flight.Rect != null &&
            CanAbsorb();
    }

    private void CleanupUnabsorbable(
        List<BallScoreUI> ballScores,
        List<ComboScoreUI> comboScores,
        Action<GameObject> onPacketFinished)
    {
        if (ballScores != null)
        {
            for (int i = 0; i < ballScores.Count; i++)
                CleanupPacket(ballScores[i], onPacketFinished);
        }

        if (comboScores != null)
        {
            for (int i = 0; i < comboScores.Count; i++)
                CleanupPacket(comboScores[i], onPacketFinished);
        }
    }

    private void CleanupPacket(
        MonoBehaviour packetComponent,
        Action<GameObject> onPacketFinished)
    {
        if (packetComponent == null)
            return;

        GameObject packet = packetComponent.gameObject;
        onPacketFinished?.Invoke(packet);
        Destroy(packet);
    }

    private bool TryConvertToOverlayPosition(
        RectTransform source,
        out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (source == null || overlayRoot == null)
            return false;

        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        Camera sourceCamera = GetCanvasEventCamera(sourceCanvas);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            sourceCamera,
            source.position
        );

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayRoot,
            screenPoint,
            GetCanvasEventCamera(overlayRoot.GetComponentInParent<Canvas>()),
            out localPoint
        );
    }

    private bool TryGetTargetOverlayPosition(out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (absorbTarget == null || overlayRoot == null)
            return false;

        Canvas targetCanvas = absorbTarget.GetComponentInParent<Canvas>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            GetCanvasEventCamera(targetCanvas),
            absorbTarget.position
        );

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayRoot,
            screenPoint,
            GetCanvasEventCamera(overlayRoot.GetComponentInParent<Canvas>()),
            out localPoint
        );
    }

    private Camera GetCanvasEventCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private Vector2 QuadraticBezier(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float t)
    {
        float inverse = 1f - t;

        return inverse * inverse * start +
            2f * inverse * t * control +
            t * t * end;
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }
}
