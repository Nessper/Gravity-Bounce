using System;

public enum EndResultState
{
    Victory,
    Retry,
    GameOver
}

[Serializable]
public class EndLevelSnapshot
{
    public EndLevelToken Token;
    public string LevelId;

    public EndLevelStats Stats;
    public MainObjectiveResult MainObjective;
    public SecondaryObjectiveResult[] Secondary;

    public EndResultState EndState;
    public int FinalScore;
    public EndMedal FinalMedal;

    public bool RewardsCommitted;
    public long EvaluatedTimestampUtc;
}