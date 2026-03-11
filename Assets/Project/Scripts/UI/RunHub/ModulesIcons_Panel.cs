using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel qui affiche la grille d'icônes du shop Modules.
/// V0 : l'UI ne filtre plus.
/// - La sélection des modules à afficher est déléguée au hub (règles shop).
/// - La description reste vide tant que le joueur n'a pas cliqué.
/// </summary>
public class ModulesIcons_Panel : MonoBehaviour
{
    [Header("Deps")]
    [SerializeField] private ModulesHubController hub;

    [Header("UI")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ModuleIconItemView iconPrefab;

    [Header("Selection")]
    [SerializeField] private ModulesDescription_Panel descriptionPanel;

    private readonly List<ModuleIconItemView> spawned = new List<ModuleIconItemView>();

    private void OnEnable()
    {
        if (hub != null)
            hub.OnInventoryChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (hub != null)
            hub.OnInventoryChanged -= Refresh;
    }

    public void Refresh()
    {
        ClearSpawned();

        if (descriptionPanel != null)
            descriptionPanel.Clear();

        if (hub == null || contentRoot == null || iconPrefab == null)
        {
            Debug.LogError("[ModulesIcons_Panel] Références manquantes (hub/contentRoot/iconPrefab).");
            return;
        }

        // NEW: le hub renvoie la liste "shop-visible" (règles centralisées).
        if (!hub.TryGetShopVisibleModules(out List<ModuleDefinition> modules) || modules == null)
        {
            Debug.LogError("[ModulesIcons_Panel] Impossible de récupérer la liste shop-visible (hub).");
            return;
        }

        if (modules.Count == 0)
        {
            // V0: si tout est possédé, le shop est vide. (On gérera un fallback plus tard.)
            Debug.Log("[ModulesIcons_Panel] Shop vide : aucun module disponible (tout possédé ?).");
            return;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            ModuleDefinition def = modules[i];
            if (def == null)
                continue;

            ModuleIconItemView view = Instantiate(iconPrefab, contentRoot);
            view.Bind(def);

            view.OnClicked.RemoveAllListeners();
            view.OnClicked.AddListener(OnModuleClicked);

            spawned.Add(view);
        }
    }

    private void OnModuleClicked(string moduleId)
    {
        if (descriptionPanel != null)
            descriptionPanel.Show(moduleId);
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
}
