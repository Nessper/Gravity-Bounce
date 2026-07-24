using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controleur simple des couches visuelles globales de la scene Main.
///
/// Responsabilites actuelles :
/// - poser l etat visuel initial
/// - afficher / fermer le briefing
/// - afficher / cacher la pause via PauseOverlayController
/// - gerer les couches globales de fin de niveau
/// - exposer des helpers dedies au dimmer du tuto
///
/// IMPORTANT :
/// - ce script ne connait pas la logique metier Retry / Menu / Score
/// - ce script ne connait pas le contenu detaille des overlays
/// - il orchestre seulement l affichage global des couches UI
/// </summary>
public class MainUIController : MonoBehaviour
{
    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [SerializeField] private CanvasGroup dimmerGroup;
    [SerializeField] private CanvasGroup levelBriefingGroup;
    [SerializeField] private CanvasGroup pauseOverlayGroup;

    [Header("Shared Overlays")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

    [Header("Shared Dialogs")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("End Level")]
    [SerializeField] private CanvasGroup gameplayHudGroup;
    [SerializeField] private CanvasGroup resultsCeremonyOverlayGroup;
    [SerializeField] private CanvasGroup endResultOverlayGroup;

    [Header("Overlay Controllers")]
    [SerializeField] private PauseOverlayController pauseOverlayController;

    [Header("Gameplay Overlays")]
    [SerializeField] private FlushComboOverlayController flushComboOverlayController;
    [SerializeField] private RuntimeComboOverlayController runtimeComboOverlayController;

    [Header("Timings")]
    [SerializeField] private float briefingFadeDuration = 0.25f;
    [SerializeField] private float closeBriefingFadeDuration = 0.25f;

    [Header("End Flow Transitions")]
    [SerializeField] private float resultsCeremonyFadeDuration = 0.5f;
    [SerializeField] private float endResultFadeDuration = 0.5f;

    [Header("Tutorial Dimmer")]
    [SerializeField] private float tutorialDimmerDialogueAlpha = 0.65f;
    [SerializeField] private float tutorialDimmerRestAlpha = 0.15f;
    [SerializeField] private float tutorialDimmerFadeDuration = 0.2f;

    private Coroutine currentRoutine;

    private void Awake()
    {
        HidePauseOverlay();
    }

    private void OnEnable()
    {
        if (pauseOverlayController != null)
        {
            pauseOverlayController.OnPauseOpened += HandlePauseOpened;
            pauseOverlayController.OnPauseClosed += HandlePauseClosed;
        }
    }

    private void OnDisable()
    {
        if (pauseOverlayController != null)
        {
            pauseOverlayController.OnPauseOpened -= HandlePauseOpened;
            pauseOverlayController.OnPauseClosed -= HandlePauseClosed;
        }

        flushComboOverlayController?.CancelAllAndSync();
    }

    public void SetInitialState()
    {
        StopRunningRoutine();
        flushComboOverlayController?.CancelAllAndSync();

        SetCanvasGroup(backgroundGroup, 1f, false, false);
        SetCanvasGroup(dimmerGroup, 1f, false, false);
        SetCanvasGroup(levelBriefingGroup, 0f, false, false);
        SetCanvasGroup(pauseOverlayGroup, 0f, false, false);

        SetCanvasGroup(gameplayHudGroup, 1f, true, true);
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);

        runtimeComboOverlayController?.RefreshAll();
    }

    /// <summary>
    /// Efface les indicateurs visuels de combos sans modifier le score.
    /// </summary>
    public void ClearRuntimeComboIndicators()
    {
        runtimeComboOverlayController?.ClearPresentation();
    }

    public void InitializePauseOverlay(
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData levelData,
        Action onRetry,
        Action onMenu)
    {
        if (pauseOverlayController == null)
            return;

        pauseOverlayController.ForceResume();
        pauseOverlayController.Configure(levelMeta, levelData, onRetry, onMenu);
        pauseOverlayController.EnablePause(false);

        HidePauseOverlay();
    }

    public void EnablePause(bool enabled)
    {
        pauseOverlayController?.EnablePause(enabled);
    }

    public void ForceResumePause()
    {
        pauseOverlayController?.ForceResume();
    }

    public void ShowLevelBriefing(Action onComplete = null)
    {
        StopRunningRoutine();
        currentRoutine = StartCoroutine(ShowLevelBriefingRoutine(onComplete));
    }

    public void CloseBriefingForIntro(Action onComplete = null)
    {
        StopRunningRoutine();
        currentRoutine = StartCoroutine(CloseBriefingForIntroRoutine(onComplete));
    }

    public void ShowGameplayHud()
    {
        SetCanvasGroup(gameplayHudGroup, 1f, true, true);
    }

    public void HideGameplayHud()
    {
        flushComboOverlayController?.CancelAllAndSync();
        SetCanvasGroup(gameplayHudGroup, 0f, false, false);
    }

