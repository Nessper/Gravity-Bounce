using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel détails module.
/// - Suit automatiquement la sélection (ModulesSelectionController).
/// - Affiche: header, description, bouton BUY (si non possédé) + statut équipable (info).
///
/// BUY (V2):
/// - Bouton reste cliquable même si l'achat est impossible.
/// - Si achat OK: joue ShopBuy.
/// - Si achat impossible: joue ShopError.
/// - Visuel: bouton "grisé" si pas assez d'argent (mais cliquable).
/// </summary>
public class ModulesDescription_Panel : MonoBehaviour
{
    private const string ModulesPackName = "modules";

    [Header("Deps")]
    [SerializeField] private ModulesHubController hub;

    [Header("Selection")]
    [SerializeField] private ModulesSelectionController selection;

    [Header("UI")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Mode")]
    [SerializeField] private bool allowBuy = true;

    [Header("Buy")]
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    [Tooltip("Optionnel: Image du bouton (fond). Si null, on ne teinte pas le fond.")]
    [SerializeField] private Image buyButtonBackground;

    [Header("Buy SFX")]
    [SerializeField] private SfxId buySfx = SfxId.ShopBuy;
    [SerializeField] private SfxId errorSfx = SfxId.ShopError;

    [Header("Buy Visual")]
    [Tooltip("Couleur du texte quand l'achat est possible.")]
    [SerializeField] private Color buyTextColorAffordable = Color.white;

    [Tooltip("Couleur du texte quand l'achat est impossible (bouton grisé mais cliquable).")]
    [SerializeField] private Color buyTextColorUnaffordable = new Color(1f, 1f, 1f, 0.5f);

    [Tooltip("Couleur du fond quand l'achat est possible.")]
    [SerializeField] private Color buyBgColorAffordable = Color.white;

    [Tooltip("Couleur du fond quand l'achat est impossible (bouton grisé mais cliquable).")]
    [SerializeField] private Color buyBgColorUnaffordable = new Color(1f, 1f, 1f, 0.5f);

    [Header("Equip Status")]
    [SerializeField] private TMP_Text equipStatusText;

    [Header("TMP Sprites")]
    [Tooltip("Décalage vertical appliqué aux sprites TMP pour alignement optique.")]
    [SerializeField] private int spriteYOffset = -6;

    private string currentModuleId;
    private int currentCost;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (selection != null)
            selection.OnSelectionChanged += HandleSelectionChanged;

        if (hub != null && hub.RunSession != null)
            hub.RunSession.OnMoneyChanged.AddListener(HandleMoneyChanged);

        HandleSelectionChanged(selection != null ? selection.SelectedModuleId : null);
    }

    private void OnDisable()
    {
        if (selection != null)
            selection.OnSelectionChanged -= HandleSelectionChanged;

        if (hub != null && hub.RunSession != null)
            hub.RunSession.OnMoneyChanged.RemoveListener(HandleMoneyChanged);
    }

    private void HandleMoneyChanged(int _)
    {
        RefreshBuyVisualState();
    }

    private void HandleSelectionChanged(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            Clear();
        else
            Show(moduleId);
    }

    public void Clear()
    {
        currentModuleId = null;
        currentCost = 0;

        if (headerText != null) headerText.text = "";
        if (descriptionText != null) descriptionText.text = "";
        if (equipStatusText != null) equipStatusText.text = "";

        if (buyButton != null)
            buyButton.gameObject.SetActive(false);
    }

    public void Show(string moduleId)
    {
        if (hub == null)
        {
            Debug.LogError("[ModulesDescription_Panel] hub non assigné.");
            return;
        }

        if (!hub.TryGetModuleById(moduleId, out ModuleDefinition def) || def == null)
        {
            Debug.LogWarning("[ModulesDescription_Panel] Module introuvable: " + moduleId);
            return;
        }

        currentModuleId = moduleId;
        currentCost = Mathf.Max(0, def.cost);

        bool isOwned = hub.IsOwned(def.id);

        if (headerText != null)
        {
            string name = GetLocalizedModuleName(def);
            string romanTier = ToRoman(Mathf.Max(1, def.tier));
            headerText.text = $"{name} {romanTier}";
        }

        if (descriptionText != null)
            descriptionText.text = GetLocalizedModuleDescription(def);

        bool showBuy = allowBuy && !isOwned;

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(showBuy);
            buyButton.interactable = true;
        }

        if (buyButtonText != null)
            buyButtonText.text = FormatMoney(currentCost);

        if (equipStatusText != null)
        {
            equipStatusText.text = "";
            equipStatusText.color = Color.white;
        }

        if (showBuy)
        {
            if (hub.RunSession != null && equipStatusText != null)
            {
                bool ok = hub.RunSession.TryGetMissingTierPrerequisite(def.id, out string missingId);

                if (!ok && !string.IsNullOrEmpty(missingId))
                {
                    string prereqName = BuildPrerequisiteLabel(missingId);

                    equipStatusText.text =
                        "Attention ! Tu peux l'acheter, mais tu ne pourras pas l'equiper tant que tu n'as pas " + prereqName + ".";
                    equipStatusText.color = Color.yellow;
                }
            }

            RefreshBuyVisualState();
            return;
        }

