using System;
using System.Collections.Generic;

/// <summary>
/// Données du catalog de modules (data-only).
/// </summary>
[Serializable]
public class ModuleCatalog
{
    public List<ModuleDefinition> modules = new List<ModuleDefinition>();
}

[Serializable]
public class ModuleDefinition
{
    public string id;
    public string familyId;          // "A", "B", "C", "G", "H", ...
    public string displayName;
    public string description;
    public int tier;
    public int cost;
    public string iconPath;          // Resources path sans extension

    // ----------------------------------------------------
    // Famille H : sustain en fin de mission
    // ----------------------------------------------------
    public int endLevelHullRepair;   // +N Hull réparé en fin de mission
    public int endLevelMoneyGain;    // +N money gagné en fin de mission

    // ----------------------------------------------------
    // Famille S : infos de briefing
    // ----------------------------------------------------
    public int scanTierSet;          // 0 si pas SCAN, sinon 1..3

    // ----------------------------------------------------
    // Famille G : Augmentation du seuil de flush
    // ----------------------------------------------------
    public int flushMinBallsAdd;     // +N billes requises pour déclencher un flush

    // ----------------------------------------------------
    // Bonus passifs déclaratifs
    // ----------------------------------------------------
    public int hullMaxAdd;           // bonus permanent de HullMax

    // ----------------------------------------------------
    // Famille C : croissance conditionnelle du HullMax
    // ----------------------------------------------------
    public int endLevelFullHullHullMaxAdd;  // +N HullMax si Hull plein en fin de level
    public int endLevelScoreDelta;          // score appliqué en fin de level (peut être négatif)
}