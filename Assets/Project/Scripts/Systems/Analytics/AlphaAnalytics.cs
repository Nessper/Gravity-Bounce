using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// AlphaAnalytics
/// ------------------------------------------------------------
/// Systeme analytics V1 pour l alpha :
/// - cree un sessionId unique au lancement du jeu
/// - incremente un runId simple (1, 2, 3...)
/// - envoie des lignes vers un Google Form / Google Sheet
///
/// Evenements supportes :
/// - level_end
/// - run_end
///
/// IMPORTANT :
/// - Donnees legeres uniquement
/// - Pas de tracking intrusif
/// - Echec reseau = ignore silencieusement
/// </summary>
public class AlphaAnalytics : MonoBehaviour
{
    public static AlphaAnalytics Instance { get; private set; }

    [Header("General")]
    [Tooltip("Active / desactive completement les analytics.")]
    [SerializeField] private bool analyticsEnabled = true;

    [Tooltip("Version envoyee. Si vide, on utilise Application.version.")]
    [SerializeField] private string versionOverride = "";

    [Header("Google Form")]
    [Tooltip("URL formResponse du Google Form.")]
    [SerializeField]
    private string formResponseUrl =
        "https://docs.google.com/forms/d/e/1FAIpQLSdmMNzi7Y0KsUdeFd0vISFq-b3CbZZa4uzg_XtzrGNLFBCAjw/formResponse";

    // Mapping des champs Google Form
    private const string ENTRY_PLATFORM = "entry.798175474";
    private const string ENTRY_EVENT = "entry.1837315321";
    private const string ENTRY_VERSION = "entry.34879558";
    private const string ENTRY_SESSION_ID = "entry.395953445";
    private const string ENTRY_RUN_ID = "entry.634565875";
    private const string ENTRY_LEVEL_ID = "entry.1306174808";
    private const string ENTRY_RESULT = "entry.1258946610";
    private const string ENTRY_MEDAL = "entry.1939692986";
    private const string ENTRY_DURATION_SEC = "entry.1150360214";
    private const string ENTRY_FINAL_LEVEL_ID = "entry.1973886726";
    private const string ENTRY_BOSS_REACHED = "entry.974327265";
    private const string ENTRY_GAME_FINISHED = "entry.1921684238";

    // Etat session
    private string sessionId;
    private int currentRunId;

    // Etat runtime niveau / run
    private float currentLevelStartRealtime;
    private float currentRunStartRealtime;

    public string SessionId => sessionId;
    public int CurrentRunId => currentRunId;

