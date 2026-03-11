using System.Collections.Generic;

[System.Serializable]
public class EndLevelStats
{
    // Stats brutes (pour ton panneau rouge)
    public int TimeElapsedSec;   // ex: 60
    public int BallsCollected;   // ex: 72
    public int BallsLost;        // ex: 13

    // Scores
    public int RawScore;         // "Score" (score brut)
    public int FinalScore;       // si tu veux l'afficher direct (peut = RawScore pour l’instant)

    // Achievements (panneau jaune) — facultatif pour l’instant
    [System.Serializable] public struct GoalLine { public string Label; public int Points; }
    public List<GoalLine> Goals = new();

    // Combos finaux (panneau vert) — peut être vide
    [System.Serializable] public struct ComboCalc { public string Label; public int Base; public float Mult; public int Total; }
    public List<ComboCalc> Combos = new();
}
