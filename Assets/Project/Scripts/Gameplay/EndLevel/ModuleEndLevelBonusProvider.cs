using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produit les lignes de bonus de fin de niveau issues
/// des modules.
///
/// Pour cette première étape, on ne gère que l'impact score
/// de la famille C (Core Growth).
///
/// IMPORTANT :
/// - Ne modifie pas directement l'UI.
/// - Ne modifie pas directement le score.
/// - Ne gère pas ici les effets Hull / HullMax :
///   ces effets sont déjà traités séparément avant la cérémonie.
/// - Toute ligne ajoutée par un module doit sortir ici avec
///   un label déjà propre à afficher, préfixé par "Module ".
/// </summary>
public static class ModuleEndLevelBonusProvider
{
    public static List<EndLevelBonusEntry> Evaluate()
    {
        var results = new List<EndLevelBonusEntry>();

        ModuleRuntimeStats stats = ModuleRuntimeStats.Instance;
        if (stats == null)
            return results;

        AddCoreGrowthScoreEntry(stats, results);

        return results;
    }

    /// <summary>
    /// Injecte la ligne de bonus/malus de score du module C
    /// (Core Growth), si un module applicable existe.
    ///
    /// Règle actuelle :
    /// - le score delta de fin de niveau est toujours appliqué
    /// - l'effet +Max Hull reste géré ailleurs, avec sa propre condition
    /// </summary>
    private static void AddCoreGrowthScoreEntry(
        ModuleRuntimeStats stats,
        List<EndLevelBonusEntry> results)
    {
        if (stats == null || results == null)
            return;

        ModuleDefinition mod = stats.GetEndLevelCoreGrowthModule();
        if (mod == null)
            return;

        int delta = mod.endLevelScoreDelta;
        if (delta == 0)
            return;

        string label = BuildModuleBonusLabel(mod);

        results.Add(new EndLevelBonusEntry(
            id: mod.id,
            label: label,
            points: delta,
            source: EndLevelBonusSource.Module));
    }

    /// <summary>
    /// Construit le label affichable pour une ligne de bonus issue d'un module.
    ///
    /// Règle UI actuelle :
    /// - toutes les lignes modules commencent par "Module "
    /// - si le tier existe, on l'affiche à la fin
    ///
    /// Exemples :
    /// - "Module Core Growth T1"
    /// - "Module Core Growth T2"
    /// - "Module Scrap Booster"
    /// </summary>
    private static string BuildModuleBonusLabel(ModuleDefinition mod)
    {
        if (mod == null)
            return "Module";

        string displayName = string.IsNullOrEmpty(mod.displayName)
            ? "Unknown"
            : mod.displayName;

        int tier = Mathf.Max(0, mod.tier);

        if (tier <= 0)
            return "Module " + displayName;

        return "Module " + displayName + " T" + tier;
    }
}