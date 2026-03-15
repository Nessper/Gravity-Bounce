// Chemin recommandé (projet Unity) : Scripts/Systems/Modules/ModuleCatalogService.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Charge le ModuleCatalog depuis Resources (JSON) et expose des helpers de lecture.
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
            Debug.LogError("[ModuleCatalogService] ModuleCatalog introuvable (Resources/Modules/ModuleCatalog).");
            return false;
        }

        try
        {
            Catalog = JsonUtility.FromJson<ModuleCatalog>(ta.text);
        }
        catch (Exception ex)
        {
            Debug.LogError("[ModuleCatalogService] Exception JSON: " + ex.Message);
            Catalog = null;
            return false;
        }

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
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        if (!EnsureLoaded())
            return null;

        for (int i = 0; i < Catalog.modules.Count; i++)
        {
            ModuleDefinition def = Catalog.modules[i];
            if (def != null && string.Equals(def.id, moduleId, StringComparison.Ordinal))
                return def;
        }

        return null;
    }

    public static List<string> GetFamilyIds()
    {
        List<string> result = new List<string>();

        if (!EnsureLoaded())
            return result;

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < Catalog.modules.Count; i++)
        {
            ModuleDefinition def = Catalog.modules[i];
            if (def == null || string.IsNullOrWhiteSpace(def.familyId))
                continue;

            if (seen.Add(def.familyId))
                result.Add(def.familyId);
        }

        return result;
    }

    public static List<ModuleDefinition> GetModulesByFamily(string familyId)
    {
        List<ModuleDefinition> result = new List<ModuleDefinition>();

        if (string.IsNullOrWhiteSpace(familyId))
            return result;

        if (!EnsureLoaded())
            return result;

        for (int i = 0; i < Catalog.modules.Count; i++)
        {
            ModuleDefinition def = Catalog.modules[i];
            if (def == null)
                continue;

            if (string.Equals(def.familyId, familyId, StringComparison.Ordinal))
                result.Add(def);
        }

        result.Sort((a, b) =>
        {
            int tierCompare = a.tier.CompareTo(b.tier);
            if (tierCompare != 0)
                return tierCompare;

            return string.Compare(a.id, b.id, StringComparison.Ordinal);
        });

        return result;
    }

    public static ModuleDefinition GetByFamilyAndTier(string familyId, int tier)
    {
        if (string.IsNullOrWhiteSpace(familyId))
            return null;

        if (!EnsureLoaded())
            return null;

        int clampedTier = Mathf.Max(1, tier);

        for (int i = 0; i < Catalog.modules.Count; i++)
        {
            ModuleDefinition def = Catalog.modules[i];
            if (def == null)
                continue;

            if (!string.Equals(def.familyId, familyId, StringComparison.Ordinal))
                continue;

            if (def.tier == clampedTier)
                return def;
        }

        return null;
    }

    public static List<int> GetAvailableTiersForFamily(string familyId)
    {
        List<int> result = new List<int>();

        if (string.IsNullOrWhiteSpace(familyId))
            return result;

        if (!EnsureLoaded())
            return result;

        HashSet<int> seen = new HashSet<int>();

        for (int i = 0; i < Catalog.modules.Count; i++)
        {
            ModuleDefinition def = Catalog.modules[i];
            if (def == null)
                continue;

            if (!string.Equals(def.familyId, familyId, StringComparison.Ordinal))
                continue;

            if (seen.Add(def.tier))
                result.Add(def.tier);
        }

        result.Sort();
        return result;
    }
}