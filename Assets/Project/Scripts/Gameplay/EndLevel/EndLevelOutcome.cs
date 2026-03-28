using System;
using UnityEngine;

public enum EndMedal
{
    None,
    Bronze,
    Silver,
    Gold
}

[Serializable]
public struct EndLevelOutcome
{
    public bool IsVictory;
    public int FinalScore;

    public int BronzeThreshold;
    public int SilverThreshold;
    public int GoldThreshold;

    public EndMedal FinalMedal;
}