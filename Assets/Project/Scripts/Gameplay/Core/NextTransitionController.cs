using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gere la transition "Next" apres la fin de niveau :
/// - fermeture du panneau final
/// - dialogue d outro optionnel
/// - skip du dialogue via hold-to-skip
/// - depart du vaisseau
/// - callback final
///
/// Regle importante :
/// ce controller utilise l overlay partage proprement avec
/// Show(this, ...) et Hide(this) uniquement.
/// </summary>
public class NextTransitionController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private GameObject endLevelRoot;
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Hold To Skip")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

    [Header("Timing (unscaled)")]
    [SerializeField] private float pauseAfterHide = 0.25f;
    [SerializeField] private float pauseAfterDialog = 0.20f;
    [SerializeField] private float pauseAfterShip = 0.15f;

    [Header("Ship Outro (optional)")]
    [SerializeField] private Transform shipRoot;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private SpriteRenderer shipSpriteRenderer;
    [SerializeField] private float shipDepartDuration = 0.55f;
    [SerializeField] private float shipOffscreenMarginWorld = 0.6f;
    [SerializeField] private float shipOvershootWorld = 0.8f;

    [Header("Flash (optional)")]
    [SerializeField] private CanvasGroup flashCanvasGroup;
    [SerializeField] private float flashPeakAlpha = 0.85f;
    [SerializeField] private float flashDuration = 0.12f;

    private bool isRunning;
    private bool skipRequested;
    private Coroutine playRoutine;

    private LocalizationManager Loc => LocalizationManager.Instance;

    public bool IsRunning => isRunning;

    /// <summary>
    /// Lance la transition de sortie de niveau.
    /// </summary>
    public void PlayOutroAndFinish(Action onComplete)
    {
        if (isRunning)
            return;

        StopControllerRoutinesOnly();

        skipRequested = false;
        isRunning = false;

        if (gameObject.activeInHierarchy)
            playRoutine = StartCoroutine(Routine(onComplete));
    }

    /// <summary>
    /// Callback appele quand le hold est complete.
    /// Ici on saute uniquement la phase dialogue.
    /// </summary>
    public void OnSkipButtonPressed()
    {
        if (!isRunning || skipRequested)
            return;

        skipRequested = true;

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Relache l overlay si le controller est desactive en cours d usage.
    /// </summary>
    private void OnDisable()
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Routine complete de transition de sortie.
    /// </summary>
    private IEnumerator Routine(Action onComplete)
    {
        isRunning = true;

        if (endLevelRoot != null && endLevelRoot.activeSelf)
            endLevelRoot.SetActive(false);

        if (pauseAfterHide > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterHide);

        yield return StartCoroutine(PlayOutroIfAny());

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        if (!skipRequested && pauseAfterDialog > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterDialog);

        StartCoroutine(PlayFlashIfAny());
        yield return StartCoroutine(PlayShipDepartUpAndOffscreenIfAny());

        if (pauseAfterShip > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterShip);

        isRunning = false;
        playRoutine = null;

        onComplete?.Invoke();
    }

    /// <summary>
    /// Joue le dialogue d outro s il existe.
    /// </summary>
    private IEnumerator PlayOutroIfAny()
    {
        if (dialogSequenceRunner == null)
            yield break;

        if (Loc == null)
        {
            Debug.LogError("[NextTransitionController] LocalizationManager.Instance est null.");
            yield break;
        }

        while (!Loc.IsReady)
        {
            if (skipRequested)
                yield break;

            yield return null;
        }

        string levelId = (endLevelUI != null) ? endLevelUI.CurrentLevelId : null;
        if (string.IsNullOrWhiteSpace(levelId))
        {
            Debug.LogWarning("[NextTransitionController] CurrentLevelId vide, outro ignoree.");
            yield break;
        }

        DialogSequence sequence = Loc.GetOutroSequence(levelId);
        if (sequence == null)
            yield break;

        DialogLine[] lines = Loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Show(this, OnSkipButtonPressed);

        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () => done = true
        );

        while (!done)
        {
            if (skipRequested)
            {
                dialogSequenceRunner.StopAndHide();

                if (holdToSkipOverlay != null)
                    holdToSkipOverlay.Hide(this);

                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Anime le depart du vaisseau vers le haut jusqu a sortir de l ecran.
    /// </summary>
    private IEnumerator PlayShipDepartUpAndOffscreenIfAny()
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

        float dur = Mathf.Max(0.01f, shipDepartDuration);

        Vector3 start = shipRoot.position;

        float camTopY = cam.transform.position.y + cam.orthographicSize;
        float halfHeight = sr.bounds.extents.y;
        float targetBottomY = camTopY + Mathf.Max(0f, shipOffscreenMarginWorld);
        float endY = targetBottomY + halfHeight;

        Vector3 end = new Vector3(start.x, endY, start.z);

        float overshoot = Mathf.Max(0f, shipOvershootWorld);
        Vector3 overshootPos = new Vector3(start.x, endY + overshoot, start.z);

        float phase1Ratio = (overshoot > 0f) ? 0.70f : 1f;
        float dur1 = dur * Mathf.Clamp01(phase1Ratio);
        float dur2 = dur - dur1;

        float t = 0f;
        while (t < dur1)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur1);
            float eased = k * k;

            shipRoot.position = Vector3.LerpUnclamped(
                start,
                overshoot > 0f ? overshootPos : end,
                eased
            );

            yield return null;
        }

        shipRoot.position = overshoot > 0f ? overshootPos : end;

        if (overshoot > 0f && dur2 > 0.001f)
        {
            float t2 = 0f;
            while (t2 < dur2)
            {
                t2 += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t2 / dur2);
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                shipRoot.position = Vector3.LerpUnclamped(overshootPos, end, eased);
                yield return null;
            }
        }

        shipRoot.position = end;
    }

    /// <summary>
    /// Joue un flash optionnel pendant la transition.
    /// </summary>
    private IEnumerator PlayFlashIfAny()
    {
        if (flashCanvasGroup == null)
            yield break;

        float dur = Mathf.Max(0.01f, flashDuration);
        float half = dur * 0.5f;

        flashCanvasGroup.gameObject.SetActive(true);
        flashCanvasGroup.alpha = 0f;
        flashCanvasGroup.blocksRaycasts = false;
        flashCanvasGroup.interactable = false;

        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            flashCanvasGroup.alpha = Mathf.Lerp(0f, flashPeakAlpha, k);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            flashCanvasGroup.alpha = Mathf.Lerp(flashPeakAlpha, 0f, k);
            yield return null;
        }

        flashCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Stoppe uniquement les routines de ce controller
    /// et relache sa possession eventuelle de l overlay.
    /// </summary>
    private void StopControllerRoutinesOnly()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        isRunning = false;
    }
}