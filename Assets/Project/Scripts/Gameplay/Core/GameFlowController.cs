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

    public UnityEvent OnDefeat;   // toujours dispo si tu veux brancher d'autres trucs
    public UnityEvent OnGameOver;

    private void OnEnable()
    {
        if (endLevelUI != null)
        {
            // OnSequenceFailed est branché dans l’Inspector
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


    public void HandleUiSequenceFailed()
    {
        if (runSession == null)
        {
            Debug.LogWarning("[GameFlowController] RunSessionState manquant.");
            return;
        }

        if (endLevelUI != null)
            endLevelUI.HideStatsPanel();


        // On enlève une vie
        runSession.RemoveLife(1);

        if (runSession.Lives <= 0)
        {
            Debug.Log("GAME OVER");

            OnGameOver?.Invoke();

            if (defeatGameOverUI != null)
                defeatGameOverUI.ShowGameOver();
        }
        else
        {
            Debug.Log("DEFEAT — RETRY");

            OnDefeat?.Invoke();

            if (defeatGameOverUI != null)
                defeatGameOverUI.ShowDefeat();
        }
        if (endLevelUI != null)
    endLevelUI.HideStatsPanel();

    }

    public void HandleVictory()
    {
        // Par sécurité : on s'assure que l'overlay est complètement éteint
        if (defeatGameOverUI != null)
            defeatGameOverUI.HideAll();
    }

    // =================================================================
    // BOUTONS (branchés sur DefeatGameOverUI.OnRetryRequested / OnMenuRequested)
    // =================================================================

    // Retry depuis DEFEAT : on garde les vies actuelles
    public void RetryLevelKeepLives()
    {
        if (runSession != null)
            runSession.MarkCarryLivesOnNextRestart(true);

        ReloadCurrentLevel();
    }

    // Retry depuis GAME OVER : reset des vies selon le vaisseau
    public void RetryLevelResetLives()
    {
        if (runSession != null)
            runSession.MarkCarryLivesOnNextRestart(false);

        ReloadCurrentLevel();
    }

    // Menu : retour au Title
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
