using TMPro;
using UnityEngine;

/// <summary>
/// Affichage texte de la progression de l'objectif principal.
/// Format : "courant / objectif" (ex: 12 / 20)
///
/// IMPORTANT :
/// - Comme ProgressBarUI, ce widget NE se met à jour que sur les flushs.
/// - Il ne réagit PAS en temps réel au score.
/// - Source de vérité : ScoreManager (TotalNonBlackBilles).
/// </summary>
public class ProgressCountUI : MonoBehaviour
{
    // --------------------------------------------------------------------
    // REFERENCES
    // --------------------------------------------------------------------

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private TMP_Text progressText;

    // --------------------------------------------------------------------
    // LEVEL DATA
    // --------------------------------------------------------------------

    [Header("Level Data")]
    [Tooltip("Seuil de l'objectif principal (nombre de billes à atteindre).")]
    [SerializeField] private int objectiveThreshold = 0;

    // --------------------------------------------------------------------
    // COLORS
    // --------------------------------------------------------------------

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color nearGoalColor = new Color(0.35f, 1f, 0.9f);
    [SerializeField] private Color reachedGoalColor = new Color(1f, 0.9f, 0.3f);

    [Tooltip("Ratio à partir duquel on considère que le joueur est proche de l'objectif.")]
    [Range(0f, 1f)]
    [SerializeField] private float nearGoalRatio = 0.8f;

    // --------------------------------------------------------------------
    // FALLBACK
    // --------------------------------------------------------------------

    [Header("Fallback")]
    [SerializeField] private string defaultText = "0/0";

    private bool isConfigured;

    // --------------------------------------------------------------------
    // UNITY
    // --------------------------------------------------------------------

    private void Awake()
    {
        ApplyDefaultText();
    }

    // --------------------------------------------------------------------
    // PUBLIC API
    // --------------------------------------------------------------------

    /// <summary>
    /// Configure le widget pour un niveau.
    /// Appelé par LevelManager (HandlePlannedReady).
    /// </summary>
    public void Configure(int objectiveThreshold)
    {
        this.objectiveThreshold = Mathf.Max(0, objectiveThreshold);
        isConfigured = true;

        if (scoreManager == null)
        {
            Debug.LogError("[ProgressCountUI] ScoreManager non assigné.");
            ApplyDefaultText();
            return;
        }

        Refresh();
    }

    /// <summary>
    /// Met à jour le texte à partir du ScoreManager.
    /// Appelé sur chaque flush (comme la ProgressBar).
    /// </summary>
    public void Refresh()
    {
        if (!isConfigured || scoreManager == null || progressText == null)
        {
            ApplyDefaultText();
            return;
        }

        int collected = Mathf.Max(0, scoreManager.TotalNonBlackBilles);
        int target = Mathf.Max(0, objectiveThreshold);

        progressText.text = collected + "/" + target;
        progressText.color = ResolveColor(collected, target);
    }

    /// <summary>
    /// Reset visuel (utile si reuse ou debug).
    /// </summary>
    public void ResetDisplay()
    {
        isConfigured = false;
        ApplyDefaultText();
    }

    // --------------------------------------------------------------------
    // INTERNAL
    // --------------------------------------------------------------------

    private void ApplyDefaultText()
    {
        if (progressText == null)
            return;

        progressText.text = defaultText;
        progressText.color = normalColor;
    }

    private Color ResolveColor(int collected, int target)
    {
        if (target <= 0)
            return normalColor;

        float ratio = collected / (float)target;

        if (ratio >= 1f)
            return reachedGoalColor;

        if (ratio >= nearGoalRatio)
            return nearGoalColor;

        return normalColor;
    }
}