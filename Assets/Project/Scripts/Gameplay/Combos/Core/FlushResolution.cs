using System;
using System.Collections.Generic;
using UnityEngine;

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

public enum ComboOccurrenceRole
{
    Participant,
    Major,
    Minor
}

[Serializable]
public struct ComboColorRole
{
    public ComboOccurrenceRole Role;
    public string BallId;
    public int Count;

    public ComboColorRole(
        ComboOccurrenceRole role,
        string ballId,
        int count)
    {
        Role = role;
        BallId = ballId;
        Count = count;
    }
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
    public string DefinitionId;
    public ComboFamily Family;
    public ComboIntensity Intensity;
    public int BasePoints;
    public int Points;
    public float AppliedMultiplier;
    public int ChainValue;
    public string SourceBin;
    public string OccurrenceKey;
    public ComboColorRole[] ColorRoles;

    public ComboEvent(
        string id,
        ComboFamily family,
        ComboIntensity intensity,
        int points,
        string sourceBin,
        int chainValue = 0,
        string occurrenceKey = null,
        ComboColorRole[] colorRoles = null)
    {
        DefinitionId = id;
        Family = family;
        Intensity = intensity;
        BasePoints = points;
        Points = points;
        AppliedMultiplier = 1f;
        ChainValue = chainValue;
        SourceBin = sourceBin;
        OccurrenceKey = occurrenceKey;
        ColorRoles = colorRoles;
    }

    public void ApplyPointsMultiplier(float multiplier)
    {
        AppliedMultiplier = multiplier;
        Points = Mathf.RoundToInt(BasePoints * multiplier);
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
    public bool ComboMultiplierApplied { get; private set; }
    public float AppliedComboMultiplier { get; private set; } = 1f;

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

    public void ApplyComboPointsMultiplier(float multiplier)
    {
        if (ComboMultiplierApplied)
            return;

        float safeMultiplier =
            float.IsNaN(multiplier) || float.IsInfinity(multiplier)
                ? 1f
                : Mathf.Max(1f, multiplier);

        int recomputedTotal = 0;

        if (ComboEvents != null)
        {
            for (int i = 0; i < ComboEvents.Count; i++)
            {
                ComboEvent comboEvent = ComboEvents[i];
                comboEvent.ApplyPointsMultiplier(safeMultiplier);
                ComboEvents[i] = comboEvent;
                recomputedTotal += comboEvent.Points;
            }
        }

        ComboTotal = recomputedTotal;
        AppliedComboMultiplier = safeMultiplier;
        ComboMultiplierApplied = true;
    }
}
