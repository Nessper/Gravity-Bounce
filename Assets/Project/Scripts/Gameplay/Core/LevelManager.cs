using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Orchestrateur du niveau :
/// - charge la config JSON
/// - initialise score, vies, timer, spawner
/// - pilote le flow : intro -> compte à rebours -> gameplay -> évacuation -> fin de niveau
/// - calcule le résultat (objectif principal) et émet OnEndComputed pour l'UI de fin.
/// 
/// IMPORTANT :
/// - Il ne gère plus lui-même la perte de vie sur échec (GameFlowController + RunSessionState s'en chargent).
/// - Il ne parle plus directement à EndLevelUI, il se contente d'émettre OnEndComputed.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ----------------------------------------------------------
    // RÉFÉRENCES GAMEPLAY
    // ----------------------------------------------------------

    [Header("Gameplay")]
    [SerializeField] private PlayerController player;               // Contrôle du paddle
    [SerializeField] private CloseBinController closeBinController; // Contrôle de fermeture des bacs
    [SerializeField] private LevelTimer levelTimer;                 // Timer principal du niveau
    [SerializeField] private ScoreManager scoreManager;             // Gestion du score et des stats
    [SerializeField] private BallSpawner ballSpawner;               // Spawner de billes (3 phases + évacuation)
    [SerializeField] private BinCollector collector;                // Gestion des flushs (collecte des billes)
    [SerializeField] private EndSequenceController endSequence;     // Phase d’évacuation et fin de niveau
    [SerializeField] private ObstacleManager obstacleManager;       // Gestion des obstacles

    // ----------------------------------------------------------
    // RÉFÉRENCES UI
    // ----------------------------------------------------------

    [Header("UI / Overlays")]
    [SerializeField] private IntroLevelUI levelIntroUI;             // Écran d’intro du niveau (titre, texte)
    [SerializeField] private CountdownUI countdownUI;               // Compte à rebours "3-2-1" + compte à rebours évacuation
    [SerializeField] private PauseController pauseController;       // Système de pause
    [SerializeField] private LevelIdUI levelIdUI;                   // Affichage ID du niveau (W1-L1, etc.)
    [SerializeField] private ScoreUI scoreUI;                       // Affichage du score en HUD
    [SerializeField] private LivesUI livesUI;                       // Affichage des vies restantes
    [SerializeField] private ProgressBarUI progressBarUI;           // Barre de progression des billes
    [SerializeField] private PhaseBannerUI phaseBannerUI;           // Bannière de phase (Intro, Tension, Evacuation, etc.)

    [Header("UI - Fin de niveau")]
    [SerializeField] private EndLevelUI endLevelUI;                 // UI de fin de niveau (stats, objectifs, combos...)

    // ----------------------------------------------------------
    // ENVIRONNEMENT / VAISSEAU DE FOND
    // ----------------------------------------------------------

    [Header("Environment / Ship Background")]
    [SerializeField] private ShipBackgroundLoader shipBackgroundLoader; // Charge et affiche le vaisseau de fond (décor sous le plateau)

    // ----------------------------------------------------------
    // CONFIGURATION & ÉTAT
    // ----------------------------------------------------------

    [Header("Config / State")]
    [SerializeField] private TextAsset levelJson;                   // Fichier JSON du niveau (LevelData)
    [SerializeField] private RunSessionState runSession;            // État de la session (vies, etc.)

    private PhasePlanInfo[] phasePlanInfos;                         // Plan de phases exposé par le BallSpawner (durée, quota, interval, nom).
    private LevelData data;                                         // Données du niveau parsées depuis le JSON
    private string levelID;                                         // ID du niveau (copie de data.LevelID)
    private float runDurationSec;                                   // Durée du niveau (dépend du vaisseau)
    private bool endSequenceRunning;                                // Pour éviter plusieurs fins de niveau

    // Nom de fichier de l'image du vaisseau sélectionné (pour le décor de fond).
    // Récupéré dans ResolveShipStats à partir du ShipDefinition.
    private string currentShipImageFile;

    // --- Objectifs secondaires ---
    // Manager logique des objectifs secondaires pour ce niveau.
    // Ne modifie pas le gameplay en temps réel, ne sert qu'à suivre la progression
    // et à produire des résultats en fin de niveau.
    private SecondaryObjectivesManager secondaryObjectivesManager =
        new SecondaryObjectivesManager();

    // Résultats calculés en fin de niveau, utilisables par l'UI de fin.
    private List<SecondaryObjectiveResult> secondaryObjectiveResults;

    /// <summary>
    /// Event émis quand le niveau est terminé et que les stats sont prêtes.
    /// EndLevelUI s'abonne à cet event pour afficher la séquence de fin.
    /// </summary>
    public UnityEvent<EndLevelStats, LevelData, MainObjectiveResult> OnEndComputed
        = new UnityEvent<EndLevelStats, LevelData, MainObjectiveResult>();

    // =====================================================================
    // CYCLE UNITY
    // =====================================================================

    private void OnEnable()
    {
        // On écoute la fin du timer pour déclencher la phase d’évacuation
        if (levelTimer != null)
            levelTimer.OnTimerEnd += HandleTimerEnd;

        // On écoute les changements de vies pour mettre à jour la UI (RunSessionState -> HUD)
        if (runSession != null)
            runSession.OnLivesChanged.AddListener(HandleLivesChanged);

        // On écoute les flushs enregistrés par le ScoreManager
        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered += HandleFlushSnapshotRegistered;
    }


    private void OnDisable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd -= HandleTimerEnd;

        if (runSession != null)
            runSession.OnLivesChanged.RemoveListener(HandleLivesChanged);

        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushSnapshotRegistered;
    }


    private void Start()
    {
        // 1) Charger la config du niveau depuis le JSON
        LoadLevelConfig();

        // 2) Configurer les objectifs secondaires (à partir de LevelData.SecondaryObjectives)
        SetupSecondaryObjectives();

        // 3) Configurer le score et la durée (vies déjà gérées par RunSessionBootstrapper)
        SetupScoreAndLives();

        // 4) Configurer le timer (durée du niveau)
        SetupTimer();

        // 5) Configurer le spawner + la barre de progression
        SetupSpawnerAndProgress();

        //6) Configurer les obstacles
        SetupObstacles();

        // 7) Configurer la séquence d’évacuation (après la fin du timer)
        SetupEvacuationSequence();

        // 8) Afficher l’intro de niveau ou démarrer direct s’il n’y en a pas
        SetupIntroOrAutoStart();
    }

    // =====================================================================
    // SETUP
    // =====================================================================

    /// <summary>
    /// Charge le JSON du niveau et renseigne LevelData + LevelID + LevelIdUI.
    /// </summary>
    private void LoadLevelConfig()
    {
        if (levelJson == null)
        {
            Debug.LogError("[LevelManager] Aucun JSON assigné !");
            return;
        }

        data = JsonUtility.FromJson<LevelData>(levelJson.text);
        if (data == null)
        {
            Debug.LogError("[LevelManager] Erreur de parsing JSON.");
            return;
        }

        levelID = data.LevelID;
        levelIdUI?.SetLevelId(levelID);
    }

    /// <summary>
    /// Initialise le manager d'objectifs secondaires à partir du LevelData.
    /// Ne fait rien si aucun objectif secondaire n'est défini.
    /// </summary>
    private void SetupSecondaryObjectives()
    {
        secondaryObjectiveResults = null;

        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        secondaryObjectivesManager.Setup(data.SecondaryObjectives);
    }

    /// <summary>
    /// Initialise le score et la durée du niveau.
    /// Les vies sont désormais initialisées par RunSessionBootstrapper
    /// (RunStateData.remainingLivesInRun -> RunSessionState).
    /// Ici, on se contente de :
    /// - brancher ScoreManager sur la UI,
    /// - remettre le score à zéro,
    /// - déterminer la durée du niveau à partir du vaisseau (et éventuellement MainQuickStart),
    /// - récupérer le fichier image du vaisseau pour le décor de fond.
    /// </summary>
    private void SetupScoreAndLives()
    {
        // Branche le ScoreManager sur la UI de score
        if (scoreManager != null && scoreUI != null)
            scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);

        // Score remis à zéro pour ce niveau
        scoreManager?.ResetScore(0);

        if (runSession == null)
        {
            Debug.LogError("[LevelManager] RunSessionState non assigné.");

            // On récupère quand même la durée du niveau pour le timer
            int dummyLives;
            ResolveShipStats(out dummyLives, out runDurationSec);
            return;
        }

        // On détermine la durée du niveau à partir du vaisseau sélectionné
        int unusedLives;
        ResolveShipStats(out unusedLives, out runDurationSec);

        // Charge le vaisseau de fond si possible (image liée au vaisseau sélectionné)
        if (shipBackgroundLoader != null && !string.IsNullOrEmpty(currentShipImageFile))
        {
            shipBackgroundLoader.Init(currentShipImageFile);
        }

        // MainQuickStart (outil debug) peut overrider la durée
        var quick = Object.FindFirstObjectByType<MainQuickStart>();
        if (quick != null && quick.enabled && quick.gameObject.activeInHierarchy)
        {
            if (quick.forcedTimerSec > 0f)
                runDurationSec = quick.forcedTimerSec;

            Debug.Log($"[LevelManager] QuickStart active — Timer={runDurationSec}s");
        }

        // Les vies seront gérées par RunSessionBootstrapper et propagées à la HUD
        // via runSession.OnLivesChanged -> HandleLivesChanged -> livesUI.SetLives(...)
    }


    /// <summary>
    /// Lit le vaisseau sélectionné dans RunConfig / ShipCatalog,
    /// renvoie le nombre de vies et la durée de bouclier pour ce niveau.
    /// Met aussi à jour currentShipImageFile pour le décor de fond.
    /// </summary>
    private void ResolveShipStats(out int lives, out float durationSec)
    {
        lives = 0;
        durationSec = 0f;
        currentShipImageFile = null;

        var run = RunConfig.Instance;
        var catalog = ShipCatalogService.Catalog;

        if (run == null || catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            Debug.LogWarning("[LevelManager] ShipCatalog manquant, valeurs par défaut (0).");
            return;
        }

        var shipId = string.IsNullOrEmpty(run.SelectedShipId) ? "CORE_SCOUT" : run.SelectedShipId;
        var ship = catalog.ships.Find(s => s.id == shipId);
        if (ship == null)
        {
            Debug.LogWarning("[LevelManager] Vaisseau introuvable : " + shipId);
            return;
        }

        currentShipImageFile = ship.imageFile;

        lives = Mathf.Max(0, ship.lives);
        durationSec = Mathf.Max(0.1f, ship.shieldSecondsPerLevel);
    }

    /// <summary>
    /// Configure le timer du niveau (sans l’activer).
    /// Il sera activé au moment où on démarre réellement le niveau (StartLevel).
    /// </summary>
    private void SetupTimer()
    {
        if (levelTimer == null)
            return;

        levelTimer.enabled = false;
        levelTimer.StartTimer(runDurationSec);
    }

    /// <summary>
    /// Configure le spawner à partir du LevelData et branche la ProgressBar
    /// via les callbacks OnPlannedReady (total prévu) et OnActivated (bille activée).
    /// </summary>
    private void SetupSpawnerAndProgress()
    {
        if (ballSpawner == null || data == null)
            return;

        ballSpawner.OnPlannedReady += total =>
        {
            // Informe le ScoreManager du nombre de billes prévues
            scoreManager?.SetPlannedBalls(total);

            // Configure la barre de progression avec total + objectif principal
            if (progressBarUI != null && data != null)
            {
                int threshold = data.MainObjective != null ? data.MainObjective.ThresholdCount : 0;
                progressBarUI.Configure(total, threshold);
                progressBarUI.Refresh();

                if (scoreManager != null)
                {
                    scoreManager.SetObjectiveThreshold(threshold);
                }
            }
        };

        ballSpawner.OnActivated += _ =>
        {
            // À chaque bille activée, on rafraîchit la barre
            progressBarUI?.Refresh();
        };

        // Applique la config du JSON (phases, mix, angles...) et pré-alloue des billes
        ballSpawner.ConfigureFromLevel(data, runDurationSec);
        ballSpawner.StartPrewarm(256);

        // Récupère le plan de phases calculé par le spawner (pour l'UI d'intro).
        phasePlanInfos = ballSpawner.GetPhasePlans();
    }

    /// <summary>
    /// Construit les obstacles définis dans le JSON en utilisant l'ObstacleManager.
    /// Ne fait rien si pas de data ou pas d'obstacles.
    /// </summary>
    private void SetupObstacles()
    {
        if (obstacleManager == null)
        {
            return;
        }

        if (data == null || data.Obstacles == null || data.Obstacles.Length == 0)
        {
            return;
        }

        obstacleManager.BuildObstacles(data.Obstacles);
    }


    /// <summary>
    /// Prépare la phase d’évacuation après la fin du timer :
    /// - coupe les contrôles au début
    /// - configure EndSequenceController avec durée d'évacuation, callbacks, etc.
    /// </summary>
    private void SetupEvacuationSequence()
    {
        // On s’assure que le joueur ne peut pas jouer avant le vrai départ
        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);
        pauseController?.EnablePause(false);

        float evacDuration = (data != null)
            ? Mathf.Max(0.1f, data.Evacuation.DurationSec)
            : 10f;

        string evacName = (data != null) ? data.Evacuation.Name : null;

        endSequence?.Configure(
            collector,
            player,
            closeBinController,
            pauseController,
            evacDuration: evacDuration,
            tickInterval: 1f,
            onEvacStartCb: () =>
            {
                // Affiche le nom de la phase d'évacuation + lance un countdown visuel si dispo
                if (!string.IsNullOrWhiteSpace(evacName))
                    phaseBannerUI?.ShowPhaseText(evacName, evacDuration);

                if (countdownUI != null)
                    StartCoroutine(countdownUI.PlayCountdownSeconds(evacDuration));
            },
            onEvacTickCb: null
        );
    }

    /// <summary>
    /// Gère le flux d’intro : affiche IntroLevelUI si dispo, et
    /// appelle StartLevel après le compte à rebours.
    /// Si pas d’intro, on lance directement StartLevel.
    /// </summary>
    private void SetupIntroOrAutoStart()
    {
        if (levelIntroUI != null && data != null)
        {
            levelIntroUI.Show(
                data,
                phasePlanInfos, // nouveau param : plan de phases calculé par le spawner
                onPlay: () =>
                {
                    levelIntroUI.Hide();

                    if (countdownUI != null)
                    {
                        // Compte à rebours "3-2-1" puis démarrage du niveau
                        StartCoroutine(countdownUI.PlayCountdown(() =>
                        {
                            EnableGameplayControls();
                            StartLevel();
                        }));
                    }
                    else
                    {
                        EnableGameplayControls();
                        StartLevel();
                    }
                },
                onBack: () =>
                {
                    // Évite de rejouer l’intro du title
                    if (RunConfig.Instance != null)
                        RunConfig.Instance.SkipTitleIntroOnce = true;

                    // Retour propre au menu Title
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
                }

            );
        }
        else
        {
            // Pas d’intro -> démarrage direct
            EnableGameplayControls();
            StartLevel();
        }

    }

    /// <summary>
    /// Active les contrôles joueur et la pause.
    /// </summary>
    private void EnableGameplayControls()
    {
        player?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);
        pauseController?.EnablePause(true);
    }

    // =====================================================================
    // BOUCLE DE JEU
    // =====================================================================

    /// <summary>
    /// Point d’entrée réel du niveau :
    /// - remet la timeScale
    /// - reset la séquence de fin
    /// - active le timer
    /// - lance le spawner
    /// </summary>
    public void StartLevel()
    {
        Time.timeScale = 1f;
        endSequenceRunning = false;

        endSequence?.ResetState();
        endLevelUI?.Hide();

        if (levelTimer != null)
            levelTimer.enabled = true;

        ballSpawner?.StartSpawning();
        MarkLevelStartedInSave();
    }

    /// <summary>
    /// Appelé quand le timer du niveau arrive à 0 :
    /// stoppe le spawner et lance la phase d’évacuation contrôlée par EndSequenceController.
    /// </summary>
    private void HandleTimerEnd()
    {
        if (endSequenceRunning)
            return;

        ballSpawner?.StopSpawning();
        endSequenceRunning = true;

        endSequence?.BeginEvacuationPhase(() =>
        {
            StartCoroutine(EndOfLevelFinalizeRoutine());
        });
    }

    /// <summary>
    /// Routine de fin :
    /// 1) flush forcé des bacs
    /// 2) balayage final pour marquer les billes restantes comme perdues
    /// 3) log des stats du spawner
    /// 4) évaluation du résultat (objectif principal) + event OnEndComputed
    /// </summary>
    private IEnumerator EndOfLevelFinalizeRoutine()
    {
        collector?.CollectAll(force: true, skipDelay: true);

        yield return StartCoroutine(FinalSweepMarkLostAndRecycle(ballSpawner, scoreManager));

        ballSpawner?.LogStats();

        EvaluateLevelResult();
    }

    /// <summary>
    /// Balayage final après la phase d’évacuation :
    /// tout ce qui reste actif et non collected est compté comme perdu et recyclé.
    /// </summary>
    private IEnumerator FinalSweepMarkLostAndRecycle(BallSpawner spawner, ScoreManager score)
    {
        // Laisse 2 frames pour laisser le flush forcé terminer ses events
        yield return null;
        yield return null;

#if UNITY_6000_0_OR_NEWER
        var balls = UnityEngine.Object.FindObjectsByType<BallState>(FindObjectsSortMode.None);
#else
        var balls = UnityEngine.Object.FindObjectsOfType<BallState>();
#endif

        foreach (var st in balls)
        {
            if (st == null)
                continue;

            var go = st.gameObject;
            if (!go.activeInHierarchy)
                continue;

            if (st.collected)
            {
                // Billes déjà collectées : on les recycle comme collected
                spawner?.Recycle(go, collected: true);
                continue;
            }

            if (st.inBin)
                continue; // Sécurité contre les cas limites

            // Tout le reste est considéré comme perdu
            score?.RegisterLost(st.TypeName);
            spawner?.Recycle(go, collected: false);
        }
    }

    // =====================================================================
    // ÉVALUATION FINALE & EVENT
    // =====================================================================

    /// <summary>
    /// Calcule si l’objectif principal est atteint ou non, construit
    /// les EndLevelStats et MainObjectiveResult, puis émet OnEndComputed.
    /// L’UI de fin (EndLevelUI) réagit à cet event.
    /// Les objectifs secondaires sont évalués ici, mais leur affichage
    /// sera géré plus tard dans l'UI de fin.
    /// </summary>
    private void EvaluateLevelResult()
    {
        if (scoreManager == null || data == null)
        {
            Debug.LogWarning("[LevelManager] ScoreManager ou données manquantes.");
            return;
        }

        int spawnedPlan = scoreManager.TotalBillesPrevues;
        int spawnedReal = scoreManager.GetRealSpawned();
        int collected = scoreManager.TotalBilles;

        int spawnedForEval = spawnedReal > 0 ? spawnedReal : spawnedPlan;
        if (spawnedForEval <= 0)
        {
            Debug.LogWarning("[LevelManager] Aucune bille, évaluation ignorée.");
            return;
        }

        // Objectif fixé par le JSON (ThresholdCount)
        int required = Mathf.Max(0, data.MainObjective?.ThresholdCount ?? 0);
        bool success = collected >= required;

        int elapsed = Mathf.RoundToInt(levelTimer != null ? levelTimer.GetElapsedTime() : 0f);
        var stats = scoreManager.BuildEndLevelStats(elapsed);

        var mainObj = new MainObjectiveResult
        {
            Text = data.MainObjective?.Text ?? string.Empty,
            ThresholdPct = 0, // on ne travaille plus en pourcentage ici
            Required = required,
            Collected = collected,
            Achieved = success,
            BonusApplied = (success && data.MainObjective != null) ? data.MainObjective.Bonus : 0
        };

        // Évaluation des objectifs secondaires (si définis)
        if (data.SecondaryObjectives != null && data.SecondaryObjectives.Length > 0)
        {
            secondaryObjectiveResults = secondaryObjectivesManager.BuildResults();

            // Optionnel : on peut loguer pour debug.
            int totalReward = secondaryObjectivesManager.GetTotalRewardScore();
            if (totalReward > 0)
            {
                Debug.Log("[LevelManager] Secondary objectives reward total = " + totalReward);
            }

            // IMPORTANT : on ne touche pas encore au score final ici.
            // L'utilisation de AwardedScore sera gérée plus tard dans la cérémonie.
        }
        else
        {
            secondaryObjectiveResults = null;
        }

        // ===================================================================================
        // COMBOS FINAUX (PerfectRun, CombosCollector, etc.)
        // ===================================================================================

        // On s'assure que la liste des combos finaux est bien initialisée / vidée.
        if (stats.Combos == null)
            stats.Combos = new List<EndLevelStats.ComboCalc>();
        else
            stats.Combos.Clear();

        // Contexte pour l'évaluation des combos finaux.
        // On utilise les stats déjà calculées : temps écoulé et total de billes.
        var finalCtx = new FinalComboContext
        {
            timeElapsedSec = stats.TimeElapsedSec,
            totalBilles = stats.BallsCollected + stats.BallsLost
        };

        // Evaluation des combos finaux à partir du ScoreManager.
        var finalCombos = FinalComboEvaluator.Evaluate(scoreManager, finalCtx);

        if (finalCombos != null && finalCombos.Count > 0)
        {
            for (int i = 0; i < finalCombos.Count; i++)
            {
                var fc = finalCombos[i];

                // On prépare une ligne de combo pour EndLevelStats,
                // en utilisant pour l'instant un multiplicateur fixe (1f).
                var comboLine = new EndLevelStats.ComboCalc
                {
                    // On stocke l'identifiant technique du combo (ex: "PerfectRun", "WhiteMaster").
                    // La traduction en texte lisible est faite côté EndLevelUI via FinalComboStyleProvider.
                    Label = fc.id,
                    Base = fc.points,
                    Mult = 1f,
                    Total = fc.points
                };

                stats.Combos.Add(comboLine);

            }
        }

        // ===================================================================================
        // EVENT DE FIN
        // ===================================================================================


        OnEndComputed.Invoke(stats, data, mainObj);
    }

    // =====================================================================
    // CALLBACKS & UTILITAIRES
    // =====================================================================

    /// <summary>
    /// Réagit aux changements de vies dans RunSessionState.
    /// Met à jour la HUD de vies.
    /// </summary>
    private void HandleLivesChanged(int lives)
    {
        livesUI?.SetLives(lives);
    }

    /// <summary>
    /// Renvoie l’ID du niveau courant (utilisé ailleurs si besoin).
    /// </summary>
    public string GetLevelID()
    {
        return data != null ? data.LevelID : levelID;
    }

    /// <summary>
    /// Renvoie la liste des résultats d'objectifs secondaires calculés en fin de niveau.
    /// Peut être null si aucun objectif secondaire n'est défini.
    /// </summary>
    public List<SecondaryObjectiveResult> GetSecondaryObjectiveResults()
    {
        return secondaryObjectiveResults;
    }

    /// <summary>
    /// Callback lorsqu'un flush est enregistre par le ScoreManager.
    /// Utilise le snapshot pour mettre a jour les objectifs secondaires
    /// de type "BallCount".
    /// </summary>
    private void HandleFlushSnapshotRegistered(BinSnapshot snapshot)
    {
        // Si aucun objectif secondaire n'est defini, on ne fait rien.
        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        if (snapshot == null || snapshot.parType == null)
            return;

        // Pour chaque type de bille collecte dans ce flush
        foreach (var kv in snapshot.parType)
        {
            string ballType = kv.Key;
            int count = kv.Value;

            // Objectifs "BallCount" : on compte chaque bille individuellement.
            for (int i = 0; i < count; i++)
            {
                secondaryObjectivesManager.OnBallCollected(ballType);
            }
        }
    }

    /// <summary>
    /// Notification externe lorsqu'un combo est déclenché.
    /// Utilisé par ComboEngine pour informer les objectifs secondaires de type ComboCount.
    /// </summary>
    public void NotifyComboTriggered(string comboId)
    {
        // Si aucun objectif secondaire n'est défini, on ne fait rien.
        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        secondaryObjectivesManager.OnComboTriggered(comboId);
    }

    /// <summary>
    /// Marque dans la sauvegarde qu'un level est réellement en cours.
    /// Cela arme la pénalité d'abandon : si le jeu se ferme sans fin officielle
    /// (victoire/défaite), une vie sera retirée au prochain boot.
    /// </summary>
    private void MarkLevelStartedInSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var run = SaveManager.Instance.Current.runState;
        if (run == null)
            return;

        // On considère qu'il y a une run en cours si on est dans un niveau jouable.
        run.hasOngoingRun = true;
        run.levelInProgress = true;
        run.abortPenaltyArmed = true;

        SaveManager.Instance.Save();
    }

}
