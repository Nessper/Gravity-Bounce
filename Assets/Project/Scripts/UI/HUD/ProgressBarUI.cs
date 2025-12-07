using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de barre de progression des billes collectées par rapport au plan du niveau.
///
/// Rôle :
/// - Affiche la progression globale des billes collectées (TotalBilles / plannedTotalBalls).
/// - Place un seuil visuel sur la barre pour l'objectif principal (ThresholdCount),
///   via SegmentedProgressBarUI.
/// - Réagit aux changements de score via ScoreManager.onScoreChanged.
///
/// Note :
/// Pour l'instant, la progression est basée sur TOUTES les billes collectées.
/// Plus tard, on pourra basculer sur "billes blanches uniquement"
/// en changeant la source (TotalWhiteBalls au lieu de TotalBilles).
/// </summary>
public class ProgressBarUI : MonoBehaviour
{
    // --------------------------------------------------------------------
    // RÉFÉRENCES
    // --------------------------------------------------------------------

    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    // Référence vers le ScoreManager, pour lire les billes collectées et écouter les changements de score.

    [SerializeField] private SegmentedProgressBarUI segmentedBar;
    // Barre segmentée responsable de l'affichage (segments + couleurs + seuil).

    // --------------------------------------------------------------------
    // DONNÉES FIXES DU NIVEAU
    // --------------------------------------------------------------------

    [Header("Données fixes du niveau")]
    [SerializeField] private int plannedTotalBalls = 1;
    // Nombre total de billes prévues pour le niveau (plan théorique, JSON).
    // Actuellement : toutes les billes. Plus tard : uniquement les blanches si nécessaire.

    [SerializeField] private int objectiveThreshold = 0;
    // Nombre de billes à atteindre pour l'objectif principal (ThresholdCount).

    // --------------------------------------------------------------------
    // ÉTAT INTERNE
    // --------------------------------------------------------------------

    // Indique si la barre a été configurée pour ce niveau.
    private bool isConfigured;

    // Indique si le listener ScoreManager.onScoreChanged est branché.
    private bool attached;

    // --------------------------------------------------------------------
    // CYCLE UNITY
    // --------------------------------------------------------------------

    private void OnEnable()
    {
        // Quand l'objet est réactivé, on rafraîchit si on était déjà attaché.
        if (attached)
            Refresh();
    }

    private void OnDisable()
    {
        // On se désabonne proprement des events du ScoreManager quand la barre est désactivée.
        if (attached && scoreManager != null)
            scoreManager.onScoreChanged.RemoveListener(HandleScoreChanged);

        attached = false;
    }

    // --------------------------------------------------------------------
    // CONFIGURATION
    // --------------------------------------------------------------------

    /// <summary>
    /// Configure la barre de progression pour ce niveau.
    /// Appelée une fois au début du niveau par le contrôleur (LevelManager).
    ///
    /// plannedTotalBalls  = nombre total de billes prévues sur le niveau.
    /// objectiveThreshold = nombre de billes à atteindre pour l'objectif principal.
    /// </summary>
    public void Configure(int plannedTotalBalls, int objectiveThreshold)
    {
        this.plannedTotalBalls = Mathf.Max(1, plannedTotalBalls);
        this.objectiveThreshold = Mathf.Max(0, objectiveThreshold);

        isConfigured = true;

        // Branchement sur l'event de score si ce n'est pas déjà fait.
        if (!attached && scoreManager != null)
        {
            scoreManager.onScoreChanged.AddListener(HandleScoreChanged);
            attached = true;
        }

        // Configure le seuil visuel sur la barre segmentée.
        if (segmentedBar != null)
        {
            segmentedBar.SetThresholdFromGoal(this.objectiveThreshold, this.plannedTotalBalls);
            segmentedBar.SetProgress01(0f);
        }

        // Met à jour la barre avec l'état actuel.
        Refresh();
    }

    /// <summary>
    /// Forçage visuel de la barre (appelé par LevelManager après Configure
    /// ou par d'autres systèmes pour refléter la progression en runtime).
    /// </summary>
    public void Refresh()
    {
        int currentScore = scoreManager != null ? scoreManager.CurrentScore : 0;
        HandleScoreChanged(currentScore);
    }

    // --------------------------------------------------------------------
    // RÉACTION AUX CHANGEMENTS DE SCORE
    // --------------------------------------------------------------------

    /// <summary>
    /// Callback appelé lorsqu'un changement de score est notifié par ScoreManager.
    /// On ne se sert pas ici de la valeur du score, mais du nombre total de billes collectées.
    /// </summary>
    private void HandleScoreChanged(int _)
    {
        if (!isConfigured || scoreManager == null || segmentedBar == null)
        {
            if (segmentedBar != null)
                segmentedBar.SetProgress01(0f);
            return;
        }

        if (plannedTotalBalls <= 0)
        {
            segmentedBar.SetProgress01(0f);
            return;
        }

        // Progression globale basée sur le nombre total de billes collectées.
        // TODO plus tard : remplacer TotalBilles par TotalWhiteBalls pour exclure les noires.
        int collected = scoreManager.TotalBilles;
        float t = Mathf.Clamp01(collected / (float)plannedTotalBalls);

        segmentedBar.SetProgress01(t);
    }
}
