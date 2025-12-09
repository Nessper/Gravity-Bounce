using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Charge la base de dialogues depuis StreamingAssets et
/// fournit des méthodes pour récupérer les lignes adaptées
/// au contexte (intro, phases, fin de niveau).
/// VIT DANS BOOT DONC EN DONTDESTROYONLOAD
/// </summary>
public class DialogManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Code langue utilisé pour charger le fichier JSON (ex: 'fr', 'en').")]
    [SerializeField] private string languageCode = "fr";

    [Tooltip("Dossier relatif dans StreamingAssets où se trouve le fichier dialogs_XX.json.")]
    [SerializeField] private string dialogsFolder = "Dialogs";

    [Header("Debug")]
    [Tooltip("Si actif, lance un test simple au démarrage pour vérifier le chargement de l'intro W1-L1.")]
    [SerializeField] private bool runSelfTestOnStart = false;

    /// <summary>
    /// Base de données des dialogues chargée depuis le JSON.
    /// </summary>
    public DialogDatabase Database { get; private set; }

    /// <summary>
    /// Indique si la base de données est prête à être utilisée.
    /// </summary>
    public bool IsReady { get; private set; }

    private System.Random random = new System.Random();

    private void Awake()
    {
        StartCoroutine(LoadDatabaseCoroutine());
    }

    /// <summary>
    /// Lance éventuellement un petit test de vérification au démarrage,
    /// une fois que la base de dialogues est chargée.
    /// </summary>
    private void Start()
    {
        if (runSelfTestOnStart)
        {
            StartCoroutine(SelfTestIntroSequence());
        }
    }

    /// <summary>
    /// Charge le fichier JSON dialogs_[languageCode].json depuis StreamingAssets
    /// de manière compatible avec toutes les plateformes (y compris Android).
    /// </summary>
    private IEnumerator LoadDatabaseCoroutine()
    {
        IsReady = false;

        string fileName = "dialogs_" + languageCode + ".json";
        string fullPath = Path.Combine(Application.streamingAssetsPath, dialogsFolder, fileName);

        string uri = fullPath;

        // Sur certaines plateformes (Android notamment), StreamingAssets doit être lu via UnityWebRequest.
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_WEBGL)
        if (!fullPath.StartsWith("jar:") && !fullPath.StartsWith("http"))
        {
            uri = "file://" + fullPath;
        }
#else
        if (!fullPath.StartsWith("file://"))
        {
            uri = "file://" + fullPath;
        }
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
                Debug.LogError("Erreur lors du chargement des dialogues: " + request.error + " (" + fullPath + ")");
                yield break;
            }

            string jsonText = request.downloadHandler.text;
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("Fichier de dialogues vide ou introuvable: " + fullPath);
                yield break;
            }

            Database = JsonUtility.FromJson<DialogDatabase>(jsonText);
            if (Database == null || Database.sequences == null)
            {
                Debug.LogError("Impossible de parser la base de dialogues: " + fullPath);
                yield break;
            }
        }

        IsReady = true;
        Debug.Log("DialogManager: base de dialogues chargée (" + languageCode + "), " +
                  Database.sequences.Length + " séquences.");
    }

    /// <summary>
    /// Récupère une séquence d'intro pour un monde et un niveau.
    /// Retourne null si aucune séquence ne correspond.
    /// </summary>
    public DialogSequence GetIntroSequence(int world, int level)
    {
        return FindSequence("intro", world, level, null, null);
    }

    /// <summary>
    /// Récupère une séquence de phase pour un monde, un niveau et un index de phase.
    /// Retourne null si aucune séquence ne correspond.
    /// </summary>
    public DialogSequence GetPhaseSequence(int world, int level, int phaseIndex)
    {
        return FindSequence("phase", world, level, phaseIndex, null);
    }

    /// <summary>
    /// Récupère une séquence de fin de niveau pour un monde, un niveau et un outcome.
    /// Exemple outcome: "success".
    /// Retourne null si aucune séquence ne correspond.
    /// </summary>
    public DialogSequence GetPostLevelSequence(int world, int level, string outcome)
    {
        return FindSequence("postLevel", world, level, null, outcome);
    }

    /// <summary>
    /// Retourne la liste de lignes d'une variante choisie aléatoirement
    /// dans une séquence donnée, en tenant compte des poids.
    /// </summary>
    public DialogLine[] GetRandomVariantLines(DialogSequence sequence)
    {
        if (sequence == null || sequence.variants == null || sequence.variants.Length == 0)
        {
            return Array.Empty<DialogLine>();
        }

        if (sequence.variants.Length == 1)
        {
            return sequence.variants[0].lines ?? Array.Empty<DialogLine>();
        }

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
            {
                return sequence.variants[i].lines ?? Array.Empty<DialogLine>();
            }
        }

        return sequence.variants[sequence.variants.Length - 1].lines ?? Array.Empty<DialogLine>();
    }

    /// <summary>
    /// Recherche une séquence correspondant au type, monde, niveau et
    /// éventuellement phaseIndex et outcome.
    /// </summary>
    private DialogSequence FindSequence(string type, int world, int level, int? phaseIndex, string outcome)
    {
        if (!IsReady || Database == null || Database.sequences == null)
        {
            Debug.LogWarning("DialogManager: base non prête, aucune séquence trouvée.");
            return null;
        }

        for (int i = 0; i < Database.sequences.Length; i++)
        {
            DialogSequence seq = Database.sequences[i];
            if (!string.Equals(seq.type, type, StringComparison.OrdinalIgnoreCase))
                continue;

            if (seq.world != world || seq.level != level)
                continue;

            if (phaseIndex.HasValue && seq.type == "phase")
            {
                if (seq.phaseIndex != phaseIndex.Value)
                    continue;
            }

            if (!string.IsNullOrEmpty(outcome) && !string.IsNullOrEmpty(seq.outcome))
            {
                if (!string.Equals(seq.outcome, outcome, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            else if (!string.IsNullOrEmpty(outcome) && string.IsNullOrEmpty(seq.outcome))
            {
                continue;
            }

            return seq;
        }

        return null;
    }

    /// <summary>
    /// Petit test de debug optionnel pour vérifier que l'intro W1-L1
    /// est bien chargée et accessible. Utilise uniquement la console.
    /// </summary>
    private IEnumerator SelfTestIntroSequence()
    {
        while (!IsReady)
        {
            yield return null;
        }

        DialogSequence seq = GetIntroSequence(1, 1);
        if (seq == null)
        {
            Debug.LogWarning("DialogManager SelfTest: aucune sequence intro pour W1-L1.");
            yield break;
        }

        DialogLine[] lines = GetRandomVariantLines(seq);
        Debug.Log("DialogManager SelfTest: intro W1-L1, " + lines.Length + " lignes.");

        for (int i = 0; i < lines.Length; i++)
        {
            DialogLine line = lines[i];
            Debug.Log("SelfTest [" + line.speakerId + "] " + line.text);
        }
    }

    /// <summary>
    /// Récupère une séquence directement par son identifiant unique (field "id" dans le JSON).
    /// Retourne null si aucune séquence ne correspond.
    /// </summary>
    public DialogSequence GetSequenceById(string sequenceId)
    {
        if (!IsReady || Database == null || Database.sequences == null)
        {
            Debug.LogWarning("DialogManager: base non prête, GetSequenceById échoue.");
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

    public DialogSequence GetEvacSequence(int world, int level)
    {
        return FindSequence("evac", world, level, null, null);
    }


}
