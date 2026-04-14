using System.Collections;
using UnityEngine;

/// <summary>
/// Transition Shop <-> Ship Systems avec :
/// - pré fade UI sortante
/// - fade dimmer smooth
/// - apparition UI entrante avec léger scale + glow
/// </summary>
public class ShopShipSystemsTransition : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup dimmerCanvasGroup;

    [Header("Shop")]
    [SerializeField] private CanvasGroup shopBackgroundCanvasGroup;
    [SerializeField] private CanvasGroup shopUiCanvasGroup;
    [SerializeField] private RectTransform shopUiRoot;

    [Header("Ship Systems")]
    [SerializeField] private CanvasGroup shipSystemsBackgroundCanvasGroup;
    [SerializeField] private CanvasGroup shipSystemsUiCanvasGroup;
    [SerializeField] private RectTransform shipSystemsUiRoot;

    [Header("Timings")]
    [SerializeField] private float dimmerFadeInSeconds = 0.30f;
    [SerializeField] private float dimmerFadeToShipSystemsSeconds = 0.30f;
    [SerializeField] private float dimmerFadeToShopSeconds = 0.30f;

    [SerializeField] private float shopUiFadeInSeconds = 0.22f;
    [SerializeField] private float shipSystemsUiFadeInSeconds = 0.22f;

    [Header("Pre Fade")]
    [SerializeField] private float shopUiPreFadeOutSeconds = 0.10f;
    [SerializeField] private float shipSystemsUiPreFadeOutSeconds = 0.10f;

    [Header("Ambient Dim")]
    [Range(0f, 1f)]
    [SerializeField] private float shipSystemsDimAlpha = 0.30f;

    [Range(0f, 1f)]
    [SerializeField] private float shopDimAlpha = 0.80f;

    [Header("UI Punch")]
    [SerializeField] private float uiStartScale = 0.96f;
    [SerializeField] private float uiEndScale = 1.02f;
    [SerializeField] private float uiSettleScale = 1f;

    private bool isRunning;
    private Coroutine activeRoutine;

    private void Awake()
    {
        RestoreShopState(true);
    }

    // ------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------

    public void PlayToShipSystemsTransition()
    {
        if (isRunning) return;

        StopActiveRoutine();
        activeRoutine = StartCoroutine(TransitionToShipSystemsRoutine());
    }

    public void PlayBackToShopTransition()
    {
        if (isRunning) return;

        StopActiveRoutine();
        activeRoutine = StartCoroutine(TransitionBackToShopRoutine());
    }

    public void RestoreShopState(bool keepAmbientDim = true)
    {
        ShowShopBackground();
        ShowShopUiImmediate();
        HideShipSystemsBackground();
        HideShipSystemsUiImmediate();

        if (keepAmbientDim)
            SetDimmerAmbient(shopDimAlpha);
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

        // 0) Pré fade UI shop
        yield return FadeUiOut(shopUiCanvasGroup, shopUiPreFadeOutSeconds);

        // 1) Fade noir
        EnsureDimmerReady();
        yield return FadeCanvasGroup(dimmerCanvasGroup, dimmerCanvasGroup.alpha, 1f, dimmerFadeInSeconds);

        // 2) Swap
        HideShopUiImmediate();
        ShowShipSystemsBackground();
        HideShipSystemsUiImmediate();

        // 3) Dimmer ambiance ShipSystems
        yield return FadeCanvasGroup(dimmerCanvasGroup, 1f, shipSystemsDimAlpha, dimmerFadeToShipSystemsSeconds);

        ReleaseDimmerInput();

        // 4) UI ShipSystems avec punch
        yield return FadeUiWithPunch(shipSystemsUiCanvasGroup, shipSystemsUiRoot, shipSystemsUiFadeInSeconds);

        isRunning = false;
    }

    private IEnumerator TransitionBackToShopRoutine()
    {
        isRunning = true;

        // 0) Pré fade ShipSystems
        yield return FadeUiOut(shipSystemsUiCanvasGroup, shipSystemsUiPreFadeOutSeconds);

        // 1) Fade noir
        EnsureDimmerReady();
        yield return FadeCanvasGroup(dimmerCanvasGroup, dimmerCanvasGroup.alpha, 1f, dimmerFadeInSeconds);

        // 2) Swap
        HideShipSystemsUiImmediate();
        HideShipSystemsBackground();
        HideShopUiImmediate();

        // 3) Dimmer ambiance Shop
        yield return FadeCanvasGroup(dimmerCanvasGroup, 1f, shopDimAlpha, dimmerFadeToShopSeconds);

        ReleaseDimmerInput();

        // 4) UI Shop avec punch
        yield return FadeUiWithPunch(shopUiCanvasGroup, shopUiRoot, shopUiFadeInSeconds);

        isRunning = false;
    }

    // ------------------------------------------------------------
    // UI EFFECTS
    // ------------------------------------------------------------

    private IEnumerator FadeUiOut(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;

        cg.interactable = false;
        cg.blocksRaycasts = false;

        yield return FadeCanvasGroup(cg, cg.alpha, 0f, duration);
    }

    private IEnumerator FadeUiWithPunch(CanvasGroup cg, RectTransform root, float duration)
    {
        if (cg == null) yield break;

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
            k = k * k * (3f - 2f * k); // smoothstep

            cg.alpha = k;

            if (root != null)
            {
                float scale = Mathf.Lerp(uiStartScale, uiEndScale, k);
                root.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // petit settle
        if (root != null)
        {
            float settleTime = 0.08f;
            float t2 = 0f;
            float start = root.localScale.x;

            while (t2 < settleTime)
            {
                t2 += Time.unscaledDeltaTime;
                float k2 = Mathf.Clamp01(t2 / settleTime);
                root.localScale = Vector3.one * Mathf.Lerp(start, uiSettleScale, k2);
                yield return null;
            }
        }

        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    // ------------------------------------------------------------
    // BACKGROUNDS
    // ------------------------------------------------------------

    private void ShowShopBackground()
    {
        if (shopBackgroundCanvasGroup == null) return;

        shopBackgroundCanvasGroup.alpha = 1f;
        shopBackgroundCanvasGroup.interactable = false;
        shopBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void HideShopBackground()
    {
        if (shopBackgroundCanvasGroup == null) return;

        shopBackgroundCanvasGroup.alpha = 0f;
    }

    private void ShowShipSystemsBackground()
    {
        if (shipSystemsBackgroundCanvasGroup == null) return;

        shipSystemsBackgroundCanvasGroup.alpha = 1f;
    }

    private void HideShipSystemsBackground()
    {
        if (shipSystemsBackgroundCanvasGroup == null) return;

        shipSystemsBackgroundCanvasGroup.alpha = 0f;
    }

    // ------------------------------------------------------------
    // UI IMMEDIATE
    // ------------------------------------------------------------

    private void ShowShopUiImmediate()
    {
        if (shopUiCanvasGroup == null) return;

        shopUiCanvasGroup.alpha = 1f;
        shopUiCanvasGroup.interactable = true;
        shopUiCanvasGroup.blocksRaycasts = true;
    }

    private void HideShopUiImmediate()
    {
        if (shopUiCanvasGroup == null) return;

        shopUiCanvasGroup.alpha = 0f;
        shopUiCanvasGroup.interactable = false;
        shopUiCanvasGroup.blocksRaycasts = false;
    }

    private void HideShipSystemsUiImmediate()
    {
        if (shipSystemsUiCanvasGroup == null) return;

        shipSystemsUiCanvasGroup.alpha = 0f;
        shipSystemsUiCanvasGroup.interactable = false;
        shipSystemsUiCanvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------
    // DIMMER
    // ------------------------------------------------------------

    private void EnsureDimmerReady()
    {
        if (dimmerCanvasGroup == null) return;

        dimmerCanvasGroup.gameObject.SetActive(true);
        dimmerCanvasGroup.blocksRaycasts = true;
    }

    private void ReleaseDimmerInput()
    {
        if (dimmerCanvasGroup == null) return;

        dimmerCanvasGroup.blocksRaycasts = false;
    }

    private void DisableDimmerHard()
    {
        if (dimmerCanvasGroup == null) return;

        dimmerCanvasGroup.alpha = 0f;
        dimmerCanvasGroup.blocksRaycasts = false;
    }

    private void SetDimmerAmbient(float alpha)
    {
        if (dimmerCanvasGroup == null) return;

        dimmerCanvasGroup.gameObject.SetActive(true);
        dimmerCanvasGroup.alpha = alpha;
        dimmerCanvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------
    // CORE FADE
    // ------------------------------------------------------------

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        float t = 0f;
        cg.alpha = from;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k); // smoothstep
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        cg.alpha = to;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
    }
}