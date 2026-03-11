using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// CreditsController (ultra minimal V1)
/// - Video: driven by Inspector (VideoPlayer.clip). No Resources loading.
/// - JSON (optional): Resources/Credits/CreditsCatalog.json
///   Uses first entry in "endings" to fetch:
///     - texts.mainText
///     - links.discordUrl
/// - Displays: SCORE, BEST (+ NEW BEST inline) using SaveManager meta persistence.
/// - Buttons: Discord (OpenURL) + Menu (GoToTitle)
/// </summary>
public class CreditsController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Media (Inspector)")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestText;
    [SerializeField] private TMP_Text mainText;

    [Header("Buttons")]
    [SerializeField] private Button discordButton;
    [SerializeField] private Button menuButton;

    [Header("Catalog (optional)")]
    [Tooltip("Resources path without extension. Default expects Assets/Resources/Credits/CreditsCatalog.json")]
    [SerializeField] private string creditsCatalogResourcePath = "Credits/CreditsCatalog";

    private void Start()
    {
        CreditsEndingConfig cfg = LoadGlobalConfig();

        PlayVideoFromInspector();

        int runScore = GetRunScore();

        int bestBefore = GetBestScore();
        bool isNewBest = (runScore > bestBefore);

        // Commit best score in meta save (idempotent if already committed elsewhere).
        TryCommitBestScore(runScore);

        int bestAfter = GetBestScore();

        if (scoreText != null)
            scoreText.text = $"{runScore}";

        if (bestText != null)
            bestText.text = isNewBest ? $"{bestAfter}  (NEW BEST!)" : $"BEST : {bestAfter}";

        if (mainText != null)
            mainText.text = ResolveMainText(cfg);

        BindButtons(cfg);
    }

    // ---------------------------------------------------------
    // Video
    // ---------------------------------------------------------

    /// <summary>
    /// Plays the VideoPlayer clip already assigned in the Inspector.
    /// No Resources loading, no path.
    /// </summary>
    private void PlayVideoFromInspector()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.isLooping = true;

        if (videoPlayer.clip != null)
            videoPlayer.Play();
    }

    // ---------------------------------------------------------
    // Config
    // ---------------------------------------------------------

    private CreditsEndingConfig LoadGlobalConfig()
    {
        CreditsCatalog catalog = CreditsCatalogLoader.Load(creditsCatalogResourcePath);
        if (catalog == null || catalog.endings == null || catalog.endings.Length == 0)
            return null;

        return catalog.endings[0];
    }

    private string ResolveMainText(CreditsEndingConfig cfg)
    {
        if (cfg != null && cfg.texts != null && !string.IsNullOrEmpty(cfg.texts.mainText))
            return cfg.texts.mainText;

        return "Developed by Paul Bertrand\nAlpha Version 0.1\n\nJoin the Discord for feedback.";
    }

    private void BindButtons(CreditsEndingConfig cfg)
    {
        if (discordButton != null)
        {
            discordButton.onClick.RemoveAllListeners();
            discordButton.onClick.AddListener(() =>
            {
                string url = (cfg != null && cfg.links != null) ? cfg.links.discordUrl : "";
                if (!string.IsNullOrEmpty(url))
                    Application.OpenURL(url);
            });
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(() =>
            {
                if (BootRoot.GameFlow != null)
                    BootRoot.GameFlow.GoToTitle();
            });
        }
    }

    // ---------------------------------------------------------
    // Score (Run + Best)
    // ---------------------------------------------------------

    private int GetRunScore()
    {
        // Preferred: SaveManager persisted run score
        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            RunStateData run = SaveManager.Instance.GetRunState();
            if (run != null)
                return Mathf.Max(0, run.currentRunScore);
        }

        // Fallback: RunSessionState (if you expose it)
        if (runSession != null)
            return Mathf.Max(0, runSession.RunScore);

        return 0;
    }

    private int GetBestScore()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return 0;

        return Mathf.Max(0, SaveManager.Instance.GetBestRunScore());
    }

    private void TryCommitBestScore(int runScore)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.TryUpdateBestRunScore(runScore);
    }
}

#region Catalog models + loader (aligned with your JSON)

[Serializable]
public class CreditsCatalog
{
    public CreditsEndingConfig[] endings;
}

[Serializable]
public class CreditsEndingConfig
{
    public string endingId;
    public string sceneName;
    public CreditsTexts texts;
    public CreditsLinks links;
}

[Serializable]
public class CreditsTexts
{
    public string mainText;
}

[Serializable]
public class CreditsLinks
{
    public string discordUrl;
}

public static class CreditsCatalogLoader
{
    public static CreditsCatalog Load(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        TextAsset asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning("[CreditsCatalogLoader] Missing TextAsset at Resources/" + resourcePath + ".json");
            return null;
        }

        try
        {
            return JsonUtility.FromJson<CreditsCatalog>(asset.text);
        }
        catch (Exception e)
        {
            Debug.LogError("[CreditsCatalogLoader] Invalid JSON: " + e.Message);
            return null;
        }
    }
}

#endregion