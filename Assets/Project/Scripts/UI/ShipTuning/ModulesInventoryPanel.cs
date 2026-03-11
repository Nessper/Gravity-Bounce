using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel d'inventaire des modules.
/// - Affiche uniquement les modules possédés.
/// - Notifie la sélection d'un module.
/// </summary>
public class ModulesInventoryPanel : MonoBehaviour
{
    [Header("Deps")]
    [SerializeField] private ModulesHubController hub;
    [SerializeField] private ModulesSelectionController selection;

    [Header("UI")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ModuleIconItemView iconPrefab;

    private readonly List<ModuleIconItemView> spawned = new List<ModuleIconItemView>();

    private void OnEnable()
    {
        if (hub != null)
        {
            hub.OnInventoryChanged += Refresh;
            hub.OnEquipmentChanged += Refresh;
        }

        if (selection != null)
            selection.OnSelectionChanged += HandleSelectionChanged;

        Refresh();

        // Sync immediat
        HandleSelectionChanged(selection != null ? selection.SelectedModuleId : null);
    }


    private void OnDisable()
    {
        if (hub != null)
        {
            hub.OnInventoryChanged -= Refresh;
            hub.OnEquipmentChanged -= Refresh;
        }

        if (selection != null)
            selection.OnSelectionChanged -= HandleSelectionChanged;
    }


    public void Refresh()
    {
        ClearSpawned();

        if (hub == null || contentRoot == null || iconPrefab == null)
        {
            Debug.LogError("[ModulesInventoryPanel] Références manquantes.");
            return;
        }

        if (!hub.TryGetAllModules(out List<ModuleDefinition> modules) || modules == null)
        {
            Debug.LogError("[ModulesInventoryPanel] Catalogue modules introuvable.");
            return;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            ModuleDefinition def = modules[i];
            if (def == null) continue;

            // Inventaire = uniquement possédés
            if (!hub.IsOwned(def.id))
                continue;

            if (hub.IsEquipped(def.id))
                continue;

            ModuleIconItemView view = Instantiate(iconPrefab, contentRoot);
            view.Bind(def);

            view.OnClicked.RemoveAllListeners();
            view.OnClicked.AddListener(OnModuleClicked);

            spawned.Add(view);
        }
        HandleSelectionChanged(selection != null ? selection.SelectedModuleId : null);
    }

    private int dbgCount;

    private void OnModuleClicked(string moduleId)
    {
        dbgCount++;
        Debug.Log($"[ModulesInventoryPanel] OnModuleClicked #{dbgCount} {moduleId} frame={Time.frameCount} panelInstance={GetInstanceID()}", this);

        if (selection != null)
            selection.Select(moduleId);
    }


    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }

    private void HandleSelectionChanged(string selectedId)
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            var view = spawned[i];
            if (view == null)
                continue;

            bool isSelected = !string.IsNullOrEmpty(selectedId) && view.ModuleId == selectedId;
            view.SetSelected(isSelected);
        }
    }

}
