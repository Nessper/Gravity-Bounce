using UnityEngine;
using UnityEngine.UI;

public class ProgressBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;  // ton ScoreManager de la scène
    [SerializeField] private Image fillImage;            // Image du GO "Fill" (Type = Filled, Fill Method = Horizontal)

    [Header("Configuration")]
    [SerializeField] private int targetScore = 100;      // Score objectif à atteindre
    [SerializeField] private bool clampOverfill = true;  // Empêche de dépasser 100%

    private void OnEnable()
    {
        if (scoreManager != null)
        {
            // On s'abonne à l'événement du ScoreManager
            scoreManager.onScoreChanged.AddListener(HandleScoreChanged);

            // Initialisation immédiate avec le score courant
            HandleScoreChanged(scoreManager.CurrentScore);
        }
        else
        {
            Debug.LogWarning("[ProgressBarUI] ScoreManager non assigné !");
            UpdateVisual(0f);
        }
    }

    private void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.onScoreChanged.RemoveListener(HandleScoreChanged);
    }

    public void SetTargetScore(int value)
    {
        targetScore = Mathf.Max(1, value);
        int current = scoreManager != null ? scoreManager.CurrentScore : 0;
        HandleScoreChanged(current);
    }

    private void HandleScoreChanged(int score)
    {
        if (targetScore <= 0) return;

        float t = (float)score / targetScore;
        if (clampOverfill) t = Mathf.Clamp01(t);

        UpdateVisual(t);
    }

    private void UpdateVisual(float t)
    {
        if (fillImage != null)
            fillImage.fillAmount = t;
    }
}
