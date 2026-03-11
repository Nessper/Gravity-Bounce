using UnityEngine;

public class PlayerInputTouch : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private PlayerController player;
    [SerializeField] private RectTransform thumbTouchArea;

    private bool hasActivePointer;
    public bool HasActivePointer => hasActivePointer;

    [Header("Options")]
    [SerializeField] private bool inputEnabled = true;

    [Header("Zone de drag effective")]
    [Range(0.2f, 1.0f)]
    [SerializeField] private float effectiveDragWidth01 = 0.60f;

    [Header("Sensibilité base (précision)")]
    [SerializeField] private bool useDpiScaling = true;
    [SerializeField] private float unitsPerCm = 0.55f;
    [SerializeField] private float unitsPerPixelFallback = 0.015f;

    [Header("Deadzone (anti micro-jitter)")]
    [Tooltip("Deadzone très faible. 0 peut créer des micro-saccades sur certains devices.")]
    [SerializeField] private float deadzonePixels = 0.4f;

    [Header("Dash (vitesse doigt)")]
    [SerializeField] private float dashBoostMax = 1.8f;
    [SerializeField] private float dashStartPixelsPerSec = 400f;
    [SerializeField] private float dashFullPixelsPerSec = 1600f;

    [Header("Punch micro (amplitude)")]
    [SerializeField] private float microBoostMax = 0.25f;
    [SerializeField] private float microBoostPixels = 28f;

    [Header("Filtrage adaptatif")]
    [SerializeField] private float smoothTimeMicro = 0.0055f;
    [SerializeField] private float smoothTimeDash = 0.03f;
    [SerializeField] private float microSpeedPixelsPerSec = 220f;
    [SerializeField] private float dashSpeedPixelsPerSec = 850f;
    [SerializeField] private float maxSpeedUnitsPerSec = 32f;

    [Header("Stabilisation vitesse doigt (évite pics de dash)")]
    [Tooltip("0 = brut. 0.15–0.35 recommandé.")]
    [SerializeField] private float speedSmoothing = 0.22f;

    [Tooltip("Cap de vitesse px/s pour éviter des pics anormaux.")]
    [SerializeField] private float speedCapPixelsPerSec = 3200f;

    [Header("Bords")]
    [SerializeField] private bool reanchorWhenClamped = true;

    // Touch state
    private int activeFingerId = -1;
    private bool dragging;

    private float startTouchX;
    private float startPaddleX;

    // Pointer velocity
    private float lastPointerX;
    private float pointerSpeedPxPerSecRaw;
    private float pointerSpeedPxPerSec; // filtrée

    // Filtering target
    private float smoothedTargetX;
    private float smoothVelocityX;

    private void Update()
    {
        if (!inputEnabled || player == null)
        {
            ResetState(true);
            return;
        }

#if UNITY_EDITOR
        HandleMouseSimulatedTouch();
#else
        if (!Application.isMobilePlatform)
        {
            ResetState(true);
            return;
        }

        HandleRealTouches();
#endif
    }

    private void ResetState(bool hard)
    {
        hasActivePointer = false;
        activeFingerId = -1;
        dragging = false;

        pointerSpeedPxPerSecRaw = 0f;
        pointerSpeedPxPerSec = 0f;

        if (hard)
            smoothVelocityX = 0f;
    }

#if UNITY_EDITOR
    private void HandleMouseSimulatedTouch()
    {
        if (!Input.GetMouseButton(0))
        {
            ResetState(false);
            return;
        }

        Vector2 pos = Input.mousePosition;

        if (thumbTouchArea != null &&
            !RectTransformUtility.RectangleContainsScreenPoint(thumbTouchArea, pos))
        {
            ResetState(false);
            return;
        }

        if (!dragging)
            BeginDrag(pos.x);

        hasActivePointer = true;
        UpdatePaddleFromRelativeDelta(pos.x);
    }
#endif

    private void HandleRealTouches()
    {
        if (Input.touchCount == 0)
        {
            ResetState(true);
            return;
        }

        if (activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began)
                    continue;

                if (IsInThumbZone(t.position))
                {
                    activeFingerId = t.fingerId;
                    BeginDrag(t.position.x);
                    UpdatePaddleFromRelativeDelta(t.position.x);
                    return;
                }
            }

            ResetState(false);
            return;
        }

        bool found = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId != activeFingerId)
                continue;

            found = true;

            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                ResetState(true);
            }
            else
            {
                hasActivePointer = true;
                dragging = true;
                UpdatePaddleFromRelativeDelta(t.position.x);
            }

            break;
        }

        if (!found)
            ResetState(true);
    }

    private void BeginDrag(float pointerX)
    {
        dragging = true;
        hasActivePointer = true;

        startTouchX = pointerX;
        startPaddleX = player.transform.position.x;

        lastPointerX = pointerX;
        pointerSpeedPxPerSecRaw = 0f;
        pointerSpeedPxPerSec = 0f;

        smoothedTargetX = startPaddleX;
        smoothVelocityX = 0f;
    }

    private bool IsInThumbZone(Vector2 screenPos)
    {
        if (thumbTouchArea == null)
            return true;

        return RectTransformUtility.RectangleContainsScreenPoint(thumbTouchArea, screenPos, null);
    }

    private void UpdatePaddleFromRelativeDelta(float currentPointerX)
    {
        if (!dragging)
            return;

        // dt plancher pour éviter des pics quand une frame est trop courte
        float dt = Mathf.Max(1f / 120f, Time.deltaTime);

        pointerSpeedPxPerSecRaw = Mathf.Abs(currentPointerX - lastPointerX) / dt;
        lastPointerX = currentPointerX;

        // Cap + filtre vitesse (anti glitch dash)
        float capped = Mathf.Min(pointerSpeedPxPerSecRaw, speedCapPixelsPerSec);
        pointerSpeedPxPerSec = Mathf.Lerp(pointerSpeedPxPerSec, capped, 1f - Mathf.Exp(-speedSmoothing * 60f * dt));

        float deltaPixels = currentPointerX - startTouchX;

        if (Mathf.Abs(deltaPixels) <= deadzonePixels)
        {
            ApplyFilteredTarget(startPaddleX);
            return;
        }

        float unitsPerPixel = ComputeUnitsPerPixel();

        // Compensation zone 3/5 écran
        unitsPerPixel *= 1f / Mathf.Clamp(effectiveDragWidth01, 0.2f, 1f);

        // Micro punch (amplitude) - stable
        float absDelta = Mathf.Abs(deltaPixels);
        float microT = Mathf.Clamp01(absDelta / Mathf.Max(1f, microBoostPixels));
        unitsPerPixel *= 1f + (microBoostMax * microT);

        // Dash (vitesse filtrée)
        unitsPerPixel *= ComputeDashMultiplier(pointerSpeedPxPerSec);

        float targetX = startPaddleX + (deltaPixels * unitsPerPixel);
        float clampedX = Mathf.Clamp(targetX, -player.XRange, player.XRange);

        if (reanchorWhenClamped && !Mathf.Approximately(targetX, clampedX))
        {
            // Ré-ancrage + reset du filtre => supprime les "snaps" / overshoot
            startPaddleX = clampedX;
            startTouchX = currentPointerX;

            smoothedTargetX = clampedX;
            smoothVelocityX = 0f;
        }

        ApplyFilteredTarget(clampedX);
    }

    private float ComputeDashMultiplier(float speedPxPerSec)
    {
        if (dashBoostMax <= 0f)
            return 1f;

        float t = Mathf.InverseLerp(dashStartPixelsPerSec, dashFullPixelsPerSec, speedPxPerSec);
        t = Mathf.Clamp01(t);

        // Pow agressif, mais vitesse filtrée => plus de pics absurdes
        t = 1f - Mathf.Pow(1f - t, 5f);

        return 1f + (dashBoostMax * t);
    }

    private void ApplyFilteredTarget(float targetX)
    {
        float maxSpeed = (maxSpeedUnitsPerSec <= 0f) ? Mathf.Infinity : maxSpeedUnitsPerSec;

        float t = Mathf.InverseLerp(microSpeedPixelsPerSec, dashSpeedPixelsPerSec, pointerSpeedPxPerSec);
        float smoothTime = Mathf.Lerp(smoothTimeMicro, smoothTimeDash, Mathf.Clamp01(t));
        if (pointerSpeedPxPerSec < 120f) smoothTime = 0f;

        if (smoothTime <= 0f)
        {
            smoothedTargetX = targetX;
            player.SetTargetXWorld(targetX);
            return;
        }

        smoothedTargetX = Mathf.SmoothDamp(
            smoothedTargetX,
            targetX,
            ref smoothVelocityX,
            smoothTime,
            maxSpeed,
            Time.deltaTime
        );

        player.SetTargetXWorld(smoothedTargetX);
    }

    private float ComputeUnitsPerPixel()
    {
        if (!useDpiScaling)
            return unitsPerPixelFallback;

        float dpi = Screen.dpi;
        if (dpi < 10f || dpi > 1000f)
            return unitsPerPixelFallback;

        float pixelsPerCm = dpi / 2.54f;
        return unitsPerCm / pixelsPerCm;
    }

    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;
        if (!state)
            ResetState(true);
    }
}