        if (!allowBuy && isOwned && hub.RunSession != null && equipStatusText != null)
        {
            bool explained = hub.RunSession.TryExplainEquipFailure(
                def.id,
                0,
                out RunSessionState.EquipFailReason reason,
                out string missingPrereqId);

            if (reason != RunSessionState.EquipFailReason.None)
            {
                if (reason == RunSessionState.EquipFailReason.MissingPrerequisite)
                {
                    string prereqName = BuildPrerequisiteLabel(missingPrereqId);

                    equipStatusText.text =
                        "Impossible d'equiper: il te manque d'abord " + prereqName + ".";
                    equipStatusText.color = Color.red;
                }
                else if (reason == RunSessionState.EquipFailReason.SlotLocked)
                {
                    equipStatusText.text = "Slot verrouille.";
                    equipStatusText.color = Color.red;
                }
                else if (reason == RunSessionState.EquipFailReason.NotOwned)
                {
                    equipStatusText.text = "Module non possede.";
                    equipStatusText.color = Color.red;
                }
                else
                {
                    equipStatusText.text = "Impossible d'equiper ce module.";
                    equipStatusText.color = Color.red;
                }
            }
        }
    }

    private void RefreshBuyVisualState()
    {
        if (buyButton == null || !buyButton.gameObject.activeInHierarchy)
            return;

        if (hub == null || hub.RunSession == null)
            return;

        int money = Mathf.Max(0, hub.RunSession.Money);
        bool affordable = money >= Mathf.Max(0, currentCost);

        if (buyButtonText != null)
            buyButtonText.color = affordable ? buyTextColorAffordable : buyTextColorUnaffordable;

        if (buyButtonBackground != null)
            buyButtonBackground.color = affordable ? buyBgColorAffordable : buyBgColorUnaffordable;
    }

    public void OnBuyPressed()
    {
        if (hub == null || hub.RunSession == null)
            return;

        string moduleId = currentModuleId;
        if (string.IsNullOrEmpty(moduleId))
            return;

        if (hub.RunSession.Money < Mathf.Max(0, currentCost))
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshBuyVisualState();
            return;
        }

        bool bought = hub.TryBuy(moduleId);
        if (!bought)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            RefreshBuyVisualState();
            return;
        }

        BootRoot.Audio?.PlayUi(buySfx);

        Show(moduleId);
    }

    private string FormatMoney(int amount)
    {
        return $"<voffset={spriteYOffset}><sprite name=\"icon_money\"></voffset> {amount}";
    }

    public void ShowEquipFailure(string moduleId, int slotIndex)
    {
        if (equipStatusText == null)
            return;

        equipStatusText.text = "";
        equipStatusText.color = Color.white;

        if (hub == null || hub.RunSession == null)
            return;

        RunSessionState run = hub.RunSession;

        bool explained = run.TryExplainEquipFailure(moduleId, slotIndex, out RunSessionState.EquipFailReason reason, out string missingPrereqId);
        if (!explained)
            return;

        if (reason == RunSessionState.EquipFailReason.None)
            return;

        if (reason == RunSessionState.EquipFailReason.SlotLocked)
        {
            equipStatusText.text = "Slot verrouille.";
            equipStatusText.color = Color.red;
            return;
        }

        if (reason == RunSessionState.EquipFailReason.NotOwned)
        {
            equipStatusText.text = "Module non possede.";
            equipStatusText.color = Color.red;
            return;
        }

        if (reason == RunSessionState.EquipFailReason.MissingPrerequisite)
        {
            string prereqName = BuildPrerequisiteLabel(missingPrereqId);

            equipStatusText.text = "Impossible d'equiper: il te manque d'abord " + prereqName + ".";
            equipStatusText.color = Color.red;
            return;
        }

        equipStatusText.text = "Impossible d'equiper ce module.";
        equipStatusText.color = Color.red;
    }

    private string BuildPrerequisiteLabel(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return "un module requis";

        if (!hub.TryGetModuleById(moduleId, out ModuleDefinition prereqDef) || prereqDef == null)
            return moduleId;

        string name = GetLocalizedModuleName(prereqDef);
        string romanTier = ToRoman(Mathf.Max(1, prereqDef.tier));

        return $"{name} {romanTier}";
    }

    private string GetLocalizedModuleName(ModuleDefinition def)
    {
        if (def == null)
            return "Unknown";

        if (string.IsNullOrWhiteSpace(def.displayNameLocKey))
            return def.id;

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            return def.displayNameLocKey;

        return LocalizationManager.Instance.GetTextOrKey(ModulesPackName, def.displayNameLocKey);
    }

    private string GetLocalizedModuleDescription(ModuleDefinition def)
    {
        if (def == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(def.descriptionLocKey))
            return string.Empty;

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            return def.descriptionLocKey;

        return LocalizationManager.Instance.GetTextOrKey(ModulesPackName, def.descriptionLocKey);
    }

    private string ToRoman(int value)
    {
        switch (value)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            default: return value.ToString();
        }
    }
}