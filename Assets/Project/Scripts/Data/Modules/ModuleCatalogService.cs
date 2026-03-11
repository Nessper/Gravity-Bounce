using System;
using UnityEngine;

/// <summary>
/// Charge le ModuleCatalog depuis Resources (JSON).
/// </summary>
public static class ModuleCatalogService
{
    public static ModuleCatalog Catalog { get; private set; }

    public static bool EnsureLoaded()
    {
        if (Catalog != null && Catalog.modules != null && Catalog.modules.Count > 0)
            return true;

        TextAsset ta = Resources.Load<TextAsset>("Modules/ModuleCatalog");
        if (ta == null)
        {
            Debug.LogError("[ModuleCatalogService] ModuleCatalog introuvable (Resources/Modules/ModuleCatalog.json).");
            return false;
        }

        Catalog = JsonUtility.FromJson<ModuleCatalog>(ta.text);
        if (Catalog == null || Catalog.modules == null)
        {
            Debug.LogError("[ModuleCatalogService] JSON invalide (Catalog null).");
            return false;
        }

        Debug.Log("[ModuleCatalogService] Catalog chargé: " + Catalog.modules.Count + " modules");
        return true;
    }

    public static ModuleDefinition GetById(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return null;

        if (!EnsureLoaded())
            return null;

        // Recherche linéaire OK (catalog petit). Optimisable plus tard si besoin.
        for (int i = 0; i < Catalog.modules.Count; i++)
        {
            ModuleDefinition def = Catalog.modules[i];
            if (def != null && string.Equals(def.id, moduleId, StringComparison.Ordinal))
                return def;
        }

        return null;
    }

}
