using System.Collections;
using UnityEngine;

/// <summary>
/// Barre de progression (wrapper) basée sur une barre segmentée.
/// IMPORTANT : cette barre ne se met à jour que sur les flushs.
/// Elle ne réagit plus aux changements de score.
/// </summary>
public class ProgressBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private SegmentedProgressBarUI segmentedBar;

    [Header("Données fixes du niveau")]
    [SerializeField] private int plannedTotalBalls = 1;
    [SerializeField] private int objectiveThreshold = 0;

    private bool isConfigured;

    /// <summary>
    /// Vrai si la barre segmentée est en cours d'animation step-by-step.
    /// </summary>
    public bool IsAnimating => segmentedBar != null && segmentedBar.IsAnimating;

    private void OnDisable()
    {
        // Plus rien à désabonner, on ne touche plus au ScoreManager ici.
    }

    /// <summary>
    /// Configure la barre pour un niveau :
    /// - plannedTotalBalls : nombre total prévu (planned) sur lequel on base la progression
    /// - objectiveThreshold : seuil d'objectif (pour placer le marqueur/segment objectif)
    /// </summary>
    public void Configure(int plannedTotalBalls, int objectiveThreshold)
    {
        this.plannedTotalBalls = Mathf.Max(1, plannedTotalBalls);
        this.objectiveThreshold = Mathf.Max(0, objectiveThreshold);
        isConfigured = true;

        if (scoreManager == null)
        {
            Debug.LogError("[ProgressBarUI] Aucun ScoreManager assigné !");
            return;
        }

        if (segmentedBar != null)
        {
            segmentedBar.SetThresholdFromGoal(this.objectiveThreshold, this.plannedTotalBalls);
            segmentedBar.SetProgress01(0f);
        }

        Refresh();
    }

    /// <summary>
    /// Recalcule la progression à partir des stats ScoreManager et l'envoie à la barre segmentée.
    /// Cette méthode peut déclencher une animation step-by-step (si activée).
    /// </summary>
    public void Refresh()
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

        int collected = scoreManager.TotalNonBlackBilles;
        float t = Mathf.Clamp01(collected / (float)plannedTotalBalls);

        segmentedBar.SetProgress01(t);
    }

    /// <summary>
    /// Permet à un système externe (fin de niveau) d'attendre que la barre ait terminé son animation.
    /// </summary>
    public IEnumerator WaitForProgressAnimationComplete(float timeoutSec = 2f)
    {
        if (segmentedBar == null)
            yield break;

        yield return segmentedBar.WaitForAnimationComplete(timeoutSec);
    }
}
