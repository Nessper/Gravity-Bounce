using UnityEngine;

/// <summary>
/// Contrôleur de la barre de score final (EndLevel).
/// - Reçoit les infos de score (thresholds, progressMax, score courant).
/// - Calcule le ratio et alimente SegmentedFinalScoreBarUI.
/// - Expose une API simple pour EndLevelUI.
/// 
/// Hiérarchie attendue :
/// FinalScoreBar (ce script)
/// Child : Bar_Segment_Group (SegmentedFinalScoreBarUI)
/// </summary>
public class FinalScoreBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private SegmentedFinalScoreBarUI segmentedBar;
    // segmentedBar : script visuel qui gère les segments (couleurs, animation step-by-step).

    [Header("Runtime")]
    [SerializeField] private int progressMax = 0;
    // progressMax : valeur de score correspondant à 100% de la barre (ex : Gold * 1.2).

    private int currentScore = 0;

    public int ProgressMax => progressMax;
    public int CurrentScore => currentScore;

    private void Awake()
    {
        // Si la référence n'est pas renseignée, on essaie de la trouver automatiquement.
        if (segmentedBar == null)
            segmentedBar = GetComponentInChildren<SegmentedFinalScoreBarUI>();
    }

    /// <summary>
    /// Configure la barre finale à partir des thresholds de médailles et du score max.
    /// - bronzeThreshold / silverThreshold / goldThreshold : valeurs de LevelData.ScoreGoals.
    /// - maxScore : souvent goldThreshold * 1.2f (déjà calculé dans EndLevelUI).
    /// 
    /// Ne lance pas encore d'animation, mais place les segments spéciaux Bronze/Silver/Gold.
    /// </summary>
    public void Configure(int bronzeThreshold, int silverThreshold, int goldThreshold, int maxScore)
    {
        progressMax = Mathf.Max(1, maxScore); // éviter division par zéro.

        if (segmentedBar != null)
        {
            segmentedBar.SetThresholdsFromGoals(bronzeThreshold, silverThreshold, goldThreshold, progressMax);
        }

        // On repart de zéro visuellement.
        ResetInstant();
    }

    /// <summary>
    /// Réinitialise la barre visuellement à 0, sans animation.
    /// </summary>
    public void ResetInstant()
    {
        currentScore = 0;

        if (segmentedBar != null)
        {
            segmentedBar.ResetInstant();
        }
    }

    /// <summary>
    /// Met à jour le score courant et la progression visuelle de la barre.
    /// EndLevelUI l'appellera à chaque fois que le score cumulé "runningScore" change.
    /// </summary>
    public void SetScore(int newScore)
    {
        currentScore = Mathf.Max(0, newScore);

        if (segmentedBar == null || progressMax <= 0)
            return;

        float ratio = Mathf.Clamp01((float)currentScore / progressMax);
        segmentedBar.SetProgress01(ratio);
    }

    
}
