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

public static class ComboTextResolver
{
    private const string CombosPackName = "combos";
    private const string TwoColorsFormatKey =
        "combo.occurrence.two_colors.format";
    private const string ThreeColorsFormatKey =
        "combo.occurrence.three_colors.format";

    public static string ResolveDisplayName(
        ComboDefinition definition,
        string fallbackId)
    {
        string safeFallback = string.IsNullOrWhiteSpace(fallbackId)
            ? string.Empty
            : fallbackId;

        if (definition == null ||
            string.IsNullOrWhiteSpace(definition.NameLocKey) ||
            LocalizationManager.Instance == null)
        {
            return safeFallback;
        }

        string localized = LocalizationManager.Instance.GetTextOrKey(
            CombosPackName,
            definition.NameLocKey);

        if (string.IsNullOrWhiteSpace(localized) ||
            localized == definition.NameLocKey)
        {
            return safeFallback;
        }

        return localized;
    }

    public static string ResolveEventDisplayName(
        ComboDefinition definition,
        ComboEvent comboEvent)
    {
        string baseName = ResolveDisplayName(
            definition,
            comboEvent.DefinitionId);

        if (comboEvent.ColorRoles == null ||
            LocalizationManager.Instance == null)
        {
            return baseName;
        }

        if (comboEvent.ColorRoles.Length == 2)
        {
            ComboColorRole first = comboEvent.ColorRoles[0];
            ComboColorRole second = comboEvent.ColorRoles[1];

            return FormatOccurrence(
                TwoColorsFormatKey,
                baseName,
                ResolveBallName(first.BallId),
                first.Count,
                ResolveBallName(second.BallId),
                second.Count);
        }

        if (comboEvent.ColorRoles.Length == 3)
        {
            ComboColorRole first = comboEvent.ColorRoles[0];
            ComboColorRole second = comboEvent.ColorRoles[1];
            ComboColorRole third = comboEvent.ColorRoles[2];

            return FormatOccurrence(
                ThreeColorsFormatKey,
                baseName,
                ResolveBallName(first.BallId),
                first.Count,
                ResolveBallName(second.BallId),
                second.Count,
                ResolveBallName(third.BallId),
                third.Count);
        }

        return baseName;
    }

    private static string ResolveBallName(string ballId)
    {
        if (string.IsNullOrWhiteSpace(ballId) ||
            LocalizationManager.Instance == null)
        {
            return ballId ?? string.Empty;
        }

        string key = "combo.color." + ballId.ToLowerInvariant();
        string localized = LocalizationManager.Instance.GetTextOrKey(
            CombosPackName,
            key);

        return localized == key ? ballId : localized;
    }

    private static string FormatOccurrence(
        string formatKey,
        string fallback,
        params object[] args)
    {
        string format = LocalizationManager.Instance.GetTextOrKey(
            CombosPackName,
            formatKey);

        if (format == formatKey)
            return fallback;

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
