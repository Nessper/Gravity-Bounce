using System.Collections;
using System;
using UnityEngine;

/// <summary>
/// Gere la transition visuelle entre un ecran source
/// (ex: Shop, Briefing) et l overlay Ship Systems.
///
/// Responsabilites :
/// - faire disparaitre doucement l UI source
/// - passer par un fade dimmer
/// - afficher Ship Systems avec un leger punch visuel
/// - permettre le retour vers l ecran source
///
/// IMPORTANT :
/// - script specifique au flow du bouton Tuning
/// - ne remplace pas un futur manager global d overlays
/// - les backgrounds sont optionnels :
///   si les refs ne sont pas renseignees, les etapes liees au background
///   sont simplement ignorees
/// </summary>
public class ShipSystemsOverlayTransitionController : MonoBehaviour
{
    /// <summary>
    /// Emis lorsque l ecran source est quitte pour Ship Systems.
    /// Les overlays sont masques par CanvasGroup et ne recoivent donc pas OnDisable.
    /// </summary>
    public static event Action SourceUiHidden;

    /// <summary>
    /// Emis lorsque Ship Systems est quitte pour revenir a l ecran source.
    /// </summary>
    public static event Action ShipSystemsUiHidden;

    [Header("Dimmer")]
    [SerializeField] private CanvasGroup dimmerCanvasGroup;

    [Header("Source Overlay")]
    [SerializeField] private CanvasGroup sourceBackgroundCanvasGroup;
    [SerializeField] private CanvasGroup sourceUiCanvasGroup;
    [SerializeField] private RectTransform sourceUiRoot;

    [Header("Ship Systems Overlay")]
    [SerializeField] private CanvasGroup shipSystemsBackgroundCanvasGroup;
    [SerializeField] private CanvasGroup shipSystemsUiCanvasGroup;
    [SerializeField] private RectTransform shipSystemsUiRoot;

    [Header("Timings")]
    [SerializeField] private float dimmerFadeInSeconds = 0.30f;
    [SerializeField] private float dimmerFadeToShipSystemsSeconds = 0.30f;
    [SerializeField] private float dimmerFadeToSourceSeconds = 0.30f;

    [SerializeField] private float sourceUiFadeInSeconds = 0.22f;
    [SerializeField] private float shipSystemsUiFadeInSeconds = 0.22f;

    [Header("Pre Fade")]
    [SerializeField] private float sourceUiPreFadeOutSeconds = 0.10f;
    [SerializeField] private float shipSystemsUiPreFadeOutSeconds = 0.10f;

    [Header("Ambient Dim")]
    [Range(0f, 1f)]
    [SerializeField] private float shipSystemsDimAlpha = 0.30f;

    [Range(0f, 1f)]
    [SerializeField] private float sourceDimAlpha = 0.80f;

    [Header("UI Punch")]
    [SerializeField] private float uiStartScale = 0.96f;
    [SerializeField] private float uiEndScale = 1.02f;
    [SerializeField] private float uiSettleScale = 1f;

    private bool isRunning;
    private Coroutine activeRoutine;

    private void Awake()
    {
        RestoreSourceState(true);
    }

    // ------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------

    /// <summary>
    /// Ouvre l overlay Ship Systems depuis l ecran source.
    /// </summary>
    public void PlayToShipSystemsTransition()
    {
        if (isRunning)
            return;

        StopActiveRoutine();
        activeRoutine = StartCoroutine(TransitionToShipSystemsRoutine());
    }

    /// <summary>
    /// Revient de Ship Systems vers l ecran source.
    /// </summary>
    public void PlayBackToSourceTransition()
    {
        if (isRunning)
            return;

        StopActiveRoutine();
        activeRoutine = StartCoroutine(TransitionBackToSourceRoutine());
    }

    /// <summary>
    /// Restaure instantanement l etat visuel source.
    /// Utile au boot de scene ou si on veut resynchroniser proprement.
    /// </summary>
    public void RestoreSourceState(bool keepAmbientDim = true)
    {
        if (HasSourceBackground())
            ShowSourceBackground();

        ShowSourceUiImmediate();

        if (HasShipSystemsBackground())
            HideShipSystemsBackground();

        HideShipSystemsUiImmediate();

        if (keepAmbientDim)
            SetDimmerAmbient(sourceDimAlpha);
        else
            DisableDimmerHard();

        isRunning = false;
    }

    // ------------------------------------------------------------
    // TRANSITIONS
    // ------------------------------------------------------------

    private IEnumerator TransitionToShipSystemsRoutine()
    {
        isRunning = true;

        // 0) Pré fade de l UI source
        yield return FadeUiOut(sourceUiCanvasGroup, sourceUiPreFadeOutSeconds);

        // 1) Fade au noir via dimmer
        EnsureDimmerReady();
        yield return FadeCanvasGroup(
            dimmerCanvasGroup,
            dimmerCanvasGroup.alpha,
            1f,
            dimmerFadeInSeconds
        );

        // 2) Swap visuel
        NotifySourceUiHidden();
        HideSourceUiImmediate();

        if (HasShipSystemsBackground())
            ShowShipSystemsBackground();

        HideShipSystemsUiImmediate();

        // 3) Redescente du dimmer vers l ambiance Ship Systems
        yield return FadeCanvasGroup(
            dimmerCanvasGroup,
            1f,
            shipSystemsDimAlpha,
            dimmerFadeToShipSystemsSeconds
        );

        ReleaseDimmerInput();

        // 4) Apparition de l UI Ship Systems avec punch
        yield return FadeUiWithPunch(
            shipSystemsUiCanvasGroup,
            shipSystemsUiRoot,
            shipSystemsUiFadeInSeconds
        );

        isRunning = false;
        activeRoutine = null;
    }

    private IEnumerator TransitionBackToSourceRoutine()
    {
        isRunning = true;

        // 0) Pré fade de l UI Ship Systems
        yield return FadeUiOut(shipSystemsUiCanvasGroup, shipSystemsUiPreFadeOutSeconds);

        // 1) Fade au noir via dimmer
        EnsureDimmerReady();
        yield return FadeCanvasGroup(
            dimmerCanvasGroup,
            dimmerCanvasGroup.alpha,
            1f,
            dimmerFadeInSeconds
        );

        // 2) Swap visuel
        NotifyShipSystemsUiHidden();
        HideShipSystemsUiImmediate();

        if (HasShipSystemsBackground())
            HideShipSystemsBackground();

        HideSourceUiImmediate();

        // 3) Redescente du dimmer vers l ambiance source
        yield return FadeCanvasGroup(
            dimmerCanvasGroup,
            1f,
            sourceDimAlpha,
            dimmerFadeToSourceSeconds
        );

        ReleaseDimmerInput();

        // 4) Réapparition de l UI source avec punch
        yield return FadeUiWithPunch(
            sourceUiCanvasGroup,
            sourceUiRoot,
            sourceUiFadeInSeconds
        );

        isRunning = false;
        activeRoutine = null;
    }

    // ------------------------------------------------------------
    // UI EFFECTS
    // ------------------------------------------------------------

    private IEnumerator FadeUiOut(CanvasGroup cg, float duration)
    {
        if (cg == null)
            yield break;

        cg.interactable = false;
        cg.blocksRaycasts = false;

        yield return FadeCanvasGroup(cg, cg.alpha, 0f, duration);
    }

    private IEnumerator FadeUiWithPunch(CanvasGroup cg, RectTransform root, float duration)
    {
        if (cg == null)
            yield break;

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (root != null)
            root.localScale = Vector3.one * uiStartScale;

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);

            cg.alpha = k;

            if (root != null)
            {
                float scale = Mathf.Lerp(uiStartScale, uiEndScale, k);
                root.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // Petit settle pour finir proprement
        if (root != null)
        {
            float settleTime = 0.08f;
            float t2 = 0f;
            float startScaleValue = root.localScale.x;

            while (t2 < settleTime)
            {
                t2 += Time.unscaledDeltaTime;
                float k2 = Mathf.Clamp01(t2 / settleTime);
                root.localScale = Vector3.one * Mathf.Lerp(startScaleValue, uiSettleScale, k2);
                yield return null;
            }
        }

        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    // ------------------------------------------------------------
    // SOURCE BACKGROUND / UI
    // ------------------------------------------------------------

    private bool HasSourceBackground()
    {
        return sourceBackgroundCanvasGroup != null;
    }

    private bool HasShipSystemsBackground()
    {
        return shipSystemsBackgroundCanvasGroup != null;
    }

    private void ShowSourceBackground()
    {
        if (sourceBackgroundCanvasGroup == null)
            return;

        sourceBackgroundCanvasGroup.alpha = 1f;
        sourceBackgroundCanvasGroup.interactable = false;
        sourceBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void HideSourceBackground()
    {
        if (sourceBackgroundCanvasGroup == null)
            return;

        sourceBackgroundCanvasGroup.alpha = 0f;
        sourceBackgroundCanvasGroup.interactable = false;
        sourceBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void ShowSourceUiImmediate()
    {
        if (sourceUiCanvasGroup == null)
            return;

        sourceUiCanvasGroup.alpha = 1f;
        sourceUiCanvasGroup.interactable = true;
        sourceUiCanvasGroup.blocksRaycasts = true;
    }

    private void HideSourceUiImmediate()
    {
        if (sourceUiCanvasGroup == null)
            return;

        sourceUiCanvasGroup.alpha = 0f;
        sourceUiCanvasGroup.interactable = false;
        sourceUiCanvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------
    // SHIP SYSTEMS BACKGROUND / UI
    // ------------------------------------------------------------

    private void ShowShipSystemsBackground()
    {
        if (shipSystemsBackgroundCanvasGroup == null)
            return;

        shipSystemsBackgroundCanvasGroup.alpha = 1f;
        shipSystemsBackgroundCanvasGroup.interactable = false;
        shipSystemsBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void HideShipSystemsBackground()
    {
        if (shipSystemsBackgroundCanvasGroup == null)
            return;

        shipSystemsBackgroundCanvasGroup.alpha = 0f;
        shipSystemsBackgroundCanvasGroup.interactable = false;
        shipSystemsBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void HideShipSystemsUiImmediate()
    {
        if (shipSystemsUiCanvasGroup == null)
            return;

        shipSystemsUiCanvasGroup.alpha = 0f;
        shipSystemsUiCanvasGroup.interactable = false;
        shipSystemsUiCanvasGroup.blocksRaycasts = false;
    }

    private void NotifySourceUiHidden()
    {
        SourceUiHidden?.Invoke();
    }

    private void NotifyShipSystemsUiHidden()
    {
        ShipSystemsUiHidden?.Invoke();
    }

    // ------------------------------------------------------------
    // DIMMER
    // ------------------------------------------------------------

    private void EnsureDimmerReady()
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.gameObject.SetActive(true);
        dimmerCanvasGroup.blocksRaycasts = true;
    }

    private void ReleaseDimmerInput()
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.blocksRaycasts = false;
    }

    private void DisableDimmerHard()
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.alpha = 0f;
        dimmerCanvasGroup.blocksRaycasts = false;
    }

    private void SetDimmerAmbient(float alpha)
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.gameObject.SetActive(true);
        dimmerCanvasGroup.alpha = alpha;
        dimmerCanvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------
    // CORE FADE
    // ------------------------------------------------------------

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null)
            yield break;

        float t = 0f;
        cg.alpha = from;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        cg.alpha = to;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = null;
    }
}
