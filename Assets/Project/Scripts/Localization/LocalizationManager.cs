using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Source unique de verite pour tout le contenu localise du jeu.
///
/// Responsabilites:
/// - charger les packs localises depuis Resources/Localization/{pack}/{lang}
/// - exposer les dialogues structures (pack "dialogs")
/// - exposer les textes simples par cle (pack "ui", etc.)
/// - centraliser la langue active
/// - fournir des helpers metier pour resoudre les sequences de dialogue
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Code langue actif. Ex: 'fr', 'en'.")]
    [SerializeField] private string languageCode = "fr";

    [Tooltip("Nom des packs de textes simples a charger au demarrage.")]
    [SerializeField] private string[] textPackNames = { "ui", "ships", "modules" };

    public static LocalizationManager Instance { get; private set; }

    public bool IsReady { get; private set; }
    public string CurrentLanguageCode => languageCode;

    private DialogDatabase dialogsDatabase;
    private readonly Dictionary<string, Dictionary<string, string>> textPacks = new();
    private readonly System.Random random = new System.Random();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAll();
    }

    private void LoadAll()
    {
        IsReady = false;

        dialogsDatabase = LoadDialogsPack("dialogs", languageCode);
        if (dialogsDatabase == null || dialogsDatabase.sequences == null)
        {
            Debug.LogError("[LocalizationManager] Echec chargement pack dialogs (" + languageCode + ").");
            return;
        }

        textPacks.Clear();

        if (textPackNames != null)
        {
            for (int i = 0; i < textPackNames.Length; i++)
            {
                string packName = textPackNames[i];

                if (string.IsNullOrWhiteSpace(packName))
                    continue;

                string normalizedPackName = packName.Trim();

                Dictionary<string, string> pack = LoadTextPack(normalizedPackName, languageCode);
                if (pack == null)
                {
                    Debug.LogError("[LocalizationManager] Echec chargement pack texte '" + normalizedPackName + "' (" + languageCode + ").");
                    return;
                }

                textPacks[normalizedPackName] = pack;
            }
        }

        IsReady = true;

        int dialogsCount = dialogsDatabase.sequences != null ? dialogsDatabase.sequences.Length : 0;
        Debug.Log("[LocalizationManager] Charge. Langue=" + languageCode + ", dialogs=" + dialogsCount + ", textPacks=" + textPacks.Count);
    }

    private DialogDatabase LoadDialogsPack(string packName, string lang)
    {
        string resourcePath = BuildResourcePath(packName, lang);
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);

        if (asset == null)
        {
            Debug.LogError("[LocalizationManager] Pack dialogs introuvable: " + resourcePath);
            return null;
        }

        DialogDatabase db = JsonUtility.FromJson<DialogDatabase>(asset.text);
        if (db == null)
        {
            Debug.LogError("[LocalizationManager] Parse impossible pour pack dialogs: " + resourcePath);
            return null;
        }

        return db;
    }

    private Dictionary<string, string> LoadTextPack(string packName, string lang)
    {
        string resourcePath = BuildResourcePath(packName, lang);
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);

        if (asset == null)
        {
            Debug.LogError("[LocalizationManager] Pack texte introuvable: " + resourcePath);
            return null;
        }

        LocalizedTextDatabase db = JsonUtility.FromJson<LocalizedTextDatabase>(asset.text);
        if (db == null || db.entries == null)
        {
            Debug.LogError("[LocalizationManager] Parse impossible pour pack texte: " + resourcePath);
            return null;
        }

        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < db.entries.Length; i++)
        {
            LocalizedTextEntry entry = db.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            string key = entry.key.Trim();

            if (result.ContainsKey(key))
            {
                Debug.LogError("[LocalizationManager] Cle dupliquee dans pack '" + packName + "': " + key);
                return null;
            }

            result[key] = entry.text ?? string.Empty;
        }

        return result;
    }

    private string BuildResourcePath(string packName, string lang)
    {
        return "Localization/" + packName + "/" + lang;
    }

    /// <summary>
    /// Normalise un levelId runtime pour matcher la convention des dialogues.
    /// Exemple: W1-L2 -> W1_L2
    /// </summary>
    public string NormalizeLevelId(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            return null;

        return levelId.Trim().Replace("-", "_");
    }

    /// <summary>
    /// Construit un id de sequence de type suffixe simple.
    /// Ex: BuildSequenceId("W1-L2", "outro") -> W1_L2_outro
    /// </summary>
    public string BuildSequenceId(string levelId, string suffix)
    {
        string normalizedLevelId = NormalizeLevelId(levelId);
        if (string.IsNullOrWhiteSpace(normalizedLevelId))
            return null;

        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        string normalizedSuffix = suffix.Trim().TrimStart('_');
        return normalizedLevelId + "_" + normalizedSuffix;
    }

    /// <summary>
    /// Construit un id de sequence de phase.
    /// Ex: BuildPhaseSequenceId("W1-L2", 1) -> W1_L2_phase1
    /// </summary>
    public string BuildPhaseSequenceId(string levelId, int phaseIndex)
    {
        string normalizedLevelId = NormalizeLevelId(levelId);
        if (string.IsNullOrWhiteSpace(normalizedLevelId))
            return null;

        return normalizedLevelId + "_phase" + phaseIndex;
    }

    public DialogSequence GetSequenceById(string sequenceId)
    {
        if (!IsReady || dialogsDatabase == null || dialogsDatabase.sequences == null)
        {
            Debug.LogError("[LocalizationManager] Base dialogs non prete.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(sequenceId))
            return null;

        for (int i = 0; i < dialogsDatabase.sequences.Length; i++)
        {
            DialogSequence seq = dialogsDatabase.sequences[i];
            if (seq == null || string.IsNullOrEmpty(seq.id))
                continue;

            if (string.Equals(seq.id, sequenceId, StringComparison.OrdinalIgnoreCase))
                return seq;
        }

        return null;
    }

    public DialogSequence GetIntroSequence(string levelId)
    {
        return GetSequenceById(BuildSequenceId(levelId, "intro"));
    }

    public DialogSequence GetEvacSequence(string levelId)
    {
        return GetSequenceById(BuildSequenceId(levelId, "evac"));
    }

    public DialogSequence GetOutroSequence(string levelId)
    {
        return GetSequenceById(BuildSequenceId(levelId, "outro"));
    }

    public DialogSequence GetPhaseSequence(string levelId, int phaseIndex)
    {
        return GetSequenceById(BuildPhaseSequenceId(levelId, phaseIndex));
    }

    public DialogLine[] GetRandomVariantLines(DialogSequence sequence)
    {
        if (sequence == null || sequence.variants == null || sequence.variants.Length == 0)
            return Array.Empty<DialogLine>();

        if (sequence.variants.Length == 1)
            return sequence.variants[0].lines ?? Array.Empty<DialogLine>();

        int totalWeight = 0;
        for (int i = 0; i < sequence.variants.Length; i++)
        {
            int w = sequence.variants[i].weight;
            if (w < 1)
                w = 1;

            totalWeight += w;
        }

        int roll = random.Next(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < sequence.variants.Length; i++)
        {
            int w = sequence.variants[i].weight;
            if (w < 1)
                w = 1;

            cumulative += w;
            if (roll < cumulative)
                return sequence.variants[i].lines ?? Array.Empty<DialogLine>();
        }

        return sequence.variants[sequence.variants.Length - 1].lines ?? Array.Empty<DialogLine>();
    }

    public string GetText(string packName, string key)
    {
        if (!IsReady)
        {
            Debug.LogError("[LocalizationManager] GetText appele avant IsReady=true.");
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(packName))
        {
            Debug.LogError("[LocalizationManager] packName vide.");
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogError("[LocalizationManager] key vide.");
            return string.Empty;
        }

        string normalizedPack = packName.Trim();
        string normalizedKey = key.Trim();

        if (!textPacks.TryGetValue(normalizedPack, out Dictionary<string, string> pack))
        {
            Debug.LogError("[LocalizationManager] Pack texte inconnu: " + normalizedPack);
            return string.Empty;
        }

        if (!pack.TryGetValue(normalizedKey, out string value))
        {
            Debug.LogError("[LocalizationManager] Cle introuvable. Pack='" + normalizedPack + "', Key='" + normalizedKey + "'");
            return string.Empty;
        }

        return value;
    }

    public string GetTextOrKey(string packName, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (!IsReady)
            return key;

        if (string.IsNullOrWhiteSpace(packName))
            return key;

        string normalizedPack = packName.Trim();
        string normalizedKey = key.Trim();

        if (!textPacks.TryGetValue(normalizedPack, out Dictionary<string, string> pack))
        {
            Debug.LogWarning("[LocalizationManager] Pack texte inconnu: " + normalizedPack);
            return normalizedKey;
        }

        if (!pack.TryGetValue(normalizedKey, out string value))
        {
            Debug.LogWarning("[LocalizationManager] Cle introuvable. Pack='" + normalizedPack + "', Key='" + normalizedKey + "'");
            return normalizedKey;
        }

        return value;
    }

    public string FormatText(string packName, string key, params object[] args)
    {
        string format = GetTextOrKey(packName, key);

        try
        {
            return string.Format(format, args);
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "[LocalizationManager] FormatText error. Pack='" + packName +
                "', Key='" + key +
                "', Message='" + e.Message + "'"
            );

            return format;
        }
    }

    public bool HasText(string packName, string key)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(packName) || string.IsNullOrWhiteSpace(key))
            return false;

        string normalizedPack = packName.Trim();
        string normalizedKey = key.Trim();

        if (!textPacks.TryGetValue(normalizedPack, out Dictionary<string, string> pack))
            return false;

        return pack.ContainsKey(normalizedKey);
    }
}