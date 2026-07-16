using System;
using System.Collections.Generic;

/// <summary>
/// Données du catalog de modules (data-only).
/// Les textes sont localisés via des clés.
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
    public string familyId;              // "A", "B", "C", "G", "H", ...

    // -----------------------------
    // Localization
    // -----------------------------
    public string displayNameLocKey;     // ex: "module.hull_patch.name"
    public string descriptionLocKey;     // ex: "module.hull_patch.t3.description"

    // -----------------------------
    // Core data
    // -----------------------------
    public int tier;
    public int cost;
    public string iconPath;              // Resources path sans extension


    // ----------------------------------------------------
    // Famille A : filtre noir pendant la mission
    // ----------------------------------------------------
    public int blackFilterChargesPerMission; // nombre de noires filtrées par mission

    // ----------------------------------------------------
    // Famille B : upgrade des billes blanches au flush
    // ----------------------------------------------------
    public int flushWhiteToBlueCount;        // convertit N blanches en bleues au flush
    public int flushWhiteToRedCount;         // convertit N blanches en rouges au flush

    // ----------------------------------------------------
    // Famille C : croissance conditionnelle du HullMax
    // ----------------------------------------------------
    public int endLevelFullHullHullMaxAdd;  // +N HullMax si Hull plein en fin de level
    public int endLevelScoreDelta;          // score appliqué en fin de level (peut être négatif)

    // ----------------------------------------------------
    // Famille E : bonus de durée de mission
    // ----------------------------------------------------
    public float levelDurationBonusSec;

    // ----------------------------------------------------
    // Famille F : bonus money selon médaille de fin
    // ----------------------------------------------------
    public int medalBronzeMoney;
    public int medalSilverMoney;
    public int medalGoldMoney;

    // ----------------------------------------------------
    // Famille G : Augmentation du seuil de flush
    // ----------------------------------------------------
    public int flushMinBallsAdd;         // +N billes requises pour déclencher un flush

    // ----------------------------------------------------
    // Famille H : sustain en fin de mission
    // ----------------------------------------------------
    public int endLevelHullRepair;       // +N Hull réparé en fin de mission
    public int endLevelMoneyGain;        // +N money gagné en fin de mission

    // ----------------------------------------------------
    // Famille S : infos de briefing
    // ----------------------------------------------------
    public int scanTierSet;              // 0 si pas SCAN, sinon 1..3

    // ----------------------------------------------------
    // Famille J : combos mixtes
    // ----------------------------------------------------
    public int jComboTierSet;

    // ----------------------------------------------------
    // Famille I : multiplicateur global des points de combos
    // ----------------------------------------------------
    public float comboPointsMultiplierSet;

    // ----------------------------------------------------
    // Famille K0 : controle transversal des drones
    // ----------------------------------------------------
    public bool dronesStartCharged;
    public float droneCooldownMultiplier;

    // ----------------------------------------------------
    // Famille K1 : drone anti-noire
    // ----------------------------------------------------
    public float k1CooldownSec;

    // ----------------------------------------------------
    // Famille K2 : drone Interceptor
    // ----------------------------------------------------
    public int k2TierSet;
    public float k2CooldownSec;



    // ----------------------------------------------------
    // Bonus passifs déclaratifs
    // ----------------------------------------------------
    public int hullMaxAdd;               // bonus permanent de HullMax


}
