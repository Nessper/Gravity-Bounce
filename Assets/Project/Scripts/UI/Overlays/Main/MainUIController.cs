using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controleur simple des couches visuelles globales de la scene Main.
///
/// Responsabilites actuelles :
/// - poser l etat visuel initial
/// - afficher le briefing
/// - fermer le briefing et remonter le dimmer a 1
/// - exposer des setters simples pour background / dimmer
/// - servir de point d entree pour la pause
/// - gerer la visibilite de la pause a partir des evenements du PauseOverlayController
/// - exposer des helpers dedies au dimmer du tuto
/// - centraliser progressivement les couches globales de fin de niveau
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

    [Header("End Level")]
    [SerializeField] private CanvasGroup gameplayHudGroup;
    [SerializeField] private CanvasGroup resultsCeremonyOverlayGroup;
    [SerializeField] private CanvasGroup endResultOverlayGroup;

    [Header("Overlay Controllers")]
    [SerializeField] private PauseOverlayController pauseOverlayController;

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
    }

    /// <summary>
    /// Pose l etat visuel initial global de la scene.
    /// </summary>
    public void SetInitialState()
    {
        StopRunningRoutine();

        SetCanvasGroup(backgroundGroup, 1f, false, false);
        SetCanvasGroup(dimmerGroup, 1f, false, false);
        SetCanvasGroup(levelBriefingGroup, 0f, false, false);
        SetCanvasGroup(pauseOverlayGroup, 0f, false, false);

        SetCanvasGroup(gameplayHudGroup, 1f, true, true);
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);
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
        SetCanvasGroup(gameplayHudGroup, 0f, false, false);
    }

    /// <summary>
    /// Affiche la vue globale de la Results Ceremony.
    /// Cette methode ne joue pas la ceremony.
    /// </summary>
    public Coroutine ShowResultsCeremonyView(MonoBehaviour owner, Action onComplete = null)
    {
        if (owner == null)
            return null;

        return owner.StartCoroutine(ShowResultsCeremonyViewRoutine(onComplete));
    }

    private IEnumerator ShowResultsCeremonyViewRoutine(Action onComplete)
    {
        HideGameplayHud();

        SetCanvasGroup(backgroundGroup, 1f, false, false);
        SetCanvasGroup(dimmerGroup, 1f, false, false);
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, true, true);
        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);

        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(
                dimmerGroup,
                1f,
                0f,
                resultsCeremonyFadeDuration,
                false,
                false
            )
        );

        Coroutine overlayFade = StartCoroutine(
            FadeCanvasGroup(
                resultsCeremonyOverlayGroup,
                0f,
                1f,
                resultsCeremonyFadeDuration,
                true,
                true
            )
        );

        yield return dimmerFade;
        yield return overlayFade;

        onComplete?.Invoke();
    }

    public void HideResultsCeremonyView()
    {
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
    }

    /// <summary>
    /// Transition globale Results Ceremony -> End Result.
    /// Flow vise :
    /// - dimmer remis instant a 1
    /// - Results Ceremony cachee instant
    /// - End Result visible a 0
    /// - fade dimmer vers 0
    /// - fade End Result vers 1
    ///
    /// IMPORTANT :
    /// - cette methode ne joue pas le contenu interne de l overlay finale
    /// - elle prepare seulement la transition globale entre les deux vues
    /// </summary>
    public Coroutine ShowEndResultView(MonoBehaviour owner, Action onComplete = null)
    {
        if (owner == null)
            return null;

        return owner.StartCoroutine(ShowEndResultViewRoutine(onComplete));
    }

    private IEnumerator ShowEndResultViewRoutine(Action onComplete)
    {
        SetCanvasGroup(dimmerGroup, 1f, false, false);
        SetCanvasGroup(resultsCeremonyOverlayGroup, 0f, false, false);
        SetCanvasGroup(endResultOverlayGroup, 0f, true, true);

        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(
                dimmerGroup,
                1f,
                0f,
                endResultFadeDuration,
                false,
                false
            )
        );

        Coroutine endResultFade = StartCoroutine(
            FadeCanvasGroup(
                endResultOverlayGroup,
                0f,
                1f,
                endResultFadeDuration,
                true,
                true
            )
        );

        yield return dimmerFade;
        yield return endResultFade;

        onComplete?.Invoke();
    }

    public void HideEndResultView()
    {
        SetCanvasGroup(endResultOverlayGroup, 0f, false, false);
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
            interactableAtEnd: false,
            blocksRaycastsAtEnd: true,
            onComplete: onComplete
        );
    }

    public Coroutine HideTutorialDimmer(MonoBehaviour owner, Action onComplete = null)
    {
        return FadeDimmerTo(
            owner,
            tutorialDimmerRestAlpha,
            tutorialDimmerFadeDuration,
            interactableAtEnd: false,
            blocksRaycastsAtEnd: false,
            onComplete: onComplete
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

    private IEnumerator ShowLevelBriefingRoutine(Action onComplete)
    {
        Coroutine dimmerFade = StartCoroutine(
            FadeCanvasGroup(
                dimmerGroup,
                dimmerGroup != null ? dimmerGroup.alpha : 1f,
                0f,
                briefingFadeDuration
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
}