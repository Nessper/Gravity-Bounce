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

[System.Serializable]
public class PhaseData
{
    public string Name;
    public float Weight;      // nouvelle source de vérité pour répartir le temps total
    public float Intervalle;
    public float AngleMin;
    public float AngleMax;
    public PhaseMixEntry[] Mix;
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
    public string Tip;
    public SpawnData Spawn;
    public BallData[] Balls;             // matches "Balls" dans le JSON
    public ScoreGoalsData[] ScoreGoals;
    public string Theme;
    public PhaseData[] Phases;
    public ObstaclePlacement[] Obstacles;

    // Phase d'évacuation (optionnelle, hors spawner)
    public EvacuationData Evacuation;

    // Liste des objectifs secondaires pour ce niveau.
    // Peut être nulle ou vide si aucun objectif secondaire.
    public SecondaryObjectiveData[] SecondaryObjectives;
}
