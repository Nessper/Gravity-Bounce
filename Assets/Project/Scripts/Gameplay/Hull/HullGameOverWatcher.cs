using UnityEngine;

/// <summary>
/// Surveille le Hull runtime.
/// Si Hull tombe a 0 PENDANT le gameplay arme,
/// on annule toute ceremonie en cours, on hard-stop le gameplay
/// et on declenche un GameOver "instant" (sans evac).
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
    [Tooltip("Si false, on force le score final a 0 (vaisseau detruit = score non pertinent).")]
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

    /// <summary>
    /// Reagit a chaque changement de Hull.
    /// Le GameOver ne peut partir que si :
    /// - Hull <= 0
    /// - GameplayArmed == true
    /// - aucun trigger precedent n'a deja gagne
    /// </summary>
    private void HandleHullChanged(int hull)
    {
        if (triggered)
            return;

        // On ne declenche que sur Hull <= 0.
        if (hull > 0)
            return;

        // On ignore si le gameplay n'est pas vraiment arme
        // (briefing, intro, ceremony, etc.).
        if (runStateController == null || !runStateController.GameplayArmed)
            return;

        // Securite anti double-trigger.
        triggered = true;

        // Priorite absolue au GameOver Hull :
        // on annule toute ceremonie / flow de fin qui pourrait etre en cours.
        if (endFlowController != null)
            endFlowController.AbortPendingCeremony();

        // Stop gameplay tout de suite (pas d'evac, pas de timer end).
        if (levelManager != null)
            levelManager.HardStopGameplay();

        // Fade musique gameplay vers 20% pour le GameOver
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolumeMultiplier(0.1f, 0.5f);
        }

        int finalScore = 0;

        // Optionnel : conserve le score courant si on veut l'afficher meme en Hull destroyed.
        if (keepScoreOnHullDestroyed && scoreManager != null)
        {
            finalScore = Mathf.Max(0, scoreManager.CurrentScore);
        }

        // Lance la sequence visuelle dediee (shake / flash / pause),
        // puis seulement le panneau final GameOver.
        if (hullFeedback != null)
        {
            hullFeedback.PlayHullDestroyedFeedback(() =>
            {
                if (endFlowController != null)
                    endFlowController.TriggerGameOverFinalRoutine(finalScore);
            });

            return;
        }

        // Fallback si aucun feedback n'est configure.
        if (endFlowController != null)
            endFlowController.TriggerGameOverFinalRoutine(finalScore);
    }
}