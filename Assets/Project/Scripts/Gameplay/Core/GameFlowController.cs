using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Contrôle le flow global d'un niveau du point de vue de la campagne (run) :
/// - Réagit aux résultats de fin de niveau (victoire / défaite) notifiés par EndLevelUI.
/// - Met à jour RunSessionState (vies en RAM) et l'UI Defeat/GameOver.
/// - Met à jour la persistance (RunStateData) : vies restantes, état de la run, levelInProgress.
/// - Gère les actions Retry (avec ou sans reset) et retour au Title.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Header("Runtime State")]
    [SerializeField] private RunSessionState runSession;        // Etat de run en RAM (vies, flag retry)
    [SerializeField] private EndLevelUI endLevelUI;             // UI de fin de niveau (victoire/défaite)
    [SerializeField] private DefeatGameOverUI defeatGameOverUI; // Overlay DEFEAT / GAME OVER
    [SerializeField] private VictoryUI victoryUI;               // Overlay Victory

    [Header("Score Summary")]
    [SerializeField] private LevelScoreSummaryUI gameOverRunScoreSummary;
    // gameOverRunScoreSummary : instance de LevelScoreSummaryUI pour afficher le score de la campagne (run).

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("Penalties")]
    [SerializeField] private int hullLossOnFailure = 4;


    // Events publics pour brancher d'autres systèmes (sons globaux, analytics, etc.)
    public UnityEvent OnDefeat;   // déclenché en cas de défaite (mais pas de Game Over total)
    public UnityEvent OnGameOver; // déclenché quand le joueur n'a plus de vies (fin de run)


    private void Start()
    {
        // Plus de logique de best score de niveau ici.
        // On pourrait logguer le score de run actuel si besoin.
    }


    private void OnEnable()
    {
        if (endLevelUI != null)
        {
            // OnVictory est branché ici par code pour nettoyer l'UI en cas de victoire.
            // OnSequenceFailed, lui, est branché directement dans l'Inspector sur HandleUiSequenceFailed.
            endLevelUI.OnVictory.AddListener(HandleVictory);
        }

        if (defeatGameOverUI != null)
        {
            // Boutons DEFEAT / GAME OVER gérés en code
            defeatGameOverUI.OnRetryDefeat.AddListener(HandleRetryDefeatRequested);
            defeatGameOverUI.OnRetryGameOver.AddListener(HandleRetryGameOverRequested);
            defeatGameOverUI.OnMenu.AddListener(HandleMenuRequested);
        }
    }

    private void OnDisable()
    {
        if (endLevelUI != null)
        {
            endLevelUI.OnVictory.RemoveListener(HandleVictory);
        }

        if (defeatGameOverUI != null)
        {
            defeatGameOverUI.OnRetryDefeat.RemoveListener(HandleRetryDefeatRequested);
            defeatGameOverUI.OnRetryGameOver.RemoveListener(HandleRetryGameOverRequested);
            defeatGameOverUI.OnMenu.RemoveListener(HandleMenuRequested);
        }
    }


    // =================================================================
    // GESTION DE LA DEFAITE (objectif principal raté)
    // Appelée par EndLevelUI quand la séquence de fin est terminée
    // et que l'objectif principal n'est PAS atteint.
    // =================================================================
    public void HandleUiSequenceFailed()
    {
        if (runSession == null)
        {
            Debug.LogWarning("[GameFlowController] RunSessionState manquant.");
            return;
        }

        // On masque le panneau de stats détaillées (EndLevelUI),
        // mais on va quand même afficher un résumé score / best / new best.
        if (endLevelUI != null)
            endLevelUI.HideStatsPanel();

        // On enlève du Hull à la run actuelle (runtime) selon la config
        if (hullLossOnFailure > 0)
            runSession.RemoveHull(hullLossOnFailure);

        // Synchronise immédiatement la persistance (RunStateData) avec la nouvelle valeur de vies
        SyncPersistentRunOnDefeat();

        if (runSession.Hull <= 0)
        {
            // Plus de vies : vraie fin de run -> GAME OVER
            Debug.Log("GAME OVER");

            OnGameOver?.Invoke();

            // Résumé de campagne (score de run vs best run), uniquement en GAME OVER
            SetupRunScoreSummary(gameOverRunScoreSummary);

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
    // Appelée quand EndLevelUI a fini toute sa séquence de victoire.
    // =================================================================
    public void HandleVictory()
    {
        Debug.Log("[GameFlowController] HandleVictory()");

        if (defeatGameOverUI != null)
            defeatGameOverUI.HideAll();

        SyncPersistentRunOnVictory();

        if (victoryUI != null)
            victoryUI.Show();
    }



    // =================================================================
    // BOUTONS (branchés sur DefeatGameOverUI.OnRetryRequested / OnMenuRequested)
    // =================================================================

    /// <summary>
    /// Retry depuis DEFEAT : on garde les vies actuelles de la run.
    /// Côté persistance, les vies ont déjà été synchronisées dans HandleUiSequenceFailed.
    /// On demande simplement à RunSessionBootstrapper de réutiliser les vies actuelles.
    /// </summary>
    public void RetryLevelKeepHull()
    {
        if (runSession != null)
            runSession.MarkCarryHullOnNextRestart(true);

        ReloadCurrentLevel();
    }

    /// <summary>
    /// Retry depuis GAME OVER : reset complet des vies selon la définition du vaisseau
    /// et réinitialisation de l'état de la run persistante.
    /// </summary>
    public void RetryLevelResetHull()
    {
        if (runSession != null)
            runSession.MarkCarryHullOnNextRestart(false);

        ResetPersistentRunForNewAttempt();
        ReloadCurrentLevel();
    }

    /// <summary>
    /// Retour au Title.
    /// Optionnel : on marque le level comme non en cours dans la persistance.
    /// La run peut rester active ou non suivant ta logique future (campagne multi-niveaux).
    /// Pour l'instant, on se contente de s'assurer que levelInProgress est false.
    /// </summary>
    public void ReturnToTitle()
    {
        MarkLevelNotInProgressInSave();

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

    /// <summary>
    /// Retry demandé depuis l'overlay DEFEAT (le joueur a encore des vies).
    /// </summary>
    private void HandleRetryDefeatRequested()
    {
        RetryLevelKeepHull();
    }

    /// <summary>
    /// Retry demandé depuis l'overlay GAME OVER (plus de vies, reset complet).
    /// </summary>
    private void HandleRetryGameOverRequested()
    {
        RetryLevelResetHull();
    }

    /// <summary>
    /// Retour menu demandé depuis l'overlay DEFEAT / GAME OVER.
    /// </summary>
    private void HandleMenuRequested()
    {
        ReturnToTitle();
    }


    // =================================================================
    // PERSISTANCE : SYNC DEFAITE / VICTOIRE / RESET
    // =================================================================

    /// <summary>
    /// Synchronise RunStateData après une défaite :
    /// - copie les vies runtime (RunSessionState.Lives) vers remainingLivesInRun
    /// - marque levelInProgress = false (le niveau est fini)
    /// - si vies <= 0, marque hasOngoingRun = false (fin de run)
    /// </summary>
    private void SyncPersistentRunOnDefeat()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var run = SaveManager.Instance.Current.runState;
        if (run == null || !run.hasOngoingRun)
            return;

        int lives = Mathf.Max(0, runSession != null ? runSession.Hull : run.remainingHullInRun);
        run.remainingHullInRun = lives;

        // Le niveau est terminé (raté), donc il n'est plus "en cours"
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        // Si plus de vies, la run est terminée
        if (lives <= 0)
        {
            run.hasOngoingRun = false;
        }

        SaveManager.Instance.Save();

        Debug.Log("[GameFlowController] Persisted defeat. Lives=" + lives
                  + ", hasOngoingRun=" + run.hasOngoingRun);
    }

    /// <summary>
    /// Synchronise RunStateData après une victoire :
    /// - copie les vies runtime vers remainingLivesInRun
    /// - marque levelInProgress = false
    /// - incrémente levelsClearedInRun
    /// - met à jour le score de monde et de run avec le score du niveau complété.
    /// </summary>
    private void SyncPersistentRunOnVictory()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var save = SaveManager.Instance.Current;
        var run = save.runState;
        if (run == null || !run.hasOngoingRun)
            return;

        int lives = Mathf.Max(0, runSession != null ? runSession.Hull : run.remainingHullInRun);
        run.remainingHullInRun = lives;

        // Le niveau vient d'être complété avec succès : il n'est plus en cours
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        // On marque qu'un niveau supplémentaire a été complété dans ce run
        run.levelsClearedInRun = Mathf.Max(0, run.levelsClearedInRun) + 1;

        // Mise à jour du score de run uniquement (plus de score de "monde")
        int levelScore = endLevelUI != null ? endLevelUI.GetFinalScore() : 0;
        if (levelScore > 0)
        {
            run.currentRunScore = Mathf.Max(0, run.currentRunScore) + levelScore;
        }

        SaveManager.Instance.Save();

        Debug.Log("[GameFlowController] Persisted victory. Hull=" + lives
                  + ", levelsClearedInRun=" + run.levelsClearedInRun
                  + ", runScore=" + run.currentRunScore);
    }




    /// <summary>
    /// Réinitialise la run persistante pour un nouveau départ après un GAME OVER.
    /// - remet hasOngoingRun = true
    /// - reset les vies à partir du vaisseau sélectionné
    /// - remet le compteur de niveaux complétés et le score de run à zéro
    /// - remet currentWorld/currentLevelIndex/currentLevelId sur le premier niveau (W1-L1 pour l'instant)
    /// </summary>
    private void ResetPersistentRunForNewAttempt()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var save = SaveManager.Instance.Current;

        if (save.runState == null)
            save.runState = new RunStateData();

        var run = save.runState;

        // Récupère le vaisseau courant via RunConfig / ShipCatalog
        int startingLives = 3;
        string shipId = "CORE_SCOUT";

        if (RunConfig.Instance != null)
            shipId = string.IsNullOrEmpty(RunConfig.Instance.SelectedShipId)
                ? "CORE_SCOUT"
                : RunConfig.Instance.SelectedShipId;

        if (ShipCatalogService.Catalog != null &&
            ShipCatalogService.Catalog.ships != null &&
            ShipCatalogService.Catalog.ships.Count > 0)
        {
            var ship = ShipCatalogService.Catalog.ships.Find(s => s.id == shipId);
            if (ship != null)
                startingLives = Mathf.Max(0, ship.maxHull);
        }

        // Nouvelle run
        run.hasOngoingRun = true;
        run.currentShipId = shipId;

        // Monde 1, niveau 0, W1-L1 pour l'instant (structure de campagne simple)
        run.currentWorld = 1;
        run.currentLevelIndex = 0;
        run.currentLevelId = "W1-L1";

        // Vies de départ et reset de la progression
        run.remainingHullInRun = startingLives;
        run.currentRunScore = 0;
        run.levelsClearedInRun = 0;

        // Le level n'est pas encore en cours : RunSessionBootstrapper marquera levelInProgress = true à l'entrée dans la scène
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        SaveManager.Instance.Save();

        Debug.Log("[GameFlowController] Persistent run reset for new attempt. Lives=" + startingLives
                  + ", shipId=" + shipId + ", levelId=" + run.currentLevelId);
    }

    /// <summary>
    /// Marque simplement levelInProgress = false dans la persistance,
    /// utilisé lorsqu'on quitte le niveau pour retourner au Title.
    /// </summary>
    private void MarkLevelNotInProgressInSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var run = SaveManager.Instance.Current.runState;
        if (run == null)
            return;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;
        SaveManager.Instance.Save();
    }

   
    /// <summary>
    /// Met à jour le LevelScoreSummaryUI pour le score de campagne (run) :
    /// - lit le score total de la run (currentRunScore)
    /// - compare au best run score persisté
    /// - indique si un nouveau record de run est atteint.
    /// </summary>
    private void SetupRunScoreSummary(LevelScoreSummaryUI targetSummary)
    {
        if (targetSummary == null)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var save = SaveManager.Instance.Current;
        if (save.runState == null)
            return;

        var run = save.runState;

        int runScore = run.currentRunScore;

        int bestBefore = SaveManager.Instance.GetBestRunScore();
        int bestAfter = bestBefore;
        bool isNewBest = false;

        if (runScore > 0)
        {
            // Même logique que pour les levels, mais appliquée au score de run.
            isNewBest = SaveManager.Instance.TryUpdateBestRunScore(runScore);
            bestAfter = SaveManager.Instance.GetBestRunScore();
        }

        int bestForDisplay = isNewBest ? bestAfter : bestBefore;

        targetSummary.Setup(runScore, bestForDisplay, isNewBest);
    }

}
