using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameFlowController : MonoBehaviour
{
    [SerializeField] private RunSessionState runSession;
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private DefeatGameOverUI defeatGameOverUI;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";

    // Events publics pour brancher d'autres systèmes (sons globaux, analytics, etc.)
    public UnityEvent OnDefeat;   // déclenché en cas de défaite (mais pas de Game Over total)
    public UnityEvent OnGameOver; // déclenché quand le joueur n'a plus de vies

    private void OnEnable()
    {
        if (endLevelUI != null)
        {
            // OnVictory est branché ici par code pour nettoyer l'UI en cas de victoire.
            // OnSequenceFailed, lui, est branché directement dans l'Inspector sur HandleUiSequenceFailed.
            endLevelUI.OnVictory.AddListener(HandleVictory);
        }
    }

    private void OnDisable()
    {
        if (endLevelUI != null)
        {
            endLevelUI.OnVictory.RemoveListener(HandleVictory);
        }
    }

    // =================================================================
    // GESTION DE LA DEFAITE (séquence de fin de niveau ratée)
    // Appelée par EndLevelUI quand la séquence est terminée
    // et que l'objectif principal n'est pas atteint.
    // =================================================================
    public void HandleUiSequenceFailed()
    {
        if (runSession == null)
        {
            Debug.LogWarning("[GameFlowController] RunSessionState manquant.");
            return;
        }

        // On masque le panneau de stats (inutile en cas de défaite)
        if (endLevelUI != null)
            endLevelUI.HideStatsPanel();

        // On enlève une vie à la run actuelle
        runSession.RemoveLife(1);

        if (runSession.Lives <= 0)
        {
            // Plus de vies : vraie fin de run -> GAME OVER
            Debug.Log("GAME OVER");

            OnGameOver?.Invoke();

            if (defeatGameOverUI != null)
                defeatGameOverUI.ShowGameOver();
        }
        else
        {
            // Il reste des vies : défaite simple -> écran DEFEAT avec Retry
            Debug.Log("DEFEAT — RETRY");

            OnDefeat?.Invoke();

            if (defeatGameOverUI != null)
                defeatGameOverUI.ShowDefeat();
        }
    }

    // =================================================================
    // GESTION DE LA VICTOIRE
    // Appelée quand EndLevelUI a fini sa séquence de victoire.
    // =================================================================
    public void HandleVictory()
    {
        // Par sécurité : on s'assure que l'overlay DEFEAT/GAME OVER est éteint
        if (defeatGameOverUI != null)
            defeatGameOverUI.HideAll();
    }

    // =================================================================
    // BOUTONS (branchés sur DefeatGameOverUI.OnRetryRequested / OnMenuRequested)
    // =================================================================

    // Retry depuis DEFEAT : on garde les vies actuelles de la run
    public void RetryLevelKeepLives()
    {
        if (runSession != null)
            runSession.MarkCarryLivesOnNextRestart(true);

        ReloadCurrentLevel();
    }

    // Retry depuis GAME OVER : reset des vies selon la définition du vaisseau
    public void RetryLevelResetLives()
    {
        if (runSession != null)
            runSession.MarkCarryLivesOnNextRestart(false);

        ReloadCurrentLevel();
    }

    // Menu : retour à la scène de Title
    public void ReturnToTitle()
    {
        if (string.IsNullOrEmpty(titleSceneName))
        {
            Debug.LogWarning("[GameFlowController] titleSceneName non défini.");
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }

    private void ReloadCurrentLevel()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
