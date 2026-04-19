using UnityEngine;

/// <summary>
/// Gere les actions metier demandees depuis l overlay de pause.
///
/// Responsabilites :
/// - appliquer la penalite d abandon
/// - decider si la run survit ou non
/// - declencher retry / game over / retour titre
///
/// Important :
/// - ne gere pas l affichage de l overlay
/// - ne freeze pas le jeu
/// - ne connait pas le contenu UI de pause
/// </summary>
public class LevelPauseFlowHandler : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Penalite appliquee lors d un abandon / retry depuis la pause.")]
    [SerializeField] private int abortPenaltyAmount = 1;

    /// <summary>
    /// Gere l action Retry depuis la pause.
    /// </summary>
    public void HandleRetryRequested()
    {
        AudioManager.Instance?.StopDialogTypingLoop();
        AudioManager.Instance?.StopAll();

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[LevelPauseFlowHandler] SaveManager absent. Retry impossible.");
            return;
        }

        SaveManager.Instance.ApplyAbortPenaltyNow(abortPenaltyAmount);

        int hull = SaveManager.Instance.GetRemainingHullInRun();

        if (hull > 0)
        {
            BootRoot.GameFlow.RetryLevel();
        }
        else
        {
            SaveManager.Instance.MarkGameOverInRun();
            BootRoot.GameFlow.GoToTitle();
        }
    }

    /// <summary>
    /// Gere l action Menu depuis la pause.
    /// </summary>
    public void HandleMenuRequested()
    {
        AudioManager.Instance?.StopDialogTypingLoop();
        AudioManager.Instance?.StopAll();

        if (SaveManager.Instance != null)
            SaveManager.Instance.ApplyAbortPenaltyNow(abortPenaltyAmount);

        BootRoot.GameFlow.GoToTitle();
    }
}