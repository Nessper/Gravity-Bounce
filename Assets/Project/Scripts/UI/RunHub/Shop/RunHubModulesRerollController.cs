using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gere le bouton REROLL du shop modules dans le RunHub.
///
/// Responsabilites :
/// - calculer le cout courant du reroll
/// - afficher le cout sur le bouton
/// - afficher un etat visuel possible / impossible
/// - tenter le reroll via ModulesHubController
/// - jouer les SFX succes / erreur
/// - nettoyer la selection et rafraichir l affichage apres reroll
///
/// Important :
/// - la logique metier du reroll reste dans ModulesHubController
/// - ce controller ne gere que l interaction UI du bouton
/// </summary>
public class RunHubModulesRerollController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ModulesHubController modulesHub;
    [SerializeField] private ModulesListPanelUI modulesListPanel;
    [SerializeField] private RunHubModulesShopController shopController;

    [Header("UI")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollButtonText;
    [SerializeField] private CanvasGroup rerollButtonVisual;

    [Header("Costs")]
    [Tooltip("Cout du premier reroll.")]
    [SerializeField] private int baseRerollCost = 2;

    [Tooltip("Increment ajoute a chaque reroll deja effectue.")]
    [SerializeField] private int rerollCostStep = 1;

    [Header("Visual State")]
    [SerializeField] private float enabledAlpha = 1f;
    [SerializeField] private float disabledAlpha = 0.45f;

    [Header("TMP Sprites")]
    [SerializeField] private string moneySpriteName = "icon_money";
    [SerializeField] private int spriteYOffset = -6;

    [Header("SFX")]
    [SerializeField] private SfxId buySfx = SfxId.ShopBuy;
    [SerializeField] private SfxId errorSfx = SfxId.ShopError;

    private void Awake()
    {
        if (modulesHub == null)
        {
            Debug.LogError("[RunHubModulesRerollController] modulesHub non assigne.");
            enabled = false;
            return;
        }

        if (modulesListPanel == null)
        {
            Debug.LogError("[RunHubModulesRerollController] modulesListPanel non assigne.");
            enabled = false;
            return;
        }

        if (shopController == null)
        {
            Debug.LogError("[RunHubModulesRerollController] shopController non assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        modulesHub.OnModulesCollectionChanged += HandleModulesCollectionChanged;

        if (modulesHub.RunSession != null)
            modulesHub.RunSession.OnMoneyChanged.AddListener(HandleMoneyChanged);

        RefreshRerollState();
    }

    private void OnDisable()
    {
        modulesHub.OnModulesCollectionChanged -= HandleModulesCollectionChanged;

        if (modulesHub.RunSession != null)
            modulesHub.RunSession.OnMoneyChanged.RemoveListener(HandleMoneyChanged);
    }

    private void HandleModulesCollectionChanged()
    {
        RefreshRerollState();
    }

    private void HandleMoneyChanged(int _)
    {
        RefreshRerollState();
    }

    /// <summary>
    /// Appelee par le bouton REROLL.
    /// </summary>
    public void OnRerollPressed()
    {
        if (modulesHub.RunSession == null)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            return;
        }

        int cost = GetCurrentRerollCost();
        int money = Mathf.Max(0, modulesHub.RunSession.Money);

        if (money < cost)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshRerollState();
            return;
        }

        bool spent = modulesHub.RunSession.TrySpendMoney(cost);
        if (!spent)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshRerollState();
            return;
        }

        bool rerolled = modulesHub.TryRerollShop();
        if (!rerolled)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshRerollState();
            return;
        }

        BootRoot.Audio?.PlayUi(buySfx);

        modulesListPanel.ClearSelection();
        shopController.RefreshUI();
        RefreshRerollState();
    }

    private void RefreshRerollState()
    {
        int cost = GetCurrentRerollCost();
        bool affordable = false;

        if (modulesHub.RunSession != null)
            affordable = Mathf.Max(0, modulesHub.RunSession.Money) >= cost;

        SetButtonLabel(FormatLabel(cost));
        SetVisualEnabled(affordable);

        if (rerollButton != null)
            rerollButton.interactable = true;
    }

    private int GetCurrentRerollCost()
    {
        int rerollCount = modulesHub != null ? Mathf.Max(0, modulesHub.GetShopRerollCount()) : 0;
        return Mathf.Max(0, baseRerollCost + rerollCount * rerollCostStep);
    }

    private void SetVisualEnabled(bool enabledState)
    {
        if (rerollButtonVisual == null)
            return;

        rerollButtonVisual.alpha = enabledState ? enabledAlpha : disabledAlpha;
        rerollButtonVisual.interactable = true;
        rerollButtonVisual.blocksRaycasts = true;
    }

    private void SetButtonLabel(string text)
    {
        if (rerollButtonText != null)
            rerollButtonText.text = text;
    }

    private string FormatLabel(int cost)
    {
        if (string.IsNullOrEmpty(moneySpriteName))
            return cost.ToString();

        return $"<voffset={spriteYOffset}><sprite name=\"{moneySpriteName}\"></voffset> {cost}";
    }
}