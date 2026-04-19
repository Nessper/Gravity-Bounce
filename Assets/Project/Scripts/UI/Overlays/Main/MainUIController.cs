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
///
/// IMPORTANT :
/// - ce script ne connait pas le contenu detaille des overlays
/// - il orchestre les controllers d overlays depuis l exterieur
/// </summary>
public class MainUIController : MonoBehaviour
{
    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [SerializeField] private CanvasGroup dimmerGroup;
    [SerializeField] private CanvasGroup levelBriefingGroup;
    [SerializeField] private CanvasGroup pauseOverlayGroup;

    [Header("Overlay Controllers")]
    [SerializeField] private PauseOverlayController pauseOverlayController;

    [Header("Timings")]
    [SerializeField] private float briefingFadeDuration = 0.25f;
    [SerializeField] private float closeBriefingFadeDuration = 0.25f;

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
    }

    /// <summary>
    /// Configure l overlay de pause une seule fois au debut du niveau.
    /// </summary>
    public void SetupPause(
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

    /// <summary>
    /// Active ou non la possibilite d ouvrir la pause.
    /// </summary>
    public void EnablePause(bool enabled)
    {
        pauseOverlayController?.EnablePause(enabled);
    }

    /// <summary>
    /// Force la fermeture de la pause si elle est ouverte.
    /// </summary>
    public void ForceResumePause()
    {
        pauseOverlayController?.ForceResume();
    }

    /// <summary>
    /// Affiche visuellement le briefing.
    /// </summary>
    public void ShowLevelBriefing(Action onComplete = null)
    {
        StopRunningRoutine();
        currentRoutine = StartCoroutine(ShowLevelBriefingRoutine(onComplete));
    }

    /// <summary>
    /// Ferme visuellement le briefing et remonte le dimmer a 1
    /// pour passer le relais a l intro.
    /// </summary>
    public void CloseBriefingForIntro(Action onComplete = null)
    {
        StopRunningRoutine();
        currentRoutine = StartCoroutine(CloseBriefingForIntroRoutine(onComplete));
    }

    /// <summary>
    /// Force immediatement l etat du background global.
    /// </summary>
    public void SetBackgroundImmediate(
        float alpha,
        bool interactable = false,
        bool blocksRaycasts = false)
    {
        SetCanvasGroup(backgroundGroup, alpha, interactable, blocksRaycasts);
    }

    /// <summary>
    /// Force immediatement l etat du dimmer global.
    /// </summary>
    public void SetDimmerImmediate(
        float alpha,
        bool interactable = false,
        bool blocksRaycasts = false)
    {
        SetCanvasGroup(dimmerGroup, alpha, interactable, blocksRaycasts);
    }

    /// <summary>
    /// Lance un fade du dimmer global.
    /// </summary>
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

    /// <summary>
    /// Affiche l overlay pause.
    /// </summary>
    public void ShowPauseOverlay()
    {
        SetCanvasGroup(pauseOverlayGroup, 1f, true, true);
    }

    /// <summary>
    /// Cache l overlay pause.
    /// </summary>
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