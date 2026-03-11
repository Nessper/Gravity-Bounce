using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Transition RunHub -> Shop (robuste).
/// Strategie:
/// - On desactive completement le RunHub (Canvas/UI) via SetActive(false) pendant le shop
/// - On affiche le ShopPanel (fond) via CanvasGroup (alpha uniquement, pas de gating raycast)
/// - On bloque l'input uniquement avec le dimmer pendant le noir
/// - Apres les dialogues: on rend l'UI du shop interactive (blocksRaycasts=true)
///
/// Important:
/// - Le fond (ShopPanel) ne doit pas piloter les raycasts.
/// - Un seul endroit pilote l'interaction du shop: uiCanvasGroup.
/// </summary>
public class RunHubShopTransition : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup dimmerCanvasGroup;

    [Tooltip("CanvasGroup du ShopPanel (fond/shop frame).")]
    [SerializeField] private CanvasGroup shopPanelCanvasGroup;

    [Tooltip("CanvasGroup du package UI du shop (onglets, boutons).")]
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("RunHub Root")]
    [Tooltip("GO racine du RunHub UI (ex: Canvas/UI ou Canvas/UI/Principal_Panel). Sera SetActive(false) pendant le shop.")]
    [SerializeField] private GameObject runHubUiRoot;

    [Header("Timings")]
    [SerializeField] private float dimmerFadeInSeconds = 0.20f;
    [SerializeField] private float dimmerFadeOutSeconds = 0.25f;

    [Header("Events")]
    public Action OnShopPanelRevealed;

    private bool _isRunning;

    private void Awake()
    {
        ResetToRunHubState();
    }

    /// <summary>
    /// Etat RunHub garanti (utile au Start et si on revient au RunHub).
    /// </summary>
    public void ResetToRunHubState()
    {
        // RunHub visible
        if (runHubUiRoot != null)
            runHubUiRoot.SetActive(true);

        // Shop cache
        HideShopPanelVisual();
        HideShopUiInteractive();

        // Dimmer off
        DisableDimmerHard();

        _isRunning = false;
    }

    public void PlayToShopTransition()
    {
        if (_isRunning)
            return;

        StartCoroutine(TransitionRoutine());
    }

    /// <summary>
    /// A appeler apres les dialogues.
    /// Rend l'UI du shop visible + interactive.
    /// </summary>
    public void ShowUIAfterDialog()
    {
        ShowShopUiInteractive();
    }

    private IEnumerator TransitionRoutine()
    {
        _isRunning = true;

        // 1) Noir (bloque l'input pendant la transition)
        EnsureDimmerReady();
        yield return FadeCanvasGroup(dimmerCanvasGroup, 0f, 1f, dimmerFadeInSeconds);

        // 2) Pendant le noir:
        // - afficher le ShopPanel (fond) sans le rendre interactif
        // - cacher l'UI shop (toujours non interactive)
        // - couper COMPLETEMENT le RunHub (sinon les nodes volent les raycasts)
        ShowShopPanelVisual();
        HideShopUiInteractive();

        if (runHubUiRoot != null)
            runHubUiRoot.SetActive(false);

        // 3) Reveal
        yield return FadeCanvasGroup(dimmerCanvasGroup, 1f, 0f, dimmerFadeOutSeconds);
        DisableDimmerHard();

        // 4) ShopPanel visible (sans UI). On declenche les dialogues via event.
        OnShopPanelRevealed?.Invoke();

        _isRunning = false;
    }

    // ------------------------------------------------------------
    // ShopPanel (fond) : VISUEL UNIQUEMENT
    // ------------------------------------------------------------

    private void ShowShopPanelVisual()
    {
        if (shopPanelCanvasGroup == null)
            return;

        shopPanelCanvasGroup.alpha = 1f;

        // On ne touche pas blocksRaycasts ici (c'est du deco).
        // Si tu veux verrouiller a 100%: mets blocksRaycasts=false sur le ShopPanel dans la scene.
    }

    private void HideShopPanelVisual()
    {
        if (shopPanelCanvasGroup == null)
            return;

        shopPanelCanvasGroup.alpha = 0f;
    }

    // ------------------------------------------------------------
    // UI Shop : INTERACTIVE UNIQUEMENT QUAND READY
    // ------------------------------------------------------------

    private void ShowShopUiInteractive()
    {
        if (uiCanvasGroup == null)
            return;

        uiCanvasGroup.alpha = 1f;
        uiCanvasGroup.interactable = true;
        uiCanvasGroup.blocksRaycasts = true;
    }

    private void HideShopUiInteractive()
    {
        if (uiCanvasGroup == null)
            return;

        uiCanvasGroup.alpha = 0f;
        uiCanvasGroup.interactable = false;
        uiCanvasGroup.blocksRaycasts = false;
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

        // Le dimmer doit bloquer pendant le noir
        dimmerCanvasGroup.blocksRaycasts = true;
    }

    private void DisableDimmerHard()
    {
        if (dimmerCanvasGroup == null)
            return;

        dimmerCanvasGroup.alpha = 0f;
        dimmerCanvasGroup.blocksRaycasts = false;
        dimmerCanvasGroup.interactable = false;
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