    public Coroutine ShowResultsCeremonyView(MonoBehaviour owner, Action onComplete = null)
    {
        if (owner == null)
            return null;

        return owner.StartCoroutine(ShowResultsCeremonyViewRoutine(onComplete));
    }

    public void HideResultsCeremonyView()
    {
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
    }

    public Coroutine ShowEndResultView(MonoBehaviour owner, Action onComplete = null)
    {
        if (owner == null)
            return null;

        return owner.StartCoroutine(ShowEndResultViewRoutine(onComplete));
    }

    public void HideEndResultView()
    {
        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);
    }

    public Coroutine HideEndResultViewAnimated(MonoBehaviour owner, Action onComplete = null)
    {
        if (owner == null)
            return null;

        return owner.StartCoroutine(HideEndResultViewAnimatedRoutine(onComplete));
    }

    public void SetBackgroundImmediate(
        float alpha,
        bool interactable = false,
        bool blocksRaycasts = false)
    {
        SetCanvasGroup(backgroundGroup, alpha, interactable, blocksRaycasts);
    }

    public void SetDimmerImmediate(
        float alpha,
        bool interactable = false,
        bool blocksRaycasts = false)
    {
        SetCanvasGroup(dimmerGroup, alpha, interactable, blocksRaycasts);
    }

    public Coroutine FadeDimmerTo(
        MonoBehaviour owner,
        float targetAlpha,
        float duration,
        bool interactableAtEnd = false,
        bool blocksRaycastsAtEnd = false,
        Action onComplete = null)
    {
        if (owner == null)
            return null;

        float startAlpha = dimmerGroup != null ? dimmerGroup.alpha : 0f;

        return owner.StartCoroutine(
            FadeCanvasGroup(
                dimmerGroup,
                startAlpha,
                targetAlpha,
                duration,
                interactableAtEnd,
                blocksRaycastsAtEnd,
                onComplete
            )
        );
    }

    public Coroutine ShowTutorialDimmer(MonoBehaviour owner, Action onComplete = null)
    {
        return FadeDimmerTo(
            owner,
            tutorialDimmerDialogueAlpha,
            tutorialDimmerFadeDuration,
            false,
            true,
            onComplete
        );
    }

    public Coroutine HideTutorialDimmer(MonoBehaviour owner, Action onComplete = null)
    {
        return FadeDimmerTo(
            owner,
            tutorialDimmerRestAlpha,
            tutorialDimmerFadeDuration,
            false,
            false,
            onComplete
        );
    }

    public void ShowTutorialDimmerImmediate()
    {
        SetDimmerImmediate(tutorialDimmerDialogueAlpha, false, true);
    }

    public void HideTutorialDimmerImmediate()
    {
        SetDimmerImmediate(tutorialDimmerRestAlpha, false, false);
    }

    public void ShowPauseOverlay()
    {
        SetCanvasGroup(pauseOverlayGroup, 1f, true, true);
    }

    public void HidePauseOverlay()
    {
        SetCanvasGroup(pauseOverlayGroup, 0f, false, false);
    }

    private void HandlePauseOpened()
    {
        SetBackgroundImmediate(1f, false, false);
        ShowPauseOverlay();
    }

    private void HandlePauseClosed()
    {
        HidePauseOverlay();
        SetBackgroundImmediate(0f, false, false);
    }

    private IEnumerator ShowResultsCeremonyViewRoutine(Action onComplete)
    {
        HideGameplayHud();

        SetCanvasGroup(backgroundGroup, 1f, false, false);
        SetCanvasGroup(dimmerGroup, 1f, false, false);
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, true, true);
        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);

        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(dimmerGroup, 1f, 0f, resultsCeremonyFadeDuration, false, false)
        );

        Coroutine overlayFade = StartCoroutine(
            FadeCanvasGroup(resultsCeremonyOverlayGroup, 0f, 1f, resultsCeremonyFadeDuration, true, true)
        );

        yield return dimmerFade;
        yield return overlayFade;

        onComplete?.Invoke();
    }

    private IEnumerator ShowEndResultViewRoutine(Action onComplete)
    {
        bool backgroundNeedsReveal =
            backgroundGroup != null &&
            backgroundGroup.alpha < 0.999f;

        if (backgroundNeedsReveal)
        {
            HideGameplayHud();

            SetCanvasGroup(dimmerGroup, 0f, false, false);
            SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
            SetCanvasGroup(endResultOverlayGroup, 0f, false, false);

            yield return StartCoroutine(
                FadeCanvasGroup(
                    backgroundGroup,
                    backgroundGroup.alpha,
                    1f,
                    endResultFadeDuration,
                    false,
                    false
                )
            );

            yield return StartCoroutine(
                FadeCanvasGroup(
                    endResultOverlayGroup,
                    0f,
                    1f,
                    endResultFadeDuration,
                    true,
                    true
                )
            );

            onComplete?.Invoke();
            yield break;
        }

        SetCanvasGroup(dimmerGroup, 1f, false, false);
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
        SetCanvasGroup(endResultOverlayGroup, 0f, true, true);

        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(dimmerGroup, 1f, 0f, endResultFadeDuration, false, false)
        );

        Coroutine endResultFade = StartCoroutine(
            FadeCanvasGroup(endResultOverlayGroup, 0f, 1f, endResultFadeDuration, true, true)
        );

        yield return dimmerFade;
        yield return endResultFade;

        onComplete?.Invoke();
    }

    private IEnumerator HideEndResultViewAnimatedRoutine(Action onComplete)
    {
        float endResultFrom = endResultOverlayGroup != null ? endResultOverlayGroup.alpha : 1f;
        float dimmerFrom = dimmerGroup != null ? dimmerGroup.alpha : 0f;

        Coroutine endResultFade = StartCoroutine(
            FadeCanvasGroup(endResultOverlayGroup, endResultFrom, 0f, endResultFadeDuration, false, false)
        );

        Coroutine dimmerFadeIn = StartCoroutine(
            FadeCanvasGroup(dimmerGroup, dimmerFrom, 1f, endResultFadeDuration, false, false)
        );

        yield return endResultFade;
        yield return dimmerFadeIn;

        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);
        SetCanvasGroup(backgroundGroup, 0f, false, false);

        yield return StartCoroutine(
            FadeCanvasGroup(dimmerGroup, 1f, 0f, endResultFadeDuration, false, false)
        );

        SetCanvasGroup(dimmerGroup, 0f, false, false);

        onComplete?.Invoke();
    }

    private IEnumerator ShowLevelBriefingRoutine(Action onComplete)
    {
        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(
                dimmerGroup,
                dimmerGroup != null ? dimmerGroup.alpha : 1f,
                0f,
                briefingFadeDuration,
                false,
                false
            )
        );

        Coroutine briefingFade = StartCoroutine(
            FadeCanvasGroup(
                levelBriefingGroup,
                levelBriefingGroup != null ? levelBriefingGroup.alpha : 0f,
                1f,
                briefingFadeDuration,
                true,
                true
            )
        );

        yield return dimmerFade;
        yield return briefingFade;

        currentRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator CloseBriefingForIntroRoutine(Action onComplete)
    {
        Coroutine briefingFade = StartCoroutine(
            FadeCanvasGroup(
                levelBriefingGroup,
                levelBriefingGroup != null ? levelBriefingGroup.alpha : 1f,
                0f,
                closeBriefingFadeDuration,
                false,
                false
            )
        );

        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(
                dimmerGroup,
                dimmerGroup != null ? dimmerGroup.alpha : 0f,
                1f,
                closeBriefingFadeDuration,
                false,
                false
            )
        );

        yield return briefingFade;
        yield return dimmerFade;

        currentRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator FadeCanvasGroup(
        CanvasGroup group,
        float from,
        float to,
        float duration,
        bool interactableAtEnd = false,
        bool blocksRaycastsAtEnd = false,
        Action onComplete = null)
    {
        if (group == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = from;

        if (duration <= 0f)
        {
            group.alpha = to;
            group.interactable = interactableAtEnd;
            group.blocksRaycasts = blocksRaycastsAtEnd;
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);

            yield return null;
        }

        group.alpha = to;
        group.interactable = interactableAtEnd;
        group.blocksRaycasts = blocksRaycastsAtEnd;

        onComplete?.Invoke();
    }

    private void SetCanvasGroup(
        CanvasGroup group,
        float alpha,
        bool interactable,
        bool blocksRaycasts)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = blocksRaycasts;
    }

    private void StopRunningRoutine()
    {
        if (currentRoutine == null)
            return;

        StopCoroutine(currentRoutine);
        currentRoutine = null;
    }

    public void ShowHoldToSkip(MonoBehaviour owner, Action onComplete)
    {
        if (holdToSkipOverlay == null || owner == null)
            return;

        holdToSkipOverlay.Show(owner, onComplete);
    }

    public void HideHoldToSkip(MonoBehaviour owner)
    {
        if (holdToSkipOverlay == null || owner == null)
            return;

        holdToSkipOverlay.Hide(owner);
    }

    public void PlayDialogSequence(
        DialogLine[] lines,
        DialogSequenceRunner.PlaybackMode mode,
        Action onComplete)
    {
        if (dialogSequenceRunner == null)
        {
            onComplete?.Invoke();
            return;
        }

        dialogSequenceRunner.Play(lines, mode, onComplete);
    }

    public void StopAndHideDialog()
    {
        dialogSequenceRunner?.StopAndHide();
    }

    public void PlayFlushResolution(FlushResolution resolution)
    {
        if (flushComboOverlayController == null)
            return;

        flushComboOverlayController.Play(resolution);
    }

    public IEnumerator WaitForFinalScorePresentation()
    {
        if (flushComboOverlayController == null)
            yield break;

        yield return flushComboOverlayController.WaitForFinalPresentationComplete();
    }
}
