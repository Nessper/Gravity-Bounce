using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Read-only presentation of the authoritative ScoreManager value.
/// Score packets only pace the visual catch-up and trigger feedback: they never
/// add their value to the gameplay score or become a source of truth.
/// </summary>
public class GameplayScoreImpactUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private AnimatedIntText animatedScore;
    [SerializeField] private RectTransform reactionRoot;

    [Header("Format")]
    [SerializeField] private bool useThousandSeparator = true;

    [Header("Catch-up")]
    [SerializeField] private float unitsPerSecond = 500f;
    [SerializeField] private float impactUnitsPerSecond = 9000f;
    [SerializeField] private float impactBoostDuration = 0.11f;
    [SerializeField] private float minStepPerFrame = 1f;

    [Header("Punch")]
    [SerializeField] private float ballPunchScale = 1.08f;
    [SerializeField] private float comboPunchScale = 1.16f;
    [SerializeField] private float punchDuration = 0.12f;
    [SerializeField] private float punchOffset = 3f;

    [Header("Sound")]
    [SerializeField] private SfxId ballImpactSfx = SfxId.UiClick;
    [SerializeField] private SfxId comboImpactSfx = SfxId.UiConfirm;
    [SerializeField] private float ballImpactVolume = 0.45f;
    [SerializeField] private float comboImpactVolume = 0.8f;

    [Header("Impact Session")]
    [SerializeField] private RectTransform sessionFloatRoot;
    [SerializeField] private float sessionInactivityDelay = 0.45f;
    [SerializeField] private AnimationCurve sessionScaleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float sessionValueForMaxScale = 2500f;
    [SerializeField] private float sessionMinScale = 1f;
    [SerializeField] private float sessionMaxScale = 1.65f;
    [SerializeField] private Gradient sessionColorGradient =
        CreateDefaultSessionGradient();
    [SerializeField] private float sessionValueForMaxColor = 2500f;
    [SerializeField] private Color negativeSessionColor =
        new Color(1f, 0.25f, 0.3f, 1f);
    [SerializeField] private float sessionRiseDuration = 0.45f;
    [SerializeField] private float sessionFloatDuration = 0.18f;
    [SerializeField] private float sessionFadeDuration = 0.35f;
    [SerializeField] private float sessionRiseDistance = 110f;

    private int displayedScore;
    private int visualTargetScore;
    private int latestAuthoritativeScore;
    private int activeVisualSequences;
    private bool hasPendingAuthoritativeDisplay;
    private int pendingAuthoritativeDisplayFrame;

    private float impactBoostRemaining;
    private Vector3 baseScale = Vector3.one;
    private Vector2 baseAnchoredPosition;
    private Color baseTextColor = Color.white;
    private Coroutine punchRoutine;

    private long impactSessionTotal;
    private float sessionInactivityRemaining;
    private int sessionImpactCount;
    private bool hasImpactSession;

    private readonly HashSet<ScoreSessionTotalUI> floatingSessionTotals =
        new HashSet<ScoreSessionTotalUI>();

    public int LatestAuthoritativeScore => latestAuthoritativeScore;
    public bool HasActiveVisualSequences => activeVisualSequences > 0;
    public bool HasPendingImpactSessionPresentation =>
        hasImpactSession || floatingSessionTotals.Count > 0;

    private void Awake()
    {
        if (scoreText == null)
            scoreText = GetComponentInChildren<TMP_Text>();

        if (animatedScore == null && scoreText != null)
            animatedScore = scoreText.GetComponent<AnimatedIntText>();

        if (reactionRoot == null && scoreText != null)
            reactionRoot = scoreText.rectTransform.parent as RectTransform;

        CacheBaseVisualState();

        int initialScore = scoreManager != null
            ? scoreManager.CurrentScore
            : 0;

        SetInstant(initialScore);
    }

    private void OnEnable()
    {
        BindScoreManager();

        if (scoreManager != null)
            SetInstant(scoreManager.CurrentScore);
    }

    private void OnDisable()
    {
        UnbindScoreManager();
        ForceResync();
        ResetReactionVisual();
        DiscardImpactSessionVisuals();
    }

    private void Update()
    {
        ApplyPendingAuthoritativeDisplay();
        UpdateImpactSession();

        if (impactBoostRemaining > 0f)
            impactBoostRemaining = Mathf.Max(0f, impactBoostRemaining - Time.deltaTime);

        // The odometer owns its glyph motion. GameplayScoreImpactUI only sends
        // it a new numeric target when an impact changes the presentation.
        if (UsesMechanicalOdometer())
            return;

        if (displayedScore == visualTargetScore)
            return;

        float rate = impactBoostRemaining > 0f
            ? impactUnitsPerSecond
            : unitsPerSecond;

        int step = Mathf.CeilToInt(
            Mathf.Max(
                minStepPerFrame,
                rate * Time.deltaTime
            )
        );

        displayedScore = MoveTowards(
            displayedScore,
            visualTargetScore,
            step
        );

        ApplyText();
    }

    public void BeginVisualSequence()
    {
        activeVisualSequences++;
        hasPendingAuthoritativeDisplay = false;

        // ScoreManager may already have published the flush total. Hold the
        // displayed number until packets begin reaching the HUD.
        visualTargetScore = displayedScore;
    }

    public void EndVisualSequence()
    {
        activeVisualSequences = Mathf.Max(0, activeVisualSequences - 1);

        if (activeVisualSequences == 0)
            ResyncDisplay(false);
    }

    public void CancelVisualSequencesAndResync()
    {
        activeVisualSequences = 0;
        ResyncDisplay(true);
        ResetReactionVisual();
        DiscardImpactSessionVisuals();
    }

    public void FinalizeImpactSessionForEndSequence()
    {
        if (activeVisualSequences > 0)
            return;

        FinalizeImpactSession();
    }

    public void PunctuateImpact(
        int packetPoints,
        Color packetColor,
        bool isCombo,
        int remainingReservedArrivals)
    {
        hasPendingAuthoritativeDisplay = false;

        // This sum is decorative only. The authoritative score still comes
        // exclusively from ScoreManager.
        RegisterImpactSessionValue(packetPoints);

        // Pace the presentation with the numeric value carried by the packet
        // that actually reached the HUD. This prevents an earlier sequence
        // from consuming the authoritative delta of a later fast flush.
        visualTargetScore = BuildPacketDrivenTarget(packetPoints);

        if (UsesMechanicalOdometer())
        {
            displayedScore = visualTargetScore;
            ApplyText();
        }

        impactBoostRemaining = impactBoostDuration;

        PlayImpactReaction(isCombo, packetColor);
        PlayImpactSound(isCombo);
    }

    private int BuildPacketDrivenTarget(int packetPoints)
    {
        long currentTarget = visualTargetScore;
        long candidate = currentTarget + packetPoints;
        long authoritative = latestAuthoritativeScore;
        long remaining = authoritative - currentTarget;

        if (remaining > 0L && packetPoints > 0)
            candidate = Math.Min(candidate, authoritative);
        else if (remaining < 0L && packetPoints < 0)
            candidate = Math.Max(candidate, authoritative);

        return (int)Math.Max(
            int.MinValue,
            Math.Min(int.MaxValue, candidate)
        );
    }

    public void SetInstant(int value)
    {
        latestAuthoritativeScore = value;
        displayedScore = value;
        visualTargetScore = value;
        impactBoostRemaining = 0f;
        hasPendingAuthoritativeDisplay = false;

        ApplyText(true);
    }

    public void ForceResync()
    {
        ResyncDisplay(true);
    }

    private void ResyncDisplay(bool instant)
    {
        if (scoreManager != null)
            latestAuthoritativeScore = scoreManager.CurrentScore;

        displayedScore = latestAuthoritativeScore;
        visualTargetScore = latestAuthoritativeScore;
        impactBoostRemaining = 0f;
        hasPendingAuthoritativeDisplay = false;

        ApplyText(instant);
    }

    public int GetRemainingDelta()
    {
        return Mathf.Abs(latestAuthoritativeScore - displayedScore);
    }

    private void HandleAuthoritativeScoreChanged(int value)
    {
        latestAuthoritativeScore = value;

        if (activeVisualSequences == 0)
        {
            // A flush publishes its authoritative value before its visual
            // packets can reserve the HUD. Defer the fallback briefly so
            // BeginVisualSequence can claim presentation during the same
            // resolution flow. Packet contacts then drive the odometer.
            hasPendingAuthoritativeDisplay = true;
            pendingAuthoritativeDisplayFrame = Time.frameCount + 2;
        }
    }

    private void ApplyPendingAuthoritativeDisplay()
    {
        if (!hasPendingAuthoritativeDisplay ||
            activeVisualSequences > 0 ||
            Time.frameCount < pendingAuthoritativeDisplayFrame)
        {
            return;
        }

        hasPendingAuthoritativeDisplay = false;
        visualTargetScore = latestAuthoritativeScore;

        if (UsesMechanicalOdometer())
        {
            displayedScore = visualTargetScore;
            ApplyText();
        }
    }

    private void BindScoreManager()
    {
        if (scoreManager == null)
            return;

        scoreManager.onScoreChanged.RemoveListener(HandleAuthoritativeScoreChanged);
        scoreManager.onScoreChanged.AddListener(HandleAuthoritativeScoreChanged);
    }

    private void UnbindScoreManager()
    {
        if (scoreManager == null)
            return;

        scoreManager.onScoreChanged.RemoveListener(HandleAuthoritativeScoreChanged);
    }

    private void PlayImpactReaction(bool isCombo, Color packetColor)
    {
        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        float sessionReinforcement = Mathf.Min(
            0.06f,
            Mathf.Max(0, sessionImpactCount - 1) * 0.01f
        );

        ResetReactionVisual();
        punchRoutine = StartCoroutine(
            PunchRoutine(
                (isCombo ? comboPunchScale : ballPunchScale) +
                    sessionReinforcement,
                packetColor
            )
        );
    }

    private void UpdateImpactSession()
    {
        if (!hasImpactSession)
            return;

        sessionInactivityRemaining -= Time.deltaTime;

        if (sessionInactivityRemaining <= 0f)
            FinalizeImpactSession();
    }

    private void RegisterImpactSessionValue(int packetPoints)
    {
        if (!hasImpactSession)
        {
            hasImpactSession = true;
            impactSessionTotal = 0L;
            sessionImpactCount = 0;
        }

        impactSessionTotal += packetPoints;
        sessionImpactCount++;
        sessionInactivityRemaining = Mathf.Max(0f, sessionInactivityDelay);
    }

    private void FinalizeImpactSession()
    {
        if (!hasImpactSession)
            return;

        long completedTotal = impactSessionTotal;
        ResetImpactSessionState();

        if (completedTotal == 0L || !isActiveAndEnabled)
            return;

        SpawnFloatingSessionTotal(completedTotal);
    }

    private void SpawnFloatingSessionTotal(long total)
    {
        if (scoreText == null ||
            sessionFloatRoot == null ||
            !sessionFloatRoot.gameObject.activeInHierarchy ||
            !TryGetSessionStartPosition(out Vector2 startPosition))
        {
            return;
        }

        TMP_Text floatingText = Instantiate(
            scoreText,
            sessionFloatRoot,
            false
        );

        floatingText.name = "ScoreSessionTotal";
        floatingText.enabled = true;
        floatingText.raycastTarget = false;
        floatingText.enableVertexGradient = false;
        floatingText.overflowMode = TextOverflowModes.Overflow;
        floatingText.text = FormatSessionTotal(total);

        float normalizedScale = GetNormalizedSessionTotal(
            total,
            sessionValueForMaxScale
        );

        float scaleProgress = sessionScaleCurve != null
            ? Mathf.Clamp01(sessionScaleCurve.Evaluate(normalizedScale))
            : normalizedScale;

        float normalizedColor = GetNormalizedSessionTotal(
            total,
            sessionValueForMaxColor
        );

        floatingText.color = total < 0L
            ? negativeSessionColor
            : sessionColorGradient != null
                ? sessionColorGradient.Evaluate(normalizedColor)
                : baseTextColor;

        RectTransform floatingRect = floatingText.rectTransform;
        Vector2 sourceSize = scoreText.rectTransform.rect.size;
        Vector2 transferAnchor = sessionFloatRoot.pivot;

        floatingRect.anchorMin = transferAnchor;
        floatingRect.anchorMax = transferAnchor;
        floatingRect.pivot = new Vector2(0.5f, 0.5f);
        floatingRect.sizeDelta = new Vector2(
            Mathf.Max(220f, sourceSize.x),
            Mathf.Max(60f, sourceSize.y)
        );
        floatingRect.anchoredPosition = startPosition;
        floatingRect.localRotation = Quaternion.identity;

        float visualScale = Mathf.Max(
            0.01f,
            Mathf.Lerp(sessionMinScale, sessionMaxScale, scaleProgress)
        );

        floatingRect.localScale = Vector3.one * visualScale;

        CanvasGroup group = floatingText.GetComponent<CanvasGroup>();

        if (group == null)
            group = floatingText.gameObject.AddComponent<CanvasGroup>();

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        ScoreSessionTotalUI view =
            floatingText.gameObject.AddComponent<ScoreSessionTotalUI>();

        floatingSessionTotals.Add(view);

        view.Play(
            floatingText,
            group,
            sessionRiseDuration,
            sessionRiseDistance,
            sessionFloatDuration,
            sessionFadeDuration,
            HandleFloatingSessionDestroyed
        );
    }

    private bool TryGetSessionStartPosition(out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (scoreText == null || sessionFloatRoot == null)
            return false;

        RectTransform sourceRect = scoreText.rectTransform;
        Vector3 sourceCenter = sourceRect.TransformPoint(sourceRect.rect.center);
        Canvas sourceCanvas = scoreText.canvas;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            GetCanvasEventCamera(sourceCanvas),
            sourceCenter
        );

        Canvas targetCanvas = sessionFloatRoot.GetComponentInParent<Canvas>();

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sessionFloatRoot,
            screenPoint,
            GetCanvasEventCamera(targetCanvas),
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

    private float GetNormalizedSessionTotal(long total, float maximumValue)
    {
        double magnitude = Math.Abs((double)total);
        double maximum = Math.Max(1d, maximumValue);
        return (float)Math.Min(1d, magnitude / maximum);
    }

    private string FormatSessionTotal(long total)
    {
        string value = useThousandSeparator
            ? total.ToString("N0")
            : total.ToString();

        return total > 0L ? "+" + value : value;
    }

    private void DiscardImpactSessionVisuals()
    {
        ResetImpactSessionState();

        if (floatingSessionTotals.Count == 0)
            return;

        ScoreSessionTotalUI[] snapshot =
            new ScoreSessionTotalUI[floatingSessionTotals.Count];

        floatingSessionTotals.CopyTo(snapshot);
        floatingSessionTotals.Clear();

        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] != null)
            {
                snapshot[i].gameObject.SetActive(false);
                Destroy(snapshot[i].gameObject);
            }
        }
    }

    private void ResetImpactSessionState()
    {
        hasImpactSession = false;
        impactSessionTotal = 0L;
        sessionImpactCount = 0;
        sessionInactivityRemaining = 0f;
    }

    private void HandleFloatingSessionDestroyed(ScoreSessionTotalUI view)
    {
        if (view != null)
            floatingSessionTotals.Remove(view);
    }

    private IEnumerator PunchRoutine(float targetScale, Color packetColor)
    {
        if (reactionRoot == null)
            yield break;

        Vector2 direction = UnityEngine.Random.insideUnitCircle;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        reactionRoot.localScale = baseScale * targetScale;
        reactionRoot.anchoredPosition =
            baseAnchoredPosition + direction * punchOffset;

        if (scoreText != null)
        {
            packetColor.a = baseTextColor.a;
            SetScoreVisualColor(
                Color.Lerp(baseTextColor, packetColor, 0.45f)
            );
        }

        float elapsed = 0f;
        Vector3 startScale = reactionRoot.localScale;
        Vector2 startPosition = reactionRoot.anchoredPosition;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;

            float t = punchDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / punchDuration);

            float eased = t * t * (3f - 2f * t);

            reactionRoot.localScale =
                Vector3.LerpUnclamped(startScale, baseScale, eased);

            reactionRoot.anchoredPosition =
                Vector2.LerpUnclamped(startPosition, baseAnchoredPosition, eased);

            if (scoreText != null)
                SetScoreVisualColor(
                    Color.Lerp(scoreText.color, baseTextColor, eased)
                );

            yield return null;
        }

        reactionRoot.localScale = baseScale;
        reactionRoot.anchoredPosition = baseAnchoredPosition;

        if (scoreText != null)
            SetScoreVisualColor(baseTextColor);

        punchRoutine = null;
    }

    private void PlayImpactSound(bool isCombo)
    {
        AudioManager audio = BootRoot.Audio;

        if (audio == null)
            return;

        SfxId sfx = isCombo ? comboImpactSfx : ballImpactSfx;

        if (sfx == SfxId.None)
            return;

        float volume = isCombo
            ? comboImpactVolume
            : ballImpactVolume;

        audio.PlayUi(sfx, 1f, volume);
    }

    private void CacheBaseVisualState()
    {
        if (reactionRoot != null)
        {
            baseScale = reactionRoot.localScale;
            baseAnchoredPosition = reactionRoot.anchoredPosition;
        }

        if (scoreText != null)
            baseTextColor = scoreText.color;
    }

    private void ResetReactionVisual()
    {
        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }

        if (reactionRoot != null)
        {
            reactionRoot.localScale = baseScale;
            reactionRoot.anchoredPosition = baseAnchoredPosition;
        }

        if (scoreText != null)
            SetScoreVisualColor(baseTextColor);
    }

    private int MoveTowards(int current, int target, int maxDelta)
    {
        if (current < target)
            return Mathf.Min(current + maxDelta, target);

        return Mathf.Max(current - maxDelta, target);
    }

    private bool UsesMechanicalOdometer()
    {
        return animatedScore != null && animatedScore.UsesMechanicalOdometer;
    }

    private void SetScoreVisualColor(Color color)
    {
        if (scoreText != null)
            scoreText.color = color;

        if (animatedScore != null)
            animatedScore.SetVisualColor(color);
    }

    private void ApplyText(bool instant = false)
    {
        if (animatedScore != null)
        {
            if (instant || !animatedScore.UsesMechanicalOdometer)
                animatedScore.SetInstant(displayedScore);
            else
                animatedScore.AnimateTo(displayedScore);

            return;
        }

        if (scoreText == null)
            return;

        scoreText.text = useThousandSeparator
            ? displayedScore.ToString("N0")
            : displayedScore.ToString();
    }

    private static Gradient CreateDefaultSessionGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(
                    new Color(0.45f, 0.9f, 1f, 1f),
                    0f
                ),
                new GradientColorKey(
                    new Color(0.1f, 1f, 0.85f, 1f),
                    0.55f
                ),
                new GradientColorKey(
                    new Color(1f, 0.55f, 0.1f, 1f),
                    1f
                )
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return gradient;
    }
}