    private string AnalyticsVersion
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(versionOverride))
                return versionOverride;

            if (!string.IsNullOrWhiteSpace(Application.version))
                return Application.version;

            return "unknown";
        }
    }

    private void Awake()
    {
        // Singleton simple persistant.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        // Dans l'éditeur Unity on désactive les analytics
        analyticsEnabled = false;
#endif

        sessionId = Guid.NewGuid().ToString("N");
        currentRunId = 0;

        currentLevelStartRealtime = 0f;
        currentRunStartRealtime = 0f;
    }

    /// <summary>
    /// A appeler au debut d une nouvelle run.
    /// Incremente le compteur de run dans la session.
    /// </summary>
    public void BeginRun()
    {
        if (!analyticsEnabled)
            return;

        currentRunId++;
        currentRunStartRealtime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// A appeler au debut d un niveau.
    /// Sert a calculer durationSec a la fin du niveau.
    /// </summary>
    public void BeginLevel()
    {
        if (!analyticsEnabled)
            return;

        currentLevelStartRealtime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// Envoie un event de fin de niveau.
    /// </summary>
    public void SendLevelEnd(
        string levelId,
        string result,
        string medal)
    {
        if (!analyticsEnabled)
            return;

        Dictionary<string, string> fields = CreateBaseFields();

        fields[ENTRY_EVENT] = "level_end";
        fields[ENTRY_LEVEL_ID] = Safe(levelId);
        fields[ENTRY_RESULT] = Safe(result);
        fields[ENTRY_MEDAL] = Safe(medal);
        fields[ENTRY_DURATION_SEC] = Mathf.RoundToInt(GetCurrentLevelDurationSec()).ToString();

        // Champs non utilises pour level_end
        fields[ENTRY_FINAL_LEVEL_ID] = "";
        fields[ENTRY_BOSS_REACHED] = "false";
        fields[ENTRY_GAME_FINISHED] = "false";

        StartCoroutine(PostForm(fields));
    }

    /// <summary>
    /// Envoie un event de fin de run.
    /// </summary>
    public void SendRunEnd(
        string finalLevelId,
        bool bossReached,
        bool gameFinished)
    {
        if (!analyticsEnabled)
            return;

        Dictionary<string, string> fields = CreateBaseFields();

        fields[ENTRY_EVENT] = "run_end";

        // Champs non utilises pour run_end
        fields[ENTRY_LEVEL_ID] = "";
        fields[ENTRY_RESULT] = "";
        fields[ENTRY_MEDAL] = "";
        fields[ENTRY_DURATION_SEC] = Mathf.RoundToInt(GetCurrentRunDurationSec()).ToString();

        fields[ENTRY_FINAL_LEVEL_ID] = Safe(finalLevelId);
        fields[ENTRY_BOSS_REACHED] = bossReached ? "true" : "false";
        fields[ENTRY_GAME_FINISHED] = gameFinished ? "true" : "false";

        StartCoroutine(PostForm(fields));
    }

    /// <summary>
    /// Cree les champs communs a tous les evenements.
    /// </summary>
    private Dictionary<string, string> CreateBaseFields()
    {
        return new Dictionary<string, string>
        {
            { ENTRY_EVENT, "" },
            { ENTRY_PLATFORM, GetPlatformName() },
            { ENTRY_VERSION, AnalyticsVersion },
            { ENTRY_SESSION_ID, Safe(sessionId) },
            { ENTRY_RUN_ID, currentRunId.ToString() },
            { ENTRY_LEVEL_ID, "" },
            { ENTRY_RESULT, "" },
            { ENTRY_MEDAL, "" },
            { ENTRY_DURATION_SEC, "0" },
            { ENTRY_FINAL_LEVEL_ID, "" },
            { ENTRY_BOSS_REACHED, "false" },
            { ENTRY_GAME_FINISHED, "false" }
        };
    }

    /// <summary>
    /// Envoi HTTP simple vers Google Form.
    /// En cas d echec reseau, on ignore silencieusement.
    /// </summary>
    private IEnumerator PostForm(Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(formResponseUrl))
            yield break;

        WWWForm form = new WWWForm();

        foreach (var kvp in fields)
            form.AddField(kvp.Key, kvp.Value ?? "");

        using (UnityWebRequest req = UnityWebRequest.Post(formResponseUrl, form))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            // Pour l alpha, on ne spam pas la console en cas d echec.
            // Si tu veux debug temporairement, decommenter ici :
            //
            // if (req.result != UnityWebRequest.Result.Success)
            //     Debug.LogWarning("[AlphaAnalytics] Echec envoi: " + req.error);
        }
    }

    /// <summary>
    /// Duree du niveau en cours.
    /// </summary>
    private float GetCurrentLevelDurationSec()
    {
        if (currentLevelStartRealtime <= 0f)
            return 0f;

        return Mathf.Max(0f, Time.realtimeSinceStartup - currentLevelStartRealtime);
    }

    /// <summary>
    /// Duree de la run en cours.
    /// </summary>
    private float GetCurrentRunDurationSec()
    {
        if (currentRunStartRealtime <= 0f)
            return 0f;

        return Mathf.Max(0f, Time.realtimeSinceStartup - currentRunStartRealtime);
    }

    /// <summary>
    /// Evite les null.
    /// </summary>
    private string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value;
    }

    // ----------------------------------------------------------------
    // Helpers pratiques optionnels pour convertir tes enums / etats
    // ----------------------------------------------------------------

    private string GetPlatformName()
    {
        #if UNITY_EDITOR
                return "editor";
        #elif UNITY_WEBGL
            return "webgl";
        #elif UNITY_STANDALONE_WIN
            return "windows";
        #elif UNITY_STANDALONE_OSX
            return "mac";
        #elif UNITY_ANDROID
            return "android";
        #elif UNITY_IOS
            return "ios";
        #else
            return "unknown";
        #endif
    }

    public static string MedalToString(EndMedal medal)
    {
        switch (medal)
        {
            case EndMedal.Bronze: return "bronze";
            case EndMedal.Silver: return "silver";
            case EndMedal.Gold: return "gold";
            default: return "none";
        }
    }
}