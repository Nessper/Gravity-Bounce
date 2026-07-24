using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Composant UI reutilisable pour afficher une liste de modules
/// avec support du hover, de la selection persistante
/// et d un panneau de details.
///
/// Responsabilites :
/// - instancier les ModuleItemUI
/// - gerer leur cycle de vie
/// - brancher HoverEntered / HoverExited / Clicked
/// - relier la liste au ModuleDetailsPanelUI
/// - afficher un texte fallback si la liste est vide
/// - exposer le module actuellement selectionne
/// - afficher un warning contextuel si des tiers precedents manquent
///
/// Ce composant ne connait pas la logique metier du contexte
/// (shop, ship select, inventory, etc.).
/// Il se contente d afficher une liste de ModuleDefinition.
/// </summary>
public class ModulesListPanelUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private GameObject modulesRow;
    [SerializeField] private Transform itemsRoot;
    [SerializeField] private TMP_Text noModulesText;
    [SerializeField] private ModuleDetailsPanelUI detailsPanel;

    [Header("Warning")]
    [SerializeField] private TMP_Text warningText;

    [Header("Prefabs")]
    [SerializeField] private GameObject moduleItemPrefab;

    [Header("Localization")]
    [SerializeField] private string warningPackName = "ui";
    [SerializeField] private string missingModulesWarningKey = "shop.warning.missing_modules";

    private readonly List<ModuleItemUI> spawnedItems = new List<ModuleItemUI>();
    private readonly Dictionary<ModuleItemUI, ModuleDefinition> itemToDefinition =
        new Dictionary<ModuleItemUI, ModuleDefinition>();

    private ModuleItemUI selectedItem;
    private string defaultHoverText = string.Empty;

    /// <summary>
    /// Module actuellement selectionne par clic.
    /// </summary>
    public ModuleDefinition SelectedModule { get; private set; }

    /// <summary>
    /// Emis quand la selection persistante change.
    /// </summary>
    public event Action<ModuleDefinition> OnSelectedModuleChanged;

    /// <summary>
    /// Emis a chaque clic sur un module,
    /// independamment de la selection persistante locale.
    /// </summary>
    public event Action<ModuleDefinition> OnModuleClicked;

    /// <summary>
    /// Emis lors d un double-clic sur un module.
    /// </summary>
    public event Action<ModuleDefinition> OnModuleDoubleClicked;

    /// <summary>
    /// Reconstruit integralement la liste de modules.
    ///
    /// - modules : liste a afficher
    /// - emptyText : texte fallback si liste vide
    /// - defaultHoverText : texte affiche dans le panneau details
    ///   quand rien n est selectionne ni survole
    /// </summary>
    public void ShowModules(List<ModuleDefinition> modules, string emptyText, string defaultHoverText)
    {
        Clear();

        this.defaultHoverText = defaultHoverText ?? string.Empty;

        int count = modules != null ? modules.Count : 0;
        bool hasModules = count > 0;

        if (modulesRow != null)
            modulesRow.SetActive(true);

        if (noModulesText != null)
        {
            noModulesText.gameObject.SetActive(!hasModules);

            if (!hasModules)
                noModulesText.text = emptyText ?? string.Empty;
        }

        if (!hasModules)
        {
            if (detailsPanel != null)
            {
                detailsPanel.SetDefaultText(string.Empty);
                detailsPanel.Clear();
            }

            RefreshWarning(null);
            return;
        }

        if (detailsPanel != null)
        {
            detailsPanel.Clear();
            detailsPanel.SetDefaultText(this.defaultHoverText);
            detailsPanel.ShowDefault();
        }

        RefreshWarning(null);

        for (int i = 0; i < modules.Count; i++)
            CreateItem(modules[i], i);
    }

    /// <summary>
    /// Nettoie tous les items et remet le panneau de details a zero.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            ModuleItemUI item = spawnedItems[i];
            if (item == null)
                continue;

            item.HoverEntered -= OnItemHoverEntered;
            item.HoverExited -= OnItemHoverExited;
            item.Clicked -= OnItemClicked;
            item.DoubleClicked -= OnItemDoubleClicked;
        }

        spawnedItems.Clear();
        itemToDefinition.Clear();

        selectedItem = null;
        SelectedModule = null;

        if (itemsRoot != null)
        {
            for (int i = itemsRoot.childCount - 1; i >= 0; i--)
                Destroy(itemsRoot.GetChild(i).gameObject);
        }

        if (detailsPanel != null)
        {
            detailsPanel.Clear();
            detailsPanel.SetDefaultText(defaultHoverText);
            detailsPanel.ShowDefault();
        }

        RefreshWarning(null);
        OnSelectedModuleChanged?.Invoke(null);
    }

    /// <summary>
    /// Efface explicitement la selection persistante courante.
    /// Utile apres un achat ou un reroll.
    /// </summary>
    public void ClearSelection()
    {
        if (selectedItem != null)
            selectedItem.SetSelected(false);

        selectedItem = null;
        SelectedModule = null;

        if (detailsPanel != null)
            detailsPanel.ShowDefault();

        RefreshWarning(null);
        OnSelectedModuleChanged?.Invoke(null);
    }

    /// <summary>
    /// Force la selection visuelle d un module dans la liste.
    /// Utilise par des controllers externes (ex: Ship Systems).
    /// </summary>
    public void SetSelectedModule(ModuleDefinition targetDef)
    {
        if (targetDef == null)
        {
            ClearSelection();
            return;
        }

        ModuleItemUI foundItem = null;

        foreach (var pair in itemToDefinition)
        {
            ModuleItemUI item = pair.Key;
            ModuleDefinition def = pair.Value;

            if (item == null || def == null)
                continue;

            if (def.id == targetDef.id)
            {
                foundItem = item;
                break;
            }
        }

        if (foundItem == null)
        {
            ClearSelection();
            return;
        }

        if (selectedItem != null && selectedItem != foundItem)
            selectedItem.SetSelected(false);

        selectedItem = foundItem;
        selectedItem.SetSelected(true);

        SelectedModule = targetDef;

        if (detailsPanel != null)
            detailsPanel.ShowModule(targetDef);

        RefreshWarning(targetDef);
        OnSelectedModuleChanged?.Invoke(SelectedModule);
    }

    private void CreateItem(ModuleDefinition def, int index)
    {
        if (def == null)
            return;

        if (itemsRoot == null || moduleItemPrefab == null)
        {
            Debug.LogError("[ModulesListPanelUI] itemsRoot ou moduleItemPrefab manquant.");
            return;
        }

        GameObject instance = Instantiate(moduleItemPrefab, itemsRoot);
        instance.name = "ModuleItem_" + index + "_" + def.id;

        ModuleItemUI item = instance.GetComponent<ModuleItemUI>();
        if (item == null)
        {
            Debug.LogError("[ModulesListPanelUI] Le prefab n a pas de ModuleItemUI.");
            Destroy(instance);
            return;
        }

        item.Bind(def);
        item.HoverEntered += OnItemHoverEntered;
        item.HoverExited += OnItemHoverExited;
        item.Clicked += OnItemClicked;
        item.DoubleClicked += OnItemDoubleClicked;

        spawnedItems.Add(item);
        itemToDefinition[item] = def;
    }

    private void OnItemHoverEntered(ModuleItemUI item)
    {
        if (item == null)
            return;

        if (!itemToDefinition.TryGetValue(item, out ModuleDefinition def) || def == null)
            return;

        if (detailsPanel != null)
            detailsPanel.ShowModule(def);

        RefreshWarning(def);
    }

    private void OnItemHoverExited(ModuleItemUI item)
    {
        if (SelectedModule != null)
        {
            if (detailsPanel != null)
                detailsPanel.ShowModule(SelectedModule);

            RefreshWarning(SelectedModule);
            return;
        }

        if (detailsPanel != null)
            detailsPanel.ShowDefault();

        RefreshWarning(null);
    }

    private void OnItemClicked(ModuleItemUI item)
    {
        if (item == null)
            return;

        if (!itemToDefinition.TryGetValue(item, out ModuleDefinition def) || def == null)
            return;

        OnModuleClicked?.Invoke(def);

        if (selectedItem == item)
        {
            selectedItem.SetSelected(false);
            selectedItem = null;
            SelectedModule = null;

            if (detailsPanel != null)
                detailsPanel.ShowDefault();

            RefreshWarning(null);
            OnSelectedModuleChanged?.Invoke(null);
            return;
        }

        if (selectedItem != null)
            selectedItem.SetSelected(false);

        selectedItem = item;
        selectedItem.SetSelected(true);

        SelectedModule = def;

        if (detailsPanel != null)
            detailsPanel.ShowModule(def);

        RefreshWarning(def);
        OnSelectedModuleChanged?.Invoke(SelectedModule);
    }

    private void OnItemDoubleClicked(ModuleItemUI item)
    {
        if (item == null)
            return;

        if (!itemToDefinition.TryGetValue(item, out ModuleDefinition def) || def == null)
            return;

        OnModuleDoubleClicked?.Invoke(def);
    }

    private void RefreshWarning(ModuleDefinition targetDef)
    {
        if (warningText == null)
            return;

        warningText.text = string.Empty;

        if (targetDef == null)
            return;

        List<ModuleDefinition> missingDefs = GetMissingPrerequisiteModules(targetDef);
        if (missingDefs == null || missingDefs.Count == 0)
            return;

        List<string> labels = new List<string>(missingDefs.Count);

        for (int i = 0; i < missingDefs.Count; i++)
            labels.Add(ModuleTextFormatter.BuildLocalizedModuleLabel(missingDefs[i]));

        string joinedLabels = JoinLabels(labels);

        string warningKey = missingDefs.Count > 1
            ? "shop.warning.missing_modules_plural"
            : "shop.warning.missing_modules_single";

        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            warningText.text = LocalizationManager.Instance.FormatText(
                warningPackName,
                warningKey,
                joinedLabels
            );
        }
        else
        {
            if (missingDefs.Count > 1)
                warningText.text = "Pré-requis manquants : " + joinedLabels;
            else
                warningText.text = "Pré-requis manquant : " + joinedLabels;
        }
    }

    private List<ModuleDefinition> GetMissingPrerequisiteModules(ModuleDefinition targetDef)
    {
        List<ModuleDefinition> result = new List<ModuleDefinition>();

        if (targetDef == null)
            return result;

        if (string.IsNullOrWhiteSpace(targetDef.familyId))
            return result;

        int targetTier = Mathf.Max(1, targetDef.tier);
        if (targetTier <= 1)
            return result;

        if (ModuleCatalogService.Catalog == null || ModuleCatalogService.Catalog.modules == null)
            return result;

        for (int tier = 1; tier < targetTier; tier++)
        {
            ModuleDefinition prereqDef = ModuleCatalogService.Catalog.modules.FirstOrDefault(
                m => m != null &&
                     m.familyId == targetDef.familyId &&
                     m.tier == tier
            );

            if (prereqDef == null)
                continue;

            if (!IsModuleOwned(prereqDef.id))
                result.Add(prereqDef);
        }

        return result;
    }

    private bool IsModuleOwned(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        return SaveManager.Instance.HasOwnedModule(moduleId);
    }

    private string JoinLabels(List<string> labels)
    {
        if (labels == null || labels.Count == 0)
            return string.Empty;

        return string.Join(", ", labels);
    }
}
