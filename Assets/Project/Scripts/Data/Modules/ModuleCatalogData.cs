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
    public string familyId;         // "HULL", "SCAN", "GREED", ...
    public string displayName;
    public string description;
    public int tier;
    public int cost;
    public string iconPath;         // Resources path sans extension

    // --- Famille HULL ---
    public int hullMaxAdd;          // +Max Hull

    // --- Famille SCAN ---
    public int scanTierSet;         // 0 si pas SCAN, sinon 1..3

    // --- Famille GREED ---
    public int flushMinBallsAdd;    // +N billes requises pour déclencher un flush
}
