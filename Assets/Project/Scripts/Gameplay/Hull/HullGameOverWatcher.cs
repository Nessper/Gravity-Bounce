using UnityEngine;

/// <summary>
/// Surveille le Hull runtime. Si Hull tombe à 0 PENDANT le gameplay armé,
/// on hard-stop le gameplay et on déclenche un GameOver "instant" (sans évac).
/// La transition vers le panel final passe par une séquence feedback (shake/flash) si dispo.
/// </summary>
public class HullGameOverWatcher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private LevelRunStateController runStateController;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private LevelEndFlowController endFlowController;
    [SerializeField] private HullDamageFeedbackController hullFeedback;
    [SerializeField] private ScoreManager scoreManager;

    [Header("GameOver (Hull=0)")]
    [Tooltip("Si false, on force le score final à 0 (vaisseau détruit = score non pertinent).")]
    [SerializeField] private bool keepScoreOnHullDestroyed = false;

    private bool triggered;

    private void OnEnable()
    {
        triggered = false;

        if (runSessionState != null)
            runSessionState.OnHullChanged.AddListener(HandleHullChanged);
    }

    private void OnDisable()
    {
        if (runSessionState != null)
            runSessionState.OnHullChanged.RemoveListener(HandleHullChanged);
    }

    private void HandleHullChanged(int hull)
    {
        if (triggered)
            return;

        // On ne déclenche que sur Hull <= 0
        if (hull > 0)
            return;

        // On ignore si le gameplay n'est pas réellement armé (briefing/intro/ceremony/etc.)
        if (runStateController == null || !runStateController.GameplayArmed)
            return;

        // Sécurité anti double-trigger
        triggered = true;

        // Stop gameplay tout de suite (pas d'evac, pas de timer end)
        if (levelManager != null)
            levelManager.HardStopGameplay();

        int finalScore = 0;

        if (keepScoreOnHullDestroyed && scoreManager != null)
        {
            // NOTE: nécessite un getter CurrentScore dans ScoreManager
            finalScore = Mathf.Max(0, scoreManager.CurrentScore);
        }

        // Séquence visuelle (plus longue) puis panel final
        if (hullFeedback != null)
        {
            hullFeedback.PlayHullDestroyedFeedback(() =>
            {
                if (endFlowController != null)
                    endFlowController.TriggerGameOverFinalRoutine(finalScore);
            });

            return;
        }

        if (endFlowController != null)
            endFlowController.TriggerGameOverFinalRoutine(finalScore);
    }
}
