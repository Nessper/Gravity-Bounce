using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Controle l affichage des modules possedes dans Ship Systems.
///
/// Responsabilites :
/// - lit l inventaire possede de la run depuis la save
/// - retire les modules deja equipes
/// - garde les modules non redondants selon la regle de progression par tiers
/// - delegue tout le rendu a ModulesListPanelUI
///
/// Sources de refresh :
/// - ModulesHubController.OnModulesCollectionChanged
///   => achat, reroll, changement de collection
/// - RunSessionState.OnEquipmentChanged
///   => equip / unequip / swap
///
/// Regle importante :
/// - on affiche les modules owned non equipes qui ne sont pas rendus redondants
///   par un tier superieur owned atteignable via une chaine complete de prerequis owned
///
/// Exemples :
/// - H1 + H2 owned -> on affiche H2
/// - H1 + H3 owned -> on affiche H1 et H3
/// - G1 + G2 owned -> on affiche G2
/// </summary>
public class ShipSystemsOwnedModulesController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private ModulesHubController modulesHub;

    [Header("UI")]
    [SerializeField] private ModulesListPanelUI ownedModulesListPanel;

    [Header("Optional")]
    [SerializeField] private TMP_Text ownedCountText;

    private const string UiPackName = "ui";

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[ShipSystemsOwnedModulesController] runSessionState non assigne.");
            enabled = false;
            return;
        }

        if (modulesHub == null)
        {
            Debug.LogError("[ShipSystemsOwnedModulesController] modulesHub non assigne.");
            enabled = false;
            return;
        }

        if (ownedModulesListPanel == null)
        {
            Debug.LogError("[ShipSystemsOwnedModulesController] ownedModulesListPanel non assigne.");
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

        if (ownedModulesListPanel != null)
            ownedModulesListPanel.Clear();
    }

    private void SubscribeEvents()
    {
        runSessionState.OnEquipmentChanged.AddListener(OnEquipmentChanged);
        runSessionState.OnShipChanged.AddListener(OnShipChanged);

        modulesHub.OnModulesCollectionChanged += OnModulesCollectionChanged;
    }

    private void UnsubscribeEvents()
    {
        runSessionState.OnEquipmentChanged.RemoveListener(OnEquipmentChanged);
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);

        modulesHub.OnModulesCollectionChanged -= OnModulesCollectionChanged;
    }

    public void RefreshUI()
    {
        RefreshOwnedModules();
    }

    private void OnEquipmentChanged()
    {
        RefreshOwnedModules();
    }

    private void OnShipChanged(string _)
    {
        RefreshOwnedModules();
    }

    private void OnModulesCollectionChanged()
    {
        RefreshOwnedModules();
    }

    private void RefreshOwnedModules()
    {
        List<ModuleDefinition> modules = BuildOwnedModulesList();

        string emptyText = "Aucun module en reserve";
        string defaultHoverText = "Survolez un module pour voir les details";

        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            emptyText = LocalizationManager.Instance.GetTextOrKey(
                UiPackName,
                "ship_systems.modules.none_available"
            );

            defaultHoverText = LocalizationManager.Instance.GetTextOrKey(
                UiPackName,
                "ship_systems.modules.hover_for_details"
            );
        }

        ownedModulesListPanel.ShowModules(modules, emptyText, defaultHoverText);
        RefreshOwnedCount(modules.Count);
    }

    private void RefreshOwnedCount(int count)
    {
        if (ownedCountText == null)
            return;

        string text = count.ToString();

        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            text = LocalizationManager.Instance.FormatText(
                UiPackName,
                "ship_systems.modules.count",
                count
            );
        }

        ownedCountText.text = text;
    }

    private List<ModuleDefinition> BuildOwnedModulesList()
    {
        List<string> ownedModuleIds = GetOwnedModuleIdsFromSave();
        if (ownedModuleIds == null || ownedModuleIds.Count == 0)
            return new List<ModuleDefinition>();

        HashSet<string> equippedIds = BuildEquippedIdsSet();

        // Toutes les defs owned, equipees ou non.
        List<ModuleDefinition> allOwnedDefs = new List<ModuleDefinition>();

        // Seulement les defs owned non equipees, candidates a l affichage.
        List<ModuleDefinition> nonEquippedOwnedDefs = new List<ModuleDefinition>();

        for (int i = 0; i < ownedModuleIds.Count; i++)
        {
            string moduleId = ownedModuleIds[i];
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            ModuleDefinition def = ResolveModuleDefinition(moduleId);
            if (def == null)
                continue;

            allOwnedDefs.Add(def);

            if (!equippedIds.Contains(def.id))
                nonEquippedOwnedDefs.Add(def);
        }

        if (nonEquippedOwnedDefs.Count == 0)
            return new List<ModuleDefinition>();

        List<ModuleDefinition> result = new List<ModuleDefinition>();

        for (int i = 0; i < nonEquippedOwnedDefs.Count; i++)
        {
            ModuleDefinition candidate = nonEquippedOwnedDefs[i];
            if (candidate == null)
                continue;

            if (!IsRedundantByReachableHigherOwnedTier(candidate, allOwnedDefs))
                result.Add(candidate);
        }

        return result
            .OrderBy(GetSortFamily)
            .ThenBy(GetTier)
            .ToList();
    }

    /// <summary>
    /// Retourne vrai si le module courant est rendu redondant
    /// par au moins un tier superieur owned atteignable via une chaine continue
    /// de prerequis owned dans la meme famille.
    /// </summary>
    private bool IsRedundantByReachableHigherOwnedTier(
        ModuleDefinition candidate,
        List<ModuleDefinition> allOwnedDefs)
    {
        if (candidate == null)
            return false;

        string familyKey = GetFamilyKey(candidate);
        if (string.IsNullOrWhiteSpace(familyKey))
            return false;

        int candidateTier = GetTier(candidate);
        if (candidateTier <= 0)
            return false;

        HashSet<int> ownedTiersInFamily = BuildOwnedTierSetForFamily(allOwnedDefs, familyKey);
        if (ownedTiersInFamily.Count == 0)
            return false;

        foreach (int targetTier in ownedTiersInFamily)
        {
            if (targetTier <= candidateTier)
                continue;

            bool fullChainOwned = true;

            for (int tier = candidateTier + 1; tier <= targetTier; tier++)
            {
                if (!ownedTiersInFamily.Contains(tier))
                {
                    fullChainOwned = false;
                    break;
                }
            }

            if (fullChainOwned)
                return true;
        }

        return false;
    }

    private HashSet<int> BuildOwnedTierSetForFamily(List<ModuleDefinition> defs, string familyKey)
    {
        HashSet<int> result = new HashSet<int>();

        if (defs == null || string.IsNullOrWhiteSpace(familyKey))
            return result;

        for (int i = 0; i < defs.Count; i++)
        {
            ModuleDefinition def = defs[i];
            if (def == null)
                continue;

            if (GetFamilyKey(def) != familyKey)
                continue;

            int tier = GetTier(def);
            if (tier > 0)
                result.Add(tier);
        }

        return result;
    }

    private HashSet<string> BuildEquippedIdsSet()
    {
        HashSet<string> equippedIds = new HashSet<string>();

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            if (runSessionState.IsEquipmentSlotLocked(i))
                continue;

            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            equippedIds.Add(moduleId);
        }

        return equippedIds;
    }

    private List<string> GetOwnedModuleIdsFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return new List<string>();

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.ownedModuleIdsInRun == null)
            return new List<string>();

        return run.ownedModuleIdsInRun;
    }

    private ModuleDefinition ResolveModuleDefinition(string moduleId)
    {
        if (ModuleCatalogService.Catalog == null || ModuleCatalogService.Catalog.modules == null)
            return null;

        return ModuleCatalogService.Catalog.modules.FirstOrDefault(
            m => m != null && m.id == moduleId
        );
    }

    private string GetFamilyKey(ModuleDefinition def)
    {
        if (def == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(def.familyId))
            return def.familyId;

        return def.id;
    }

    private string GetSortFamily(ModuleDefinition def)
    {
        if (def == null)
            return string.Empty;

        string family = GetFamilyKey(def);
        return family ?? string.Empty;
    }

    private int GetTier(ModuleDefinition def)
    {
        if (def == null)
            return 0;

        return def.tier;
    }
}