using UnityEngine;

[System.Serializable]
public class SpawnData
{
    public float Intervalle;
}

[System.Serializable]
public class BallData
{
    public string Type;
    public int Points;
}

// --- Phase structures ---
[System.Serializable]
public class PhaseMixEntry
{
    public string Type;   // White / Blue / Red / Black
    public float Poids;   // relative weight (0..1)
}

// --- Forced spawns (in-quota) ---
// Permet de garantir des billes particulières dans une phase,
// sans modifier le quota total de la phase (donc sans fausser la pression).
[System.Serializable]
public class ForcedSpawnEntry
{
    public string Type;     // "Red", "Black", etc.
    public int Count = 1;   // combien de fois (ex: 1)
    public float AtPercent; // 0..1 : position approximative dans la phase (ex: 0.6). <0 => auto
}



[System.Serializable]
public class PhaseData
{
    public string Name;
    public float Weight;
    public float Intervalle;
    public float AngleMin;
    public float AngleMax;
    public PhaseMixEntry[] Mix;

    // NEW: spawns forcés dans le quota de la phase.
    // Ex: garantir 1 Red à 60% de la phase.
    public ForcedSpawnEntry[] ForcedSpawns;
}



// --- Evacuation (fin de niveau, hors spawner) ---
[System.Serializable]
public class EvacuationData
{
    public string Name;        // ex: "Evacuation"
    public float DurationSec;  // ex: 10
}

// --- Main Objective ---
[System.Serializable]
public class MainObjectiveData
{
    public string Text;
    public int ThresholdCount; // valeur cible principale (interprétation selon ta logique actuelle)
    public int Bonus;          // bonus de score accordé si l'objectif principal est atteint
}

// --- Score goals (Bronze / Silver / Gold) ---
[System.Serializable]
public class ScoreGoalsData
{
    public string Type;   // "Bronze", "Silver", "Gold"
    public int Points;    // seuil de score pour chaque médaille
}

[System.Serializable]
public class SecondaryObjectiveData
{
    public string Id;         // Identifiant interne (facultatif mais utile pour debug / logs)
    public string Type;       // "BallCount", "ComboCount", etc. (interprété côté logique)
    public string TargetId;   // "Any", "White", "Black", "WhiteStreak", "SuperFlush", etc.
    public int Threshold;     // Valeur à atteindre (ex : 4 billes, 1 combo)
    public int RewardScore;   // Score attribué si l'objectif est réussi, 0 si raté
    public string UiText;     // Texte affiché dans l'UI de fin de niveau

    // NEW (optionnel) :
    // <= 0 : toutes phases
    // 1..N : uniquement pendant "PHASE 1..N"
    public int PhaseIndex;
}


// --- Root LevelData ---
[System.Serializable]
public class LevelData
{
    public string LevelID;
    public string World;
    public string Title;
    public MainObjectiveData MainObjective;
    public float LevelDurationSec;
    public int Lives;
    public SpawnData Spawn;
    public BallData[] Balls;             // matches "Balls" dans le JSON
    public ScoreGoalsData[] ScoreGoals;
    public PhaseData[] Phases;
    public ObstaclePlacement[] Obstacles;

    // Phase d'évacuation (optionnelle, hors spawner)
    public EvacuationData Evacuation;

    // Liste des objectifs secondaires pour ce niveau.
    // Peut être nulle ou vide si aucun objectif secondaire.
    public SecondaryObjectiveData[] SecondaryObjectives;
}
