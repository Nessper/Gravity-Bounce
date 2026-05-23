using System;
using UnityEngine;

public enum ComboBonusMode
{
    Flat,
    PercentOfColorPoints,
    PercentOfPositivePoints
}

public enum ComboOverlayStyle
{
    Standard,
    Chain,
    Timing,
    Danger,
    Module
}

[Serializable]
public class ComboDefinition
{
    public string Id;

    public ComboFamily Family;
    public ComboIntensity Intensity;

    [Header("Localization")]
    public string NameLocKey;
    public string DescriptionLocKey;

    [Header("Bonus")]
    public ComboBonusMode BonusMode;
    public float BonusValue;

    [Header("Presentation")]
    public Color UiColor = Color.white;
    public ComboOverlayStyle OverlayStyle = ComboOverlayStyle.Standard;

    [Header("Timing")]
    public float TimingWindowSec = 0f;

    [Header("Chain Scaling")]
    public int Threshold = 0;
    public int MaxLevel = 0;
    public float BonusPerLevel = 0f;

    public int ComputeFlatBonus()
    {
        if (BonusMode != ComboBonusMode.Flat)
            return 0;

        return Mathf.RoundToInt(BonusValue);
    }

    public int ComputePercentBonus(int sourcePoints)
    {
        if (BonusMode != ComboBonusMode.PercentOfColorPoints &&
            BonusMode != ComboBonusMode.PercentOfPositivePoints)
            return 0;

        if (sourcePoints <= 0)
            return 0;

        return Mathf.RoundToInt(sourcePoints * BonusValue);
    }

    public int ComputeScaledPercentBonus(
    int sourcePoints,
    int level)
    {
        if (BonusMode != ComboBonusMode.PercentOfColorPoints &&
            BonusMode != ComboBonusMode.PercentOfPositivePoints)
            return 0;

        if (sourcePoints <= 0)
            return 0;

        int safeLevel = level;

        if (MaxLevel > 0)
            safeLevel = Mathf.Min(safeLevel, MaxLevel);

        float finalBonusValue =
            BonusValue + (BonusPerLevel * Mathf.Max(0, safeLevel - 1));

        return Mathf.RoundToInt(sourcePoints * finalBonusValue);
    }
}