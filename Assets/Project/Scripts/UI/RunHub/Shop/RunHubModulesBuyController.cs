using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gere le bouton BUY du shop modules dans le RunHub.
///
/// Responsabilites :
/// - lire le module actuellement selectionne dans ModulesListPanelUI
/// - afficher le prix du module selectionne
/// - afficher un etat visuel achetable / non achetable
/// - tenter l achat via ModulesHubController
/// - jouer les SFX succes / erreur
/// - nettoyer la selection et rafraichir l affichage apres achat
///
/// Important :
/// - la logique metier d achat reste dans ModulesHubController
/// - ce controller ne gere que l interaction UI du bouton BUY
/// </summary>
public class RunHubModulesBuyController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ModulesHubController modulesHub;
    [SerializeField] private ModulesListPanelUI modulesListPanel;
    [SerializeField] private RunHubModulesShopController shopController;

    [Header("UI")]
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private CanvasGroup buyButtonVisual;

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
            Debug.LogError("[RunHubModulesBuyController] modulesHub non assigne.");
            enabled = false;
            return;
        }

        if (modulesListPanel == null)
        {
            Debug.LogError("[RunHubModulesBuyController] modulesListPanel non assigne.");
            enabled = false;
            return;
        }

        if (shopController == null)
        {
            Debug.LogError("[RunHubModulesBuyController] shopController non assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        modulesListPanel.OnSelectedModuleChanged += HandleSelectedModuleChanged;
        modulesHub.OnModulesCollectionChanged += HandleModulesCollectionChanged;

        if (modulesHub.RunSession != null)
            modulesHub.RunSession.OnMoneyChanged.AddListener(HandleMoneyChanged);

        RefreshBuyState();
    }

    private void OnDisable()
    {
        modulesListPanel.OnSelectedModuleChanged -= HandleSelectedModuleChanged;
        modulesHub.OnModulesCollectionChanged -= HandleModulesCollectionChanged;

        if (modulesHub.RunSession != null)
            modulesHub.RunSession.OnMoneyChanged.RemoveListener(HandleMoneyChanged);
    }

    private void HandleSelectedModuleChanged(ModuleDefinition _)
    {
        RefreshBuyState();
    }

    private void HandleModulesCollectionChanged()
    {
        RefreshBuyState();
    }

    private void HandleMoneyChanged(int _)
    {
        RefreshBuyState();
    }

    /// <summary>
    /// Appelee par le bouton BUY.
    /// </summary>
    public void OnBuyPressed()
    {
        ModuleDefinition selected = modulesListPanel.SelectedModule;
        if (selected == null)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshBuyState();
            return;
        }

        if (modulesHub.RunSession == null)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            return;
        }

        int cost = Mathf.Max(0, selected.cost);
        int money = Mathf.Max(0, modulesHub.RunSession.Money);

        if (money < cost)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshBuyState();
            return;
        }

        bool bought = modulesHub.TryBuy(selected.id);
        if (!bought)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshBuyState();
            return;
        }

        BootRoot.Audio?.PlayUi(buySfx);

        modulesListPanel.ClearSelection();
        shopController.RefreshUI();
        RefreshBuyState();
    }

    private void RefreshBuyState()
    {
        ModuleDefinition selected = modulesListPanel.SelectedModule;

        if (selected == null)
        {
            SetButtonLabel(string.Empty);
            SetVisualEnabled(false);

            if (buyButton != null)
                buyButton.interactable = true;

            return;
        }

        int cost = Mathf.Max(0, selected.cost);
        int money = 0;

        if (modulesHub.RunSession != null)
            money = Mathf.Max(0, modulesHub.RunSession.Money);

        bool affordable = money >= cost;

        SetButtonLabel(FormatCost(cost));
        SetVisualEnabled(affordable);

        if (buyButton != null)
            buyButton.interactable = true;
    }

    private void SetVisualEnabled(bool enabledState)
    {
        if (buyButtonVisual == null)
            return;

        buyButtonVisual.alpha = enabledState ? enabledAlpha : disabledAlpha;
        buyButtonVisual.interactable = true;
        buyButtonVisual.blocksRaycasts = true;
    }

    private void SetButtonLabel(string text)
    {
        if (buyButtonText != null)
            buyButtonText.text = text;
    }

    private string FormatCost(int cost)
    {
        return $"<voffset=-6><sprite name=\"icon_money\"></voffset> {cost}";
    }
}