using UnityEngine;
using System.Text.RegularExpressions;

public static class ModuleTextFormatter
{
    private const string ModulesPackName = "modules";
    private const int SpriteYOffset = -6;

    public static string BuildLocalizedDetails(ModuleDefinition def)
    {
        if (def == null)
            return string.Empty;

        string name = GetLocalizedName(def);
        string desc = GetLocalizedDescription(def);

        desc = ReplaceKeywordsWithIcons(desc);

        int tier = Mathf.Max(1, def.tier);

        return $"<b>{name} {ToRoman(tier)}</b> — {desc}";
    }

    private static string GetLocalizedName(ModuleDefinition def)
    {
        if (def == null)
            return string.Empty;

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            return string.IsNullOrWhiteSpace(def.displayNameLocKey) ? def.id : def.displayNameLocKey;

        if (string.IsNullOrWhiteSpace(def.displayNameLocKey))
            return def.id;

        return LocalizationManager.Instance.GetTextOrKey(
            ModulesPackName,
            def.displayNameLocKey
        );
    }

    private static string GetLocalizedDescription(ModuleDefinition def)
    {
        if (def == null)
            return string.Empty;

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            return string.IsNullOrWhiteSpace(def.descriptionLocKey) ? string.Empty : def.descriptionLocKey;

        if (string.IsNullOrWhiteSpace(def.descriptionLocKey))
            return string.Empty;

        return LocalizationManager.Instance.GetTextOrKey(
            ModulesPackName,
            def.descriptionLocKey
        );
    }

    private static string ReplaceKeywordsWithIcons(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = ReplaceWord(text, "hull", Icon("icon_hull"));
        text = ReplaceWord(text, "coque", Icon("icon_hull"));

        text = ReplaceWord(text, "money", Icon("icon_money"));
        text = ReplaceWord(text, "credit", Icon("icon_money"));
        text = ReplaceWord(text, "credits", Icon("icon_money"));
        text = ReplaceWord(text, "crédit", Icon("icon_money"));
        text = ReplaceWord(text, "crédits", Icon("icon_money"));

        text = ReplaceWord(text, "shield", Icon("icon_shield"));
        text = ReplaceWord(text, "bouclier", Icon("icon_shield"));

        return text;
    }

    /// <summary>
    /// Remplace uniquement les mots complets (safe)
    /// </summary>
    private static string ReplaceWord(string input, string word, string replacement)
    {
        return Regex.Replace(
            input,
            $@"\b{word}\b",
            replacement,
            RegexOptions.IgnoreCase
        );
    }

    private static string Icon(string spriteName)
    {
        return $"<voffset={SpriteYOffset}><sprite name=\"{spriteName}\"></voffset>";
    }

    private static string ToRoman(int number)
    {
        switch (number)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            default: return number.ToString();
        }
    }
}