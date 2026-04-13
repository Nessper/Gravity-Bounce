using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gere la transition visuelle RunHub -> Shop avec le nouveau decoupage :
///
/// - RunHubUI : contenu interactif du hub
/// - ShopBackground : fond plein ecran du shop
/// - ShopUI : UI interactive du shop
/// - Dimmer : noir de transition puis voile d ambiance
///
/// Nouveau flow :
/// 1. Clic Next depuis RunHub
/// 2. Dimmer monte a 1 et bloque les inputs
/// 3. Pendant le noir :
///    - RunHubUI cache
///    - ShopBackground visible
///    - ShopUI cache
/// 4. Dimmer redescend a un alpha tres haut (ex: 0.92)
/// 5. Le dialogue shop se joue
/// 6. A la fin du dialogue :
///    - dimmer redescend a un alpha plus leger (ex: 0.8)
///    - l UI du shop fade in
///
/// Important :
/// - Le background shop est purement visuel
/// - Le dimmer ne bloque plus les clics une fois la transition terminee
/// - La vraie interaction du shop est portee uniquement par shopUiCanvasGroup
/// - RunHubUI est maintenant pilote via CanvasGroup pour homogeniser le comportement
/// </summary>
public class RunHubShopTransition : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup dimmerCanvasGroup;

    [Tooltip("CanvasGroup du fond plein ecran du shop.")]
    [SerializeField] private CanvasGroup shopBackgroundCanvasGroup;

    [Tooltip("CanvasGroup de l UI interactive du shop (ShopRoot).")]
    [SerializeField] private CanvasGroup shopUiCanvasGroup;

    [Header("RunHub UI")]
    [Tooltip("CanvasGroup de la racine UI du RunHub.")]
    [SerializeField] private CanvasGroup runHubUiCanvasGroup;

    [Header("Timings")]
    [SerializeField] private float dimmerFadeInSeconds = 0.20f;
    [SerializeField] private float dimmerFadeToPreDialogSeconds = 0.25f;
    [SerializeField] private float dimmerFadeAfterDialogSeconds = 0.20f;
    [SerializeField] private float shopUiFadeInSeconds = 0.20f;

    [Header("Ambient Dim")]
    [Range(0f, 1f)]
    [SerializeField] private float preDialogDimAlpha = 0.92f;

    [Range(0f, 1f)]
    [SerializeField] private float postDialogDimAlpha = 0.8f;

    [Header("Events")]
    public Action OnShopTransitionCompleted;

    private bool isRunning;
    private Coroutine revealShopUiRoutine;

    private void Awake()
    {
        RestoreRunHubState();
    }

    /// <summary>
    /// Restaure l etat visuel standard du RunHub.
    /// A utiliser au demarrage ou lors du retour depuis le shop.
    /// </summary>
    public void RestoreRunHubState()
    {
        ShowRunHubUi();
        HideShopBackground();
        HideShopUiInteractiveImmediate();
        DisableDimmerHard();

        if (revealShopUiRoutine != null)
        {
            StopCoroutine(revealShopUiRoutine);
            revealShopUiRoutine = null;
        }

        isRunning = false;
    }

    /// <summary>
    /// Lance la transition Hub -> Shop.
    /// </summary>
    public void PlayToShopTransition()
    {
        if (isRunning)
            return;

        StartCoroutine(TransitionRoutine());
    }

    /// <summary>
    /// A appeler a la fin du dialogue shop.
    /// Rend l UI du shop visible et interactive, apres un leger reveal du dimmer.
    /// </summary>
    public void ShowShopUiAfterDialog()
    {
        if (isRunning)
            return;

        if (revealShopUiRoutine != null)
            StopCoroutine(revealShopUiRoutine);

        revealShopUiRoutine = StartCoroutine(ShowShopUiAfterDialogRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        isRunning = true;

        // 1) Fade noir total + blocage input
        EnsureDimmerReady();
        yield return FadeCanvasGroup(dimmerCanvasGroup, 0f, 1f, dimmerFadeInSeconds);

        // 2) Pendant le noir :
        // - RunHub UI cache
        // - fond shop visible
        // - UI shop encore cachee
        HideRunHubUi();
        ShowShopBackground();
        HideShopUiInteractiveImmediate();

        // 3) Reveal vers un noir encore tres present avant dialogue
        yield return FadeCanvasGroup(dimmerCanvasGroup, 1f, preDialogDimAlpha, dimmerFadeToPreDialogSeconds);

        // 4) Le dimmer reste visible mais ne bloque plus les clics
        // Les clics ne doivent pas etre bloques avant le dialogue.
        if (dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = false;
        }

        isRunning = false;

        // 5) Le flow dialogue peut commencer
        OnShopTransitionCompleted?.Invoke();
    }

    private IEnumerator ShowShopUiAfterDialogRoutine()
    {
        // 1) On allege un peu le dimmer apres le dialogue
        if (dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = false;

            yield return FadeCanvasGroup(
                dimmerCanvasGroup,
                dimmerCanvasGroup.alpha,
                postDialogDimAlpha,
                dimmerFadeAfterDialogSeconds);
        }

        // 2) Puis on fade in l UI shop
        yield return FadeShopUiInteractive();

        revealShopUiRoutine = null;
    }

    // ------------------------------------------------------------
    // RunHub UI
    // ------------------------------------------------------------

    private void ShowRunHubUi()
    {
        if (runHubUiCanvasGroup == null)
            return;

        runHubUiCanvasGroup.alpha = 1f;
        runHubUiCanvasGroup.interactable = true;
        runHubUiCanvasGroup.blocksRaycasts = true;
    }

    private void HideRunHubUi()
    {
        if (runHubUiCanvasGroup == null)
            return;

        runHubUiCanvasGroup.alpha = 0f;
        runHubUiCanvasGroup.interactable = false;
        runHubUiCanvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------
    // Shop Background : visuel uniquement
    // ------------------------------------------------------------

    private void ShowShopBackground()
    {
        if (shopBackgroundCanvasGroup == null)
            return;

        shopBackgroundCanvasGroup.alpha = 1f;
        shopBackgroundCanvasGroup.interactable = false;
        shopBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void HideShopBackground()
    {
        if (shopBackgroundCanvasGroup == null)
            return;

        shopBackgroundCanvasGroup.alpha = 0f;
        shopBackgroundCanvasGroup.interactable = false;
        shopBackgroundCanvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------
    // Shop UI : visible et interactive seulement quand prete
    // ------------------------------------------------------------

    private void HideShopUiInteractiveImmediate()
    {
        if (shopUiCanvasGroup == null)
            return;

        shopUiCanvasGroup.alpha = 0f;
        shopUiCanvasGroup.interactable = false;
        shopUiCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeShopUiInteractive()
    {
        if (shopUiCanvasGroup == null)
            yield break;

        shopUiCanvasGroup.alpha = 0f;
        shopUiCanvasGroup.interactable = false;
        shopUiCanvasGroup.blocksRaycasts = false;

        yield return FadeCanvasGroup(shopUiCanvasGroup, 0f, 1f, shopUiFadeInSeconds);

        shopUiCanvasGroup.interactable = true;
        shopUiCanvasGroup.blocksRaycasts = true;
    }

    // ------------------------------------------------------------
    // Dimmer
    // ------------------------------------------------------------

    private void EnsureDimmerReady()
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.gameObject.SetActive(true);
        dimmerCanvasGroup.alpha = 0f;
        dimmerCanvasGroup.interactable = false;
        dimmerCanvasGroup.blocksRaycasts = true;
    }

    private void DisableDimmerHard()
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.alpha = 0f;
        dimmerCanvasGroup.interactable = false;
        dimmerCanvasGroup.blocksRaycasts = false;
        dimmerCanvasGroup.gameObject.SetActive(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null)
            yield break;

        if (duration <= 0f)
        {
            cg.alpha = to;
            yield break;
        }

        cg.alpha = from;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        cg.alpha = to;
    }
}