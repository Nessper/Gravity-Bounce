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
    // Poids supprimé : la répartition est désormais gérée par Phases[].Mix[]
}

// --- NOUVEAU : structures pour les phases ---
[System.Serializable]
public class PhaseMixEntry
{
    public string Type;   // White / Blue / Red / Black (doit exister dans Billes[])
    public float Poids;   // poids relatif pendant la phase (0..1)
}

[System.Serializable]
public class PhaseData
{
    public string Name;        // "Intro", "Tension", "Final Rush", etc.
    public float DurationSec;  // durée de la phase en secondes
    public float Intervalle;   // intervalle de spawn spécifique à la phase
    public float AngleMin;     // angle min de tir
    public float AngleMax;     // angle max de tir
    public PhaseMixEntry[] Mix;// répartition des types sur cette phase
}

[System.Serializable]
public class LevelData
{
    public string LevelID;
    public string World;
    public string Titre;
    public int ObjectifPourcentage;
    public string Objectif;
    public string Objectif2;
    public string Objectif3;
    public string Objectif4;
    public string Objectif5;
    public float LevelDurationSec;
    public int Lives;
    public string Tip;
    public SpawnData Spawn;
    public BallData[] Billes;           // catalogue des types et leurs points
    public ScoreGoalsData[] ScoreGoals;
    public string Theme;
    public PhaseData[] Phases;          // NOUVEAU : phases du niveau
}

[System.Serializable]
public class ScoreGoalsData
{
    public string Type;  // Bronze / Silver / Gold
    public int Points;
}