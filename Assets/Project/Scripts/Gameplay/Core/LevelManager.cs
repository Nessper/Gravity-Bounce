using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Orchestrateur du niveau :
/// - charge la config JSON
/// - initialise score, hull, timer, spawner
/// - pilote le flow : intro -> compte à rebours -> gameplay -> évacuation -> fin de niveau
/// - calcule le résultat (objectif principal) et émet OnEndComputed pour l'UI de fin.
/// 
/// IMPORTANT :
/// - Il ne gère plus lui-même la perte de hull sur échec (GameFlowController + RunSessionState s'en chargent).
/// - Il ne parle plus directement à EndLevelUI, il se contente d'émettre OnEndComputed.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ----------------------------------------------------------
    // RÉFÉRENCES GAMEPLAY
    // ----------------------------------------------------------

    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private BinCollector collector;
    [SerializeField] private EndSequenceController endSequence;
    [SerializeField] private ObstacleManager obstacleManager;
    [SerializeField] private LevelControlsController controlsController;
    [SerializeField] private HullSystem hullSystem;
    [SerializeField] private ContractLivesUI contractLivesUI;

    // ----------------------------------------------------------
    // RÉFÉRENCES UI
    // ----------------------------------------------------------

    [Header("UI / Overlays")]
    [SerializeField] private LevelBriefingController briefingController;
    [SerializeField] private CountdownUI countdownUI;
    [SerializeField] private PauseController pauseController;
    [SerializeField] private LevelIdUI levelIdUI;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private HullUI hullUI;
    [SerializeField] private ProgressBarUI progressBarUI;

    // ----------------------------------------------------------
    // ENVIRONNEMENT / VAISSEAU DE FOND
    // ----------------------------------------------------------

    [Header("Environment / Ship Background")]
    [SerializeField] private ShipBackgroundLoader shipBackgroundLoader;

    // ----------------------------------------------------------
    // CONFIGURATION & ÉTAT
    // ----------------------------------------------------------

    [Header("Config / State")]
    [SerializeField] private TextAsset levelJson;
    [SerializeField] private RunSessionState runSession;

    private PhasePlanInfo[] phasePlanInfos;
    private LevelData data;
    private string levelID;
    private float runDurationSec;
    private bool endSequenceRunning;

    // Hull maximal du vaisseau (issu du ShipDefinition / catalog)
    private int maxHull;

    // Dernier Hull connu via RunSessionState.OnHullChanged
    private int lastKnownHull = -1;

    [Header("Narration / Intro Sequence")]
    [SerializeField] private LevelIntroSequenceController introSequenceController;

    // Nom de fichier de l'image du vaisseau sélectionné (pour le décor de fond).
    private string currentShipImageFile;

    // --- Objectifs secondaires ---
    private SecondaryObjectivesManager secondaryObjectivesManager =
        new SecondaryObjectivesManager();

    private List<SecondaryObjectiveResult> secondaryObjectiveResults;

    public UnityEvent<EndLevelStats, LevelData, MainObjectiveResult> OnEndComputed
        = new UnityEvent<EndLevelStats, LevelData, MainObjectiveResult>();

    // =====================================================================
    // CYCLE UNITY
    // =====================================================================

    private void OnEnable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd += HandleTimerEnd;

        if (runSession != null)
        {
            runSession.OnHullChanged.AddListener(HandleHullChanged);
            runSession.OnContractLivesChanged.AddListener(HandleContractLivesChanged);
        }

        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered += HandleFlushSnapshotRegistered;
    }


    private void OnDisable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd -= HandleTimerEnd;

        if (runSession != null)
        {
            runSession.OnHullChanged.RemoveListener(HandleHullChanged);
            runSession.OnContractLivesChanged.RemoveListener(HandleContractLivesChanged);
        }

        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushSnapshotRegistered;
    }


    private void Start()
    {
        // 1) JSON
        LoadLevelConfig();

        // 2) Objectifs secondaires
        SetupSecondaryObjectives();

        // 3) Score + durée + décor de fond
        SetupScoreAndHull();

        // 4) Timer
        SetupTimer();

        // 5) Spawner + ProgressBar
        SetupSpawnerAndProgress();

        // 6) Obstacles
        SetupObstacles();

        // 7) Evacuation
        SetupEvacuationSequence();

        // 7bis) Seed du hull pour être sûr d'avoir une valeur dès le début
        if (runSession != null)
        {
            HandleHullChanged(runSession.Hull);
        }

        //7ter)  On met a jour les contractLives
        SetupContractLivesUI();



        // 8) Intro / briefing
        SetupIntroOrAutoStart();
    }

    // =====================================================================
    // SETUP
    // =====================================================================

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

    private void SetupSecondaryObjectives()
    {
        secondaryObjectiveResults = null;

        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        secondaryObjectivesManager.Setup(data.SecondaryObjectives);
    }

    private void SetupScoreAndHull()
    {
        if (scoreManager != null && scoreUI != null)
            scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);

        scoreManager?.ResetScore(0);

        // Pas de RunSession : on fait ce qu'on peut avec le ship catalog
        if (runSession == null)
        {
            Debug.LogError("[LevelManager] RunSessionState non assigné.");

            ResolveShipStats(out maxHull, out runDurationSec);

            // Même sans runSession, on peut au moins afficher le maxHull
            if (hullSystem != null)
            {
                // On initialise avec hull = maxHull (valeur "pleine" par défaut)
                hullSystem.Initialize(maxHull, maxHull);
            }
            else
            {
                hullUI?.SetMaxHull(maxHull);
                hullUI?.SetHull(maxHull);
            }

            return;
        }

        // Récupère maxHull et durée depuis le vaisseau
        ResolveShipStats(out maxHull, out runDurationSec);

        int hullForHud = runSession.Hull; // ex : 9 après RunRecoveryOnBoot

        // Initialise le système de coque (qui mettra la UI à jour)
        if (hullSystem != null)
        {
            hullSystem.Initialize(hullForHud, maxHull);
        }
        else if (hullUI != null)
        {
            hullUI.SetMaxHull(maxHull);
            hullUI.SetHull(hullForHud);
        }

        if (shipBackgroundLoader != null && !string.IsNullOrEmpty(currentShipImageFile))
        {
            shipBackgroundLoader.Init(currentShipImageFile);
        }

        var quick = Object.FindFirstObjectByType<MainQuickStart>();
        if (quick != null && quick.enabled && quick.gameObject.activeInHierarchy)
        {
            if (quick.forcedTimerSec > 0f)
                runDurationSec = quick.forcedTimerSec;

            Debug.Log($"[LevelManager] QuickStart active — Timer={runDurationSec}s");
        }
    }

    private void SetupContractLivesUI()
    {
        if (contractLivesUI == null || runSession == null)
            return;

        contractLivesUI.SetContractLives(runSession.ContractLives);
    }



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


    private void SetupTimer()
    {
        if (levelTimer == null)
            return;

        levelTimer.enabled = false;
        levelTimer.StartTimer(runDurationSec);
    }

    private void SetupSpawnerAndProgress()
    {
        if (ballSpawner == null || data == null)
            return;

        // Sécurité : éviter double-bind en cas de retry futur
        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushRegistered;

        ballSpawner.OnPlannedReady += _ =>
        {
            if (scoreManager == null)
                return;

            int plannedNonBlack = ballSpawner.PlannedNonBlackSpawnCount;
            scoreManager.SetPlannedBalls(plannedNonBlack);

            int threshold = data.MainObjective != null ? data.MainObjective.ThresholdCount : 0;

            if (progressBarUI != null)
            {
                progressBarUI.Configure(plannedNonBlack, threshold);
            }

            scoreManager.SetObjectiveThreshold(threshold);
            scoreManager.OnFlushSnapshotRegistered += HandleFlushRegistered;
        };

        ballSpawner.ConfigureFromLevel(data, runDurationSec);
        ballSpawner.StartPrewarm(256);

        phasePlanInfos = ballSpawner.GetPhasePlans();
    }

    private void HandleFlushRegistered(BinSnapshot snapshot)
    {
        // Chaque flush met à jour la barre selon TotalNonBlackBilles mis à jour par ScoreManager
        progressBarUI?.Refresh();
    }

    private void SetupObstacles()
    {
        if (obstacleManager == null)
            return;

        if (data == null || data.Obstacles == null || data.Obstacles.Length == 0)
            return;

        obstacleManager.BuildObstacles(data.Obstacles);
    }

    private void DisableGameplayControls()
    {
        if (controlsController != null)
        {
            controlsController.DisableGameplayControls();
            return;
        }

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

        player?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);
    }

    private void SetupEvacuationSequence()
    {
        DisableGameplayControls();

        float evacDuration = (data != null)
            ? Mathf.Max(0.1f, data.Evacuation.DurationSec)
            : 10f;

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
    /// Injecte le hull courant dans le briefing, puis affiche le briefing.
    /// </summary>
    private void SetupIntroOrAutoStart()
    {
        if (briefingController != null && data != null)
        {
            int hullForIntro = lastKnownHull;

            if (hullForIntro < 0 && runSession != null)
            {
                hullForIntro = runSession.Hull;
            }

            if (hullForIntro >= 0)
            {
                int maxHullForUi = maxHull > 0 ? maxHull : hullForIntro;
                briefingController.SetShipRuntimeHull(hullForIntro, maxHullForUi);
            }

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

    private void EnableGameplayControls()
    {
        EnableGameplayControlsInternal();
    }

    // =====================================================================
    // BOUCLE DE JEU
    // =====================================================================

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

    private IEnumerator EndOfLevelFinalizeRoutine()
    {
        // Le flush final est maintenant géré par EndSequenceController
        // (evacuation + CollectAll(force: true, skipDelay: true) + attente).
        // Ici on se contente de faire le balayage final des billes restantes
        // et d'évaluer le résultat du niveau.

        yield return StartCoroutine(FinalSweepMarkLostAndRecycle(ballSpawner, scoreManager));

        ballSpawner?.LogStats();

        EvaluateLevelResult();
    }


    private IEnumerator FinalSweepMarkLostAndRecycle(BallSpawner spawner, ScoreManager score)
    {
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
        // On mémorise la valeur runtime
        lastKnownHull = hull;

        // HUD gameplay via HullSystem si dispo, sinon fallback direct
        if (hullSystem != null)
        {
            hullSystem.SetCurrentHull(hull);
        }
        else
        {
            hullUI?.SetHull(hull);
        }

        // Et on passe la même info au briefing si jamais il est déjà prêt
        if (briefingController != null)
        {
            int maxHullForUi = maxHull > 0 ? maxHull : hull;
            briefingController.SetShipRuntimeHull(hull, maxHullForUi);
        }
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

    private void HandleContractLivesChanged(int lives)
    {
        if (contractLivesUI != null)
        {
            contractLivesUI.SetContractLives(lives);
        }
    }


    private void OnDestroy()
    {
        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushRegistered;
    }


}
