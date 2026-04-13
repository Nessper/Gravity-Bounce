using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Controle l affichage du Ship Status dans le RunHub.
///
/// - Lit les donnees runtime depuis RunSessionState
/// - Met a jour hull / money / slots
/// - Affiche les modules equipes via ModulesListPanelUI
///
/// IMPORTANT :
/// - Aucun spawn manuel d items modules ici
/// - Toute la logique UI modules est deleguee a ModulesListPanelUI
/// </summary>
public class RunHubShipStatusController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("UI - Text / Values")]
    [SerializeField] private HullUI hullUI;
    [SerializeField] private TMPro.TMP_Text hullText;
    [SerializeField] private TMPro.TMP_Text durationText;
    [SerializeField] private TMPro.TMP_Text moneyText;
    [SerializeField] private TMPro.TMP_Text openSlotsText;

    [Header("Modules UI")]
    [SerializeField] private ModulesListPanelUI equippedModulesListPanel;

    private const string UiPackName = "ui";

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[RunHubShipStatusController] runSessionState non assigne.");
            enabled = false;
            return;
        }

        if (equippedModulesListPanel == null)
        {
            Debug.LogError("[RunHubShipStatusController] equippedModulesListPanel non assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();

        if (hullUI != null)
            hullUI.ResetVisualState();

        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();

        if (equippedModulesListPanel != null)
            equippedModulesListPanel.Clear();

        if (hullUI != null)
            hullUI.ResetVisualState();
    }

    private void SubscribeEvents()
    {
        runSessionState.OnShipChanged.AddListener(OnShipChanged);
        runSessionState.OnHullChanged.AddListener(OnHullChanged);
        runSessionState.OnHullMaxChanged.AddListener(OnHullMaxChanged);
        runSessionState.OnMoneyChanged.AddListener(OnMoneyChanged);
        runSessionState.OnEquipmentChanged.AddListener(OnEquipmentChanged);
    }

    private void UnsubscribeEvents()
    {
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
        runSessionState.OnHullChanged.RemoveListener(OnHullChanged);
        runSessionState.OnHullMaxChanged.RemoveListener(OnHullMaxChanged);
        runSessionState.OnMoneyChanged.RemoveListener(OnMoneyChanged);
        runSessionState.OnEquipmentChanged.RemoveListener(OnEquipmentChanged);
    }

    public void RefreshUI()
    {
        RefreshHull();
        RefreshDuration();
        RefreshMoney();
        RefreshOpenSlots();
        RefreshEquippedModules();
    }

    private void OnShipChanged(string _) => RefreshUI();
    private void OnHullChanged(int _) => RefreshHull();
    private void OnHullMaxChanged(int _) => RefreshHull();
    private void OnMoneyChanged(int _) => RefreshMoney();
    private void OnEquipmentChanged()
    {
        RefreshOpenSlots();
        RefreshEquippedModules();
    }

    // ----------------------------------------
    // HULL
    // ----------------------------------------

    private void RefreshHull()
    {
        int current = runSessionState.Hull;
        int max = runSessionState.HullMax;

        if (hullUI != null)
        {
            hullUI.SetMaxHull(max);
            hullUI.SetCurrentHull(current);
            return;
        }

        if (hullText != null)
            hullText.text = current + "/" + max;
    }

    // ----------------------------------------
    // DURATION (temporaire = baseLevelDurationSec)
    // ----------------------------------------

    private void RefreshDuration()
    {
        var ship = ShipCatalogService.GetById(runSessionState.ShipId);
        if (ship == null || durationText == null)
            return;

        durationText.text = ship.baseLevelDurationSec.ToString("0") + "s";
    }

    // ----------------------------------------
    // MONEY
    // ----------------------------------------

    private void RefreshMoney()
    {
        if (moneyText != null)
            moneyText.text = runSessionState.Money.ToString();
    }

    // ----------------------------------------
    // SLOTS
    // ----------------------------------------

    private void RefreshOpenSlots()
    {
        int open = GetOpenSlotCount();
        int total = runSessionState.EquipmentSlotCount;

        string text = open + " / " + total;

        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            text = LocalizationManager.Instance.FormatText(
                UiPackName,
                "ship_select.slots.unlocked_format",
                open,
                total
            );
        }

        if (openSlotsText != null)
            openSlotsText.text = text;
    }

    private int GetOpenSlotCount()
    {
        int count = 0;

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            if (!runSessionState.IsEquipmentSlotLocked(i))
                count++;
        }

        return count;
    }

    // ----------------------------------------
    // MODULES
    // ----------------------------------------

    private void RefreshEquippedModules()
    {
        List<ModuleDefinition> modules = new List<ModuleDefinition>();

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            if (runSessionState.IsEquipmentSlotLocked(i))
                continue;

            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            ModuleDefinition def = ResolveModuleDefinition(moduleId);
            if (def != null)
                modules.Add(def);
        }

        string emptyText = "Aucun module installe";
        string defaultHoverText = "Survolez un module pour voir les details";

        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            emptyText = LocalizationManager.Instance.GetTextOrKey(
                UiPackName,
                "ship_select.modules.none"
            );

            defaultHoverText = LocalizationManager.Instance.GetTextOrKey(
                UiPackName,
                "ship_select.modules.hover_for_details"
            );
        }

        equippedModulesListPanel.ShowModules(modules, emptyText, defaultHoverText);
    }

    private ModuleDefinition ResolveModuleDefinition(string moduleId)
    {
        if (ModuleCatalogService.Catalog == null || ModuleCatalogService.Catalog.modules == null)
            return null;

        return ModuleCatalogService.Catalog.modules.FirstOrDefault(
            m => m != null && m.id == moduleId
        );
    }
}