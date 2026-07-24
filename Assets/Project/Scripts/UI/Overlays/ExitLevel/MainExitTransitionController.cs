using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gere la transition de sortie de la scene Main apres un resultat de niveau.
///
/// Responsabilites :
/// - jouer un dialogue d outro optionnel lie au levelId
/// - permettre le skip du dialogue via hold-to-skip
/// - jouer un flash optionnel
/// - animer le depart du vaisseau
/// - appeler un callback final quand la transition est terminee
///
/// IMPORTANT :
/// - ne gere pas les rewards
/// - ne gere pas la navigation
/// - ne connait pas EndResultOverlayController
/// - ne connait pas les anciens scripts EndLevelUI / FinalPanelUI
/// </summary>
public class MainExitTransitionController : MonoBehaviour
{

    [Header("Main UI")]
    [SerializeField] private MainUIController mainUIController;

    [Header("Timing")]
    [SerializeField] private float pauseBeforeDialog = 0.15f;
    [SerializeField] private float pauseAfterDialog = 0.20f;
    [SerializeField] private float pauseAfterShip = 0.15f;

    [Header("Ship Outro")]
    [SerializeField] private Transform shipRoot;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private SpriteRenderer shipSpriteRenderer;
    [SerializeField] private float shipDepartDuration = 0.55f;
    [SerializeField] private float shipOffscreenMarginWorld = 0.6f;
    [SerializeField] private float shipOvershootWorld = 0.8f;

    [Header("Flash")]
    [SerializeField] private CanvasGroup flashCanvasGroup;
    [SerializeField] private float flashPeakAlpha = 0.85f;
    [SerializeField] private float flashDuration = 0.12f;

    private bool isRunning;
    private bool skipRequested;
    private Coroutine playRoutine;
    private ShipIdleAnimation shipIdleAnimation;

    public bool IsRunning => isRunning;

    public void Play(string levelId, Action onComplete)
    {
        if (isRunning)
            return;

        StopCurrentTransition();

        skipRequested = false;

        if (!gameObject.activeInHierarchy)
        {
            onComplete?.Invoke();
            return;
        }

        playRoutine = StartCoroutine(PlayRoutine(levelId, onComplete));
    }

    public void RequestSkipDialog()
    {
        if (!isRunning || skipRequested)
            return;

        skipRequested = true;

        mainUIController?.StopAndHideDialog();

        mainUIController?.HideHoldToSkip(this);
    }

    private IEnumerator PlayRoutine(string levelId, Action onComplete)
    {
        isRunning = true;

        if (pauseBeforeDialog > 0f)
            yield return new WaitForSecondsRealtime(pauseBeforeDialog);

        yield return StartCoroutine(PlayOutroDialogIfAny(levelId));

        mainUIController?.HideHoldToSkip(this);

        if (!skipRequested && pauseAfterDialog > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterDialog);

        StartCoroutine(PlayFlashIfAny());

        yield return StartCoroutine(PlayShipDepartIfAny());

        if (pauseAfterShip > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterShip);

        isRunning = false;
        playRoutine = null;

        onComplete?.Invoke();
    }

    private IEnumerator PlayOutroDialogIfAny(string levelId)
    {
        if (mainUIController == null)
            yield break;

        if (string.IsNullOrWhiteSpace(levelId))
            yield break;

        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null)
        {
            Debug.LogError("[MainExitTransitionController] LocalizationManager.Instance est null.");
            yield break;
        }

        while (!loc.IsReady)
        {
            if (skipRequested)
                yield break;

            yield return null;
        }

        DialogSequence sequence = loc.GetOutroSequence(levelId);
        if (sequence == null)
            yield break;

        DialogLine[] lines = loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;

        mainUIController?.ShowHoldToSkip(this, RequestSkipDialog);

        mainUIController.PlayDialogSequence(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () => done = true
        );

        while (!done)
        {
            if (skipRequested)
            {
                mainUIController.StopAndHideDialog();

                mainUIController?.HideHoldToSkip(this);

                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator PlayShipDepartIfAny()
    {
        if (shipRoot == null)
            yield break;

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (cam == null || !cam.orthographic)
            yield break;

        SpriteRenderer sr = shipSpriteRenderer != null
            ? shipSpriteRenderer
            : shipRoot.GetComponentInChildren<SpriteRenderer>();

        if (sr == null)
            yield break;

        // L animation idle ecrit elle aussi la position du vaisseau a chaque frame.
        // Elle doit s arreter avant l outro, sinon elle le ramene a sa position de repos.
        DisableShipIdleAnimation();

        float duration = Mathf.Max(0.01f, shipDepartDuration);

        Vector3 start = shipRoot.position;

        float camTopY = cam.transform.position.y + cam.orthographicSize;
        float halfHeight = sr.bounds.extents.y;
        float targetBottomY = camTopY + Mathf.Max(0f, shipOffscreenMarginWorld);
        float endY = targetBottomY + halfHeight;

        Vector3 end = new Vector3(start.x, endY, start.z);

        float overshoot = Mathf.Max(0f, shipOvershootWorld);
        Vector3 overshootPos = new Vector3(start.x, endY + overshoot, start.z);

        float phase1Ratio = overshoot > 0f ? 0.70f : 1f;
        float duration1 = duration * Mathf.Clamp01(phase1Ratio);
        float duration2 = duration - duration1;

        float elapsed = 0f;

        while (elapsed < duration1)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration1);
            float eased = t * t;

            shipRoot.position = Vector3.LerpUnclamped(
                start,
                overshoot > 0f ? overshootPos : end,
                eased
            );

            yield return null;
        }

        shipRoot.position = overshoot > 0f ? overshootPos : end;

        if (overshoot > 0f && duration2 > 0.001f)
        {
            float elapsed2 = 0f;

            while (elapsed2 < duration2)
            {
                elapsed2 += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed2 / duration2);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                shipRoot.position = Vector3.LerpUnclamped(overshootPos, end, eased);

                yield return null;
            }
        }

        shipRoot.position = end;
    }

    private void DisableShipIdleAnimation()
    {
        if (shipIdleAnimation == null && shipRoot != null)
            shipIdleAnimation = shipRoot.GetComponent<ShipIdleAnimation>();

        if (shipIdleAnimation != null)
            shipIdleAnimation.enabled = false;
    }

    private IEnumerator PlayFlashIfAny()
    {
        if (flashCanvasGroup == null)
            yield break;

        float duration = Mathf.Max(0.01f, flashDuration);
        float half = duration * 0.5f;

        flashCanvasGroup.gameObject.SetActive(true);
        flashCanvasGroup.alpha = 0f;
        flashCanvasGroup.interactable = false;
        flashCanvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / half);
            flashCanvasGroup.alpha = Mathf.Lerp(0f, flashPeakAlpha, t);

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / half);
            flashCanvasGroup.alpha = Mathf.Lerp(flashPeakAlpha, 0f, t);

            yield return null;
        }

        flashCanvasGroup.alpha = 0f;
    }

    public void StopCurrentTransition()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        mainUIController?.StopAndHideDialog();

        mainUIController?.HideHoldToSkip(this);

        isRunning = false;
        skipRequested = false;
    }

    private void OnDisable()
    {
        StopCurrentTransition();
    }
}
