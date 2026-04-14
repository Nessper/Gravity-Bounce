using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Controle l affichage des modules equipes dans Ship Systems.
///
/// Responsabilites :
/// - lit les modules equipes depuis RunSessionState
/// - ignore les slots verrouilles
/// - delegue tout le rendu a ModulesListPanelUI
///
/// Important :
/// - aucun spawn manuel d items ici
/// - aucune logique metier d equipement ici
/// - ce script ne fait que transformer l etat runtime en liste UI
/// </summary>
public class ShipSystemsEquippedModulesController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Modules UI")]
    [SerializeField] private ModulesListPanelUI equippedModulesListPanel;

    private const string UiPackName = "ui";

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[ShipSystemsEquippedModulesController] runSessionState non assigne.");
            enabled = false;
            return;
        }

        if (equippedModulesListPanel == null)
        {
            Debug.LogError("[ShipSystemsEquippedModulesController] equippedModulesListPanel non assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();

        if (equippedModulesListPanel != null)
            equippedModulesListPanel.Clear();
    }

    private void SubscribeEvents()
    {
        runSessionState.OnShipChanged.AddListener(OnShipChanged);
        runSessionState.OnEquipmentChanged.AddListener(OnEquipmentChanged);
    }

    private void UnsubscribeEvents()
    {
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
        runSessionState.OnEquipmentChanged.RemoveListener(OnEquipmentChanged);
    }

    public void RefreshUI()
    {
        RefreshEquippedModules();
    }

    private void OnShipChanged(string _)
    {
        RefreshEquippedModules();
    }

    private void OnEquipmentChanged()
    {
        RefreshEquippedModules();
    }

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