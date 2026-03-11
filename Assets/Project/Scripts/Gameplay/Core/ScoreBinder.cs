using UnityEngine;

/// <summary>
/// Synchronise le score runtime avec le HUD.
/// - bind ScoreManager.onScoreChanged -> ScoreUI
/// - push initial (optionnel selon ton ScoreManager)
/// 
/// But: sortir ce binding de LevelManager.
/// </summary>
public class ScoreBinder : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreUI scoreUI;

    private void OnEnable()
    {
        if (scoreManager == null || scoreUI == null)
            return;

        scoreManager.onScoreChanged.RemoveListener(scoreUI.UpdateScoreText);
        scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);

        // Si ton ScoreManager expose un getter, tu peux pousser un initial.
        // Sinon, laisse comme ca (le ResetScore declenchera un event si tu l as code ainsi).
        // Exemple:
        // scoreUI.UpdateScoreText(scoreManager.GetScore());
    }

    private void OnDisable()
    {
        if (scoreManager == null || scoreUI == null)
            return;

        scoreManager.onScoreChanged.RemoveListener(scoreUI.UpdateScoreText);
    }
}
