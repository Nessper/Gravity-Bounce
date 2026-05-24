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
    public string BallId;
    public float Poids;
}

// --- Forced spawns (in-quota) ---
[System.Serializable]
public class ForcedSpawnEntry
{
    public string BallId;
    public int Count = 1;
    public float AtPercent;
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
    public ForcedSpawnEntry[] ForcedSpawns;
}

// --- Evacuation (fin de niveau, hors spawner) ---
[System.Serializable]
public class EvacuationData
{
    public string Name;
    public float DurationSec;
}

// --- Main Objective ---
[System.Serializable]
public class MainObjectiveData
{
    public string Text;
    public int ThresholdCount;
    public int Bonus;
}

// --- Score goals (Bronze / Silver / Gold) ---
[System.Serializable]
public class ScoreGoalsData
{
    public string Type;
    public int Points;
}

[System.Serializable]
public class SecondaryObjectiveData
{
    public string Id;
    public string Type;
    public string TargetId;
    public int Threshold;
    public int RewardScore;
    public string UiText;
    public int PhaseIndex;
}

[System.Serializable]
public class ScanTextData
{
    public string T1;
    public string T2;
    public string T3;
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
    public BallData[] Balls;
    public ScoreGoalsData[] ScoreGoals;
    public PhaseData[] Phases;
    public ObstaclePlacement[] Obstacles;

    // Phase d evacuation (optionnelle, hors spawner)
    public EvacuationData Evacuation;

    // Liste des objectifs secondaires pour ce niveau.
    public SecondaryObjectiveData[] SecondaryObjectives;

    // Textes de scan selon le tier du module.
    public ScanTextData ScanText;
}