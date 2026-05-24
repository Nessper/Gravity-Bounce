using System;
using System.Collections.Generic;

public enum ComboFamily
{
    Color,
    Volume,
    Timing,
    Chain,
    Danger,
    Module
}

public enum ComboIntensity
{
    Minor,
    Major,
    Epic,
    Legendary
}

[Serializable]
public struct BaseScoreItem
{
    public string BallId;
    public int Points;

    public BaseScoreItem(string ballId, int points)
    {
        BallId = ballId;
        Points = points;
    }
}

[Serializable]
public struct ComboEvent
{
    public string Id;
    public ComboFamily Family;
    public ComboIntensity Intensity;
    public int Points;
    public int ChainValue;
    public string SourceBin;

    public ComboEvent(
        string id,
        ComboFamily family,
        ComboIntensity intensity,
        int points,
        string sourceBin,
        int chainValue = 0)
    {
        Id = id;
        Family = family;
        Intensity = intensity;
        Points = points;
        ChainValue = chainValue;
        SourceBin = sourceBin;
    }
}

[Serializable]
public class FlushResolution
{
    public string BinSource;
    public BinSide BinSide;
    public bool IsFinalFlush;

    public List<BaseScoreItem> BaseItems = new();
    public int BaseTotal;

    public List<ComboEvent> ComboEvents = new();
    public int ComboTotal;

    public int FinalTotal => BaseTotal + ComboTotal;

    public bool HasCombos => ComboEvents != null && ComboEvents.Count > 0;

    public void AddBaseItem(string ballId, int points)
    {
        BaseItems.Add(new BaseScoreItem(ballId, points));
        BaseTotal += points;
    }

    public void AddCombo(ComboEvent comboEvent)
    {
        ComboEvents.Add(comboEvent);
        ComboTotal += comboEvent.Points;
    }
}