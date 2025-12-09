using UnityEngine;


// Cette barre NE se met à jour que sur les flushs.
// Elle ne réagit plus aux changements de score.

public class ProgressBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private SegmentedProgressBarUI segmentedBar;

    [Header("Données fixes du niveau")]
    [SerializeField] private int plannedTotalBalls = 1;
    [SerializeField] private int objectiveThreshold = 0;

    private bool isConfigured;

    private void OnDisable()
    {
        // plus rien à désabonner, on ne touche plus au ScoreManager ici
    }

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
}
