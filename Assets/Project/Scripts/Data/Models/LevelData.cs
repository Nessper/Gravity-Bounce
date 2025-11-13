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
    public int ThresholdCount; // 0–100 (e.g., 50 for 50%)
    public int Bonus;
}

// --- Score goals (Bronze / Silver / Gold) ---
[System.Serializable]
public class ScoreGoalsData
{
    public string Type;
    public int Points;
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
    public BallData[] Balls;             // matches "Billes" -> renamed to "Balls" for JSON
    public ScoreGoalsData[] ScoreGoals;
    public string Theme;
    public PhaseData[] Phases;

    // Phase d'évacuation (optionnelle, hors spawner)
    public EvacuationData Evacuation;
}
