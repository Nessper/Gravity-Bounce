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
    public float Poids;
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
    public BallData[] Billes;
    public ScoreGoalsData[] ScoreGoals;
    public string Theme;
}

[System.Serializable]
public class ScoreGoalsData
{
    public string Type;
    public int Points;
}
