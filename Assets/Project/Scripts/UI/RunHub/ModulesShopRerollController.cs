using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Contrôleur dédié au reroll du shop Modules.
/// Règle V1:
/// - Reroll infini
/// - Coût = baseCost + rerollCount (rerollCount persisté dans RunStateData)
/// - Reroll => nouvelle offre (shopOfferModuleIds=null), sans refill automatique ailleurs
///
/// Audio:
/// - Si reroll OK => SfxId.ShopReroll
/// - Sinon => SfxId.ShopError
/// </summary>
public class ModulesShopRerollController : MonoBehaviour
{
    [Header("Deps")]
    [SerializeField] private ModulesHubController hub;

    [Header("Pricing")]
    [Tooltip("Coût du 1er reroll. Le coût augmente de +1 à chaque reroll.")]
    [SerializeField] private int baseRerollCost = 1;

    [Header("SFX")]
    [SerializeField] private SfxId rerollSfx = SfxId.ShopReroll;
    [SerializeField] private SfxId errorSfx = SfxId.ShopError;

    [Header("UI")]
    [Tooltip("TMP qui affiche le prix du reroll (optionnel).")]
    [SerializeField] private TMP_Text rerollPriceText;

    [Tooltip("Décalage vertical appliqué aux sprites TMP pour alignement optique.")]
    [SerializeField] private int spriteYOffset = -6;

    [Tooltip("Si true: affiche l'icône money en TMP sprite (icon_money).")]
    [SerializeField] private bool showMoneyIcon = true;

    public event Action OnRerolled;

    private void OnEnable()
    {
        RefreshPriceUI();
    }

    /// <summary>Coût actuel du reroll (base + count).</summary>
    public int CurrentCost
    {
        get
        {
            int count = (hub != null) ? hub.GetShopRerollCount() : 0;
            return Mathf.Max(0, baseRerollCost) + Mathf.Max(0, count);
        }
    }

    /// <summary>True si on a assez de money pour reroll.</summary>
    public bool CanRerollNow
    {
        get
        {
            if (hub == null || hub.RunSession == null)
                return false;

            return hub.RunSession.Money >= CurrentCost;
        }
    }

    /// <summary>
    /// APPELÉ PAR UN BOUTON UI (Inspector).
    /// </summary>
    public void OnRerollPressed()
    {
        TryReroll();
    }

    /// <summary>
    /// Tente d'effectuer un reroll:
    /// - débit money
    /// - increment rerollCount
    /// - reset offer => nouveau deal
    /// - refresh UI prix
    /// + SFX OK/ERROR
    /// </summary>
    public bool TryReroll()
    {
        if (hub == null || hub.RunSession == null)
            return false;

        int cost = CurrentCost;

        // Fast fail: pas assez de money
        if (hub.RunSession.Money < cost)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshPriceUI();
            return false;
        }

        // 1) Paye (déclenche OnMoneyChanged via RunSessionState)
        if (!hub.RunSession.TrySpendMoney(cost))
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshPriceUI();
            return false;
        }

        // 2) ++ rerollCount (persist)
        hub.IncrementShopRerollCountAndPersist();

        // 3) Invalide l’offre (persist) + event refresh
        hub.ForceRerollShopOfferAndPersist();

        // 4) Force un redeal maintenant (pas “au prochain refresh”)
        hub.TryGetShopVisibleModules(out _);

        RefreshPriceUI();

        BootRoot.Audio?.PlayUi(rerollSfx);
        OnRerolled?.Invoke();
        return true;
    }

    /// <summary>
    /// À appeler quand on entre dans un nouveau shop (ou au moment du deal initial).
    /// Reset le compteur de reroll.
    /// </summary>
    public void ResetRerollCountForNewShop()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        run.shopRerollCount = 0;
        SaveManager.Instance.Save();

        RefreshPriceUI();
    }

    // --------------------------------------------------------------------
    // UI
    // --------------------------------------------------------------------

    private void RefreshPriceUI()
    {
        if (rerollPriceText == null)
            return;

        int cost = CurrentCost;

        if (!showMoneyIcon)
        {
            rerollPriceText.text = cost.ToString();
            return;
        }

        rerollPriceText.text = $"<voffset={spriteYOffset}><sprite name=\"icon_money\"></voffset> {cost}";
    }
}