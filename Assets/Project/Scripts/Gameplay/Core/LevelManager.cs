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
    [SerializeField] private LevelControlsController controlsController; // Gestion des Inputs

    // ----------------------------------------------------------
    // RÉFÉRENCES UI
    // ----------------------------------------------------------

    [Header("UI / Overlays")]
    [SerializeField] private LevelBriefingController briefingController; // Gère le briefing (overlay d'intro)
    [SerializeField] private CountdownUI countdownUI;                    // Compte à rebours "3-2-1" + compte à rebours évacuation
    [SerializeField] private PauseController pauseController;            // Système de pause
    [SerializeField] private LevelIdUI levelIdUI;                        // Affichage ID du niveau (W1-L1, etc.)
    [SerializeField] private ScoreUI scoreUI;                            // Affichage du score en HUD
    [SerializeField] private HullUI hullUI;                            // Affichage des vies restantes
    [SerializeField] private ProgressBarUI progressBarUI;                // Barre de progression des billes

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

    [Header("Narration / Intro Sequence")]
    [SerializeField] private LevelIntroSequenceController introSequenceController;

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
            runSession.OnHullChanged.AddListener(HandleHullChanged);

        // On écoute les flushs enregistrés par le ScoreManager
        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered += HandleFlushSnapshotRegistered;
    }

    private void OnDisable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd -= HandleTimerEnd;

        if (runSession != null)
            runSession.OnHullChanged.RemoveListener(HandleHullChanged);

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
        SetupScoreAndHull();

        // 4) Configurer le timer (durée du niveau)
        SetupTimer();

        // 5) Configurer le spawner + la barre de progression
        SetupSpawnerAndProgress();

        // 6) Configurer les obstacles
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
    private void SetupScoreAndHull()
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
    }

    /// <summary>
    /// Lit le vaisseau sélectionné dans RunConfig / ShipCatalog,
    /// renvoie le nombre de vies et la durée de bouclier pour ce niveau.
    /// Met aussi à jour currentShipImageFile pour le décor de fond.
    /// </summary>
    private void ResolveShipStats(out int hull, out float durationSec)
    {
        hull = 0;
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

        hull = Mathf.Max(0, ship.maxHull);
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
    /// 
    /// Désormais, le "total prévu" utilisé pour la progression et l'objectif
    /// est le nombre de billes NON NOIRES prévues (BallSpawner.PlannedNonBlackSpawnCount).
    /// </summary>
    private void SetupSpawnerAndProgress()
    {
        if (ballSpawner == null || data == null)
            return;

        ballSpawner.OnPlannedReady += _ =>
        {
            if (scoreManager == null)
                return;

            // Nombre de billes prévues HORS NOIRES (plan théorique)
            int plannedNonBlack = ballSpawner.PlannedNonBlackSpawnCount;

            // Informe le ScoreManager du nombre de billes prévues (hors noires)
            scoreManager.SetPlannedBalls(plannedNonBlack);

            // Récupère le seuil d'objectif principal (en nombre de billes non noires)
            int threshold = data.MainObjective != null ? data.MainObjective.ThresholdCount : 0;

            // Configure la barre de progression avec total (hors noires) + objectif principal
            if (progressBarUI != null)
            {
                progressBarUI.Configure(plannedNonBlack, threshold);
                progressBarUI.Refresh();
            }

            // Configure l'objectif principal dans le ScoreManager (seuil en non-noires)
            scoreManager.SetObjectiveThreshold(threshold);
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
    /// Désactive tous les contrôles de gameplay.
    /// Si un LevelControlsController est configuré, on lui délègue,
    /// sinon on retombe sur l'ancien comportement direct.
    /// </summary>
    private void DisableGameplayControls()
    {
        if (controlsController != null)
        {
            controlsController.DisableGameplayControls();
            return;
        }

        // Fallback legacy (si jamais controlsController n'est pas assigné)
        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);
    }

    private void EnableGameplayControlsInternal()
    {
        if (controlsController != null)
        {
            controlsController.EnableGameplayControls();
            return;
        }

        // Fallback legacy
        player?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);
    }

    /// <summary>
    /// Prépare la phase d’évacuation après la fin du timer :
    /// - coupe les contrôles au début
    /// - configure EndSequenceController avec durée d'évacuation, callbacks, etc.
    /// </summary>
    private void SetupEvacuationSequence()
    {
        // On s’assure que le joueur ne peut pas jouer avant le vrai départ
        DisableGameplayControls();

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
                if (countdownUI != null)
                    StartCoroutine(countdownUI.PlayCountdownSeconds(evacDuration));
            },
            onEvacTickCb: null
        );
    }

    /// <summary>
    /// Gère le flux d'intro :
    /// - affiche le briefing (LevelIntroOverlay) via LevelBriefingController
    /// - une fois le briefing terminé (Play), lance la sequence de début de niveau
    ///   (dialogues d'intro + compte à rebours + StartLevel).
    /// Le briefing est obligatoire : s'il n'est pas correctement configuré, on log une erreur
    /// et on enchaîne directement sur la sequence de début.
    /// </summary>
    private void SetupIntroOrAutoStart()
    {
        if (briefingController != null && data != null)
        {
            briefingController.Show(
                data,
                phasePlanInfos,
                onPlay: () =>
                {
                    if (introSequenceController != null)
                    {
                        introSequenceController.Play(() =>
                        {
                            StartLevel();
                        });
                    }
                    else
                    {
                        Debug.LogWarning("[LevelManager] Aucun LevelIntroSequenceController assigné. Demarrage direct du niveau.");
                        StartLevel();
                    }
                },
                onBack: null
            );
        }
        else
        {
            Debug.LogError("[LevelManager] Aucun LevelBriefingController assigné alors que le briefing est obligatoire. Demarrage direct du niveau sans intro.");
            StartLevel();
        }
    }

    /// <summary>
    /// Active les contrôles joueur et la pause.
    /// </summary>
    private void EnableGameplayControls()
    {
        EnableGameplayControlsInternal();
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
                spawner?.Recycle(go, collected: true);
                continue;
            }

            if (st.inBin)
                continue;

            score?.RegisterLost(st.TypeName);
            spawner?.Recycle(go, collected: false);
        }
    }

    // =====================================================================
    // ÉVALUATION FINALE & EVENT
    // =====================================================================

    private void EvaluateLevelResult()
    {
        if (scoreManager == null || data == null)
        {
            Debug.LogWarning("[LevelManager] ScoreManager ou LevelData manquants, evaluation impossible.");
            return;
        }

        int elapsed = Mathf.RoundToInt(levelTimer != null ? levelTimer.GetElapsedTime() : 0f);

        var evalResult = LevelResultEvaluator.Evaluate(
            scoreManager,
            data,
            secondaryObjectivesManager,
            elapsed
        );

        if (evalResult.Stats == null)
        {
            Debug.LogWarning("[LevelManager] Evaluation de fin de niveau invalide (Stats null).");
            return;
        }

        secondaryObjectiveResults = evalResult.SecondaryObjectives;

        OnEndComputed.Invoke(evalResult.Stats, data, evalResult.MainObjective);
    }

    // =====================================================================
    // CALLBACKS & UTILITAIRES
    // =====================================================================

    private void HandleHullChanged(int hull)
    {
        hullUI?.SetHull(hull);
    }

    public string GetLevelID()
    {
        return data != null ? data.LevelID : levelID;
    }

    public List<SecondaryObjectiveResult> GetSecondaryObjectiveResults()
    {
        return secondaryObjectiveResults;
    }

    private void HandleFlushSnapshotRegistered(BinSnapshot snapshot)
    {
        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        if (snapshot == null || snapshot.parType == null)
            return;

        foreach (var kv in snapshot.parType)
        {
            string ballType = kv.Key;
            int count = kv.Value;

            for (int i = 0; i < count; i++)
            {
                secondaryObjectivesManager.OnBallCollected(ballType);
            }
        }
    }

    public void NotifyComboTriggered(string comboId)
    {
        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        secondaryObjectivesManager.OnComboTriggered(comboId);
    }

    private void MarkLevelStartedInSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var run = SaveManager.Instance.Current.runState;
        if (run == null)
            return;

        run.hasOngoingRun = true;
        run.levelInProgress = true;
        run.abortPenaltyArmed = true;

        SaveManager.Instance.Save();
    }
}
