using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Charge la base de dialogues depuis StreamingAssets et fournit des methodes
/// pour recuperer les sequences et les lignes adaptees au contexte.
///
/// Ce manager vit dans BootRoot (DontDestroyOnLoad).
///
/// Convention d'IDs:
/// - intro:  {levelId}_intro
/// - phases: {levelId}_phase{index}
/// - evac:   {levelId}_evac
/// - outro:  {levelId}_outro
///
/// levelId runtime accepte: "W1-L2" ou "W1_L2".
/// La normalisation remplace '-' par '_'.
/// </summary>
public class DialogManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Code langue utilise pour charger le fichier JSON (ex: 'fr', 'en').")]
    [SerializeField] private string languageCode = "fr";

    [Tooltip("Dossier relatif dans StreamingAssets ou se trouve le fichier dialogs_XX.json.")]
    [SerializeField] private string dialogsFolder = "Dialogs";

    [Header("Debug")]
    [Tooltip("Si actif, lance un test simple au demarrage pour verifier le chargement de l'intro W1_L1.")]
    [SerializeField] private bool runSelfTestOnStart = false;

    /// <summary>Base de donnees des dialogues chargee depuis le JSON.</summary>
    public DialogDatabase Database { get; private set; }

    /// <summary>Indique si la base de donnees est prete a etre utilisee.</summary>
    public bool IsReady { get; private set; }

    private readonly System.Random random = new System.Random();

    private void Awake()
    {
        StartCoroutine(LoadDatabaseCoroutine());
    }

    private void Start()
    {
        if (runSelfTestOnStart)
            StartCoroutine(SelfTestIntroSequence_LevelId());
    }

    /// <summary>
    /// Charge le fichier JSON dialogs_[languageCode].json depuis StreamingAssets
    /// de maniere compatible avec toutes les plateformes (y compris Android).
    /// </summary>
    private IEnumerator LoadDatabaseCoroutine()
    {
        IsReady = false;

        string fileName = "dialogs_" + languageCode + ".json";
        string fullPath = Path.Combine(Application.streamingAssetsPath, dialogsFolder, fileName);

        string uri = fullPath;

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_WEBGL)
        if (!fullPath.StartsWith("jar:") && !fullPath.StartsWith("http"))
            uri = "file://" + fullPath;
#else
        if (!fullPath.StartsWith("file://"))
            uri = "file://" + fullPath;
#endif

        using (UnityWebRequest request = UnityWebRequest.Get(uri))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isHttpError || request.isNetworkError)
#endif
            {
                Debug.LogError("DialogManager: erreur chargement dialogues: " + request.error + " (" + fullPath + ")");
                yield break;
            }

            string jsonText = request.downloadHandler.text;
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("DialogManager: fichier dialogues vide ou introuvable: " + fullPath);
                yield break;
            }

            Database = JsonUtility.FromJson<DialogDatabase>(jsonText);
            if (Database == null || Database.sequences == null)
            {
                Debug.LogError("DialogManager: impossible de parser la base: " + fullPath);
                yield break;
            }
        }

        IsReady = true;
        Debug.Log("DialogManager: base chargee (" + languageCode + "), " + Database.sequences.Length + " sequences.");
    }

    /// <summary>
    /// Normalise un levelId runtime pour matcher la convention de cle des dialogues.
    /// Exemple: "W1-L2" devient "W1_L2".
    /// </summary>
    private string NormalizeLevelId(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return levelId;

        return levelId.Trim().Replace("-", "_");
    }

    /// <summary>
    /// Construit un id de sequence a partir du levelId et du kind.
    /// kind attendu: "intro", "evac", "outro", "phase".
    /// </summary>
    public string BuildSequenceId(string levelId, string kind, int? phaseIndex = null)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(kind))
            return null;

        string normalized = NormalizeLevelId(levelId);
        string k = kind.Trim().ToLowerInvariant();

        if (k == "phase")
        {
            if (!phaseIndex.HasValue)
                return null;

            return normalized + "_phase" + phaseIndex.Value;
        }

        return normalized + "_" + k;
    }

    /// <summary>
    /// Acces direct par identifiant unique (field "id" dans le JSON).
    /// Retourne null si aucune sequence ne correspond.
    /// </summary>
    public DialogSequence GetSequenceById(string sequenceId)
    {
        if (!IsReady || Database == null || Database.sequences == null)
        {
            Debug.LogWarning("DialogManager: base non prete, GetSequenceById echoue.");
            return null;
        }

        if (string.IsNullOrEmpty(sequenceId))
            return null;

        for (int i = 0; i < Database.sequences.Length; i++)
        {
            DialogSequence seq = Database.sequences[i];
            if (seq == null || string.IsNullOrEmpty(seq.id))
                continue;

            if (string.Equals(seq.id, sequenceId, StringComparison.OrdinalIgnoreCase))
                return seq;
        }

        return null;
    }

    /// <summary>
    /// Recupere une sequence par levelId + kind (intro/evac/outro/phase).
    /// Pour "phase", fournir phaseIndex.
    /// </summary>
    public DialogSequence GetSequence(string levelId, string kind, int? phaseIndex = null)
    {
        string id = BuildSequenceId(levelId, kind, phaseIndex);
        if (string.IsNullOrEmpty(id))
            return null;

        return GetSequenceById(id);
    }

    public DialogSequence GetIntroSequence(string levelId)
    {
        return GetSequence(levelId, "intro", null);
    }

    public DialogSequence GetEvacSequence(string levelId)
    {
        return GetSequence(levelId, "evac", null);
    }

    public DialogSequence GetOutroSequence(string levelId)
    {
        return GetSequence(levelId, "outro", null);
    }

    public DialogSequence GetPhaseSequence(string levelId, int phaseIndex)
    {
        return GetSequence(levelId, "phase", phaseIndex);
    }

    /// <summary>
    /// Retourne la liste de lignes d'une variante choisie aleatoirement
    /// dans une sequence donnee, en tenant compte des poids.
    /// </summary>
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
            if (w < 1) w = 1;
            totalWeight += w;
        }

        int roll = random.Next(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < sequence.variants.Length; i++)
        {
            int w = sequence.variants[i].weight;
            if (w < 1) w = 1;

            cumulative += w;
            if (roll < cumulative)
                return sequence.variants[i].lines ?? Array.Empty<DialogLine>();
        }

        return sequence.variants[sequence.variants.Length - 1].lines ?? Array.Empty<DialogLine>();
    }

    private IEnumerator SelfTestIntroSequence_LevelId()
    {
        while (!IsReady)
            yield return null;

        // Test sur un id coherent: W1_L1_intro.
        DialogSequence seq = GetIntroSequence("W1_L1");
        if (seq == null)
        {
            Debug.LogWarning("DialogManager SelfTest: aucune sequence intro pour W1_L1.");
            yield break;
        }

        DialogLine[] lines = GetRandomVariantLines(seq);
        Debug.Log("DialogManager SelfTest: intro W1_L1, " + lines.Length + " lignes.");

        for (int i = 0; i < lines.Length; i++)
            Debug.Log("SelfTest [" + lines[i].speakerId + "] " + lines[i].text);
    }
}
