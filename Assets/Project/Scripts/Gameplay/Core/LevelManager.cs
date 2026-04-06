using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// LevelManager = Orchestrateur pur du niveau.
/// 
/// Responsabilites:
/// - Construit le contexte (levelId + meta + data) via LevelBootstrapper
/// - Applique le runtime ship (maxHull + duree) via ShipRuntimeSetup
/// - Configure timer / spawner / obstacles / evacuation
/// - Pilote le flow : briefing -> intro -> gameplay -> evacuation -> finalize -> evaluation
/// - Emet OnEndComputed pour declencher l UI de fin (EndLevelRoot, etc.)
/// 
/// Ce script ne doit pas:
/// - binder des HUD widgets (Hull/ContractLives/Score...) => binders dedies
/// - charger des JSON lui-meme => bootstrapper
/// - gerer la navigation globale => GameFlowController / BootRoot
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Run State")]
    [SerializeField] private RunSessionState runSessionState;

    // ----------------------------------------------------------
    // REFERENCES GAMEPLAY
    // ----------------------------------------------------------

    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private ObstacleManager obstacleManager;
    [SerializeField] private LevelControlsController controlsController;
    [SerializeField] private LevelMusicDirector levelMusicDirector;

    [Header("Tutorial")]
    [SerializeField] private LevelTutorialController tutorialController;

    // NOTE : ici, collector n est plus utilise directement par LevelManager,
    // mais il peut etre utile a l inspection et/ou utilise par LevelEvacuationController.
    // Si tu veux aller au bout: supprime ce champ ici et garde uniquement la ref dans LevelEvacuationController.
    [SerializeField] private BinCollector collector;

    [Header("Resume Ceremony Visuals")]
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private BoardOutroAssembler boardOutro;


    // ----------------------------------------------------------
    // UI / OVERLAYS
    // ----------------------------------------------------------

    [Header("UI / Overlays")]
    [SerializeField] private LevelBriefingController briefingController;
    [SerializeField] private PauseController pauseController;
    [SerializeField] private ProgressBarUI progressBarUI;
    [SerializeField] private ProgressCountUI progressCountUI;
    [Header("UI / End Level")]
    [SerializeField] private EndLevelUI endLevelUI;

    // ----------------------------------------------------------
    // RUN / STATE
    // ----------------------------------------------------------

    [Header("Run / Persistance")]
    [SerializeField] private LevelRunStateController runStateController;

    // Contexte du niveau charge (source de verite: RunPlan -> levelId)
    private PhasePlanInfo[] phasePlanInfos;
    private LevelCatalogService.LevelCatalogEntry levelMeta;
    private LevelData data;
    private string levelID;

    // Etat runtime
    private bool endSequenceRunning;
    private bool hardStopped;

    // Runtime ship stats (fournies par ShipRuntimeSetup)
    private float runDurationSec;
    private int maxHull;
    private float shieldSeconds; // shield runtime total en secondes

    // Token de niveau scellé
    private EndLevelToken? endLevelToken;


    // ----------------------------------------------------------
    // HELPERS / SERVICES
    // ----------------------------------------------------------

    [Header("Binders")]
    [SerializeField] private HullBinder hullBinder;

    [Header("Services")]
    [SerializeField] private FinalBallCleanupService finalBallCleanupService;

    [Header("Controllers")]
    [SerializeField] private LevelEvacuationController evacuationController;
    [SerializeField] private LevelEndModuleBonusController endModuleBonusController;

    [Header("Flow Helpers")]
    [SerializeField] private LevelPauseFlowHandler pauseFlowHandler;

    [Header("Bootstrapping")]
    [SerializeField] private LevelBootstrapper levelBootstrapper;

    [Header("Runtime Setup")]
    [SerializeField] private ShipRuntimeSetup shipRuntimeSetup;

    [Header("Narration / Dialogues")]
    [SerializeField] private LevelIntroSequenceController introSequenceController;

    [Header("Objectifs secondaires")]
    [SerializeField] private LevelSecondaryObjectivesController secondaryObjectivesController;

    [Header("FinalComboConfig")]
    [SerializeField] private FinalComboConfig finalComboConfig;

    // ----------------------------------------------------------
    // EVENTS / HANDLERS (pour detacher proprement)
    // ----------------------------------------------------------

    // OnPlannedReady est emise une seule fois quand le spawner a calcule son plan (planned counts).
    // On stocke le handler pour pouvoir detacher dans OnDestroy (scene reload, debug rerun, etc.).
    private Action<int> onPlannedReadyHandler;
    private bool plannedReadyHooked;

    // Flush hook sur ScoreManager (refresh progress)
    private bool flushHooked;

    // FLAG : Gameplay sealed
    private bool gameplaySealed;

    // Phase hook (pour phase-scoped secondary objectives)
    private Action<int, string> onPhaseChangedHandler;
    private bool phaseChangedHooked;

    // =====================================================================
    // UNITY LIFECYCLE
    // =====================================================================

    private void OnEnable()
    {
        // Timer end -> declenche evacuation
        if (levelTimer != null)
            levelTimer.OnTimerEnd += HandleTimerEnd;

        // Goal reached -> timestamp sur ScoreManager
        if (scoreManager != null)
            scoreManager.onGoalReached.AddListener(HandleMainGoalReached);

        // Gameplay sealed
        if (evacuationController != null)
            evacuationController.OnGameplaySealed += HandleGameplaySealed;
    }

    private void OnDisable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd -= HandleTimerEnd;

        if (scoreManager != null)
            scoreManager.onGoalReached.RemoveListener(HandleMainGoalReached);

        if (evacuationController != null)
            evacuationController.OnGameplaySealed -= HandleGameplaySealed;
    }

    private void Start()
    {
        // 1) Bootstrap context
        if (!BuildLevelContext())
        {
            Debug.LogError("[LevelManager] BuildLevelContext a echoue. Niveau non demarre.");
            enabled = false;
            return;
        }

        // 2) Objectifs secondaires
        SetupSecondaryObjectives();

        // 3) Profil de mission du vaisseau
        // IMPORTANT: configure durée/shield + init visuels (background) + init systèmes (score, etc.)
        // Le Hull n'est PAS géré ici (source de vérité = RunSessionState, sync via HullBinder).
        if (!ApplyShipMissionProfile())
        {
            Debug.LogError("[LevelManager] ApplyShipMissionProfile a echoue. Niveau non demarre.");
            enabled = false;
            return;
        }

        // 3bis) RESUME SNAPSHOT (après ApplyShipMissionProfile, sinon background vide)
        if (TryResumeEndCeremonyFromSnapshot())
            return;

        // 4) Timer
        SetupTimer();

        // 5) Spawner + ProgressBar
        SetupSpawnerAndProgress();

        // 6) Obstacles
        SetupObstacles();

        // 7) Evacuation
        if (evacuationController != null)
        {
            evacuationController.Configure(
                data,
                onBeforeBoardOutroCb: PlayPreBoardOutroBonuses
            );
        }

        // 8) Briefing / intro
        levelMusicDirector?.SelectRandomPair();
        levelMusicDirector?.PlayBriefingMusic();
        SetupIntroOrAutoStart();

        // 9) Pause
        BindPauseOverlay();
    }



    // =====================================================================
    // BOOTSTRAP / CONTEXT
    // =====================================================================

    /// <summary>
    /// Construit le contexte du niveau via LevelBootstrapper.
    /// Source de verite: RunSessionState -> RunPlan -> current node -> levelId.
    /// </summary>
    private bool BuildLevelContext()
    {
        if (levelBootstrapper == null)
        {
            Debug.LogError("[LevelManager] levelBootstrapper non assigne.");
            return false;
        }

        LevelContext ctx;
        if (!levelBootstrapper.TryBuildContext(out ctx) || ctx == null || !ctx.IsValid())
        {
            Debug.LogError("[LevelManager] LevelContext invalide.");
            return false;
        }

        levelID = ctx.levelId;
        levelMeta = ctx.levelMeta;
        data = ctx.levelData;

        return true;
    }

    private void SetupSecondaryObjectives()
    {
        if (secondaryObjectivesController == null)
            return;

        secondaryObjectivesController.SetupFromLevel(data);
    }

    /// <summary>
    /// Applique le profil de mission du vaisseau :
    /// - résout la durée de la mission (runDurationSec)
    /// - résout le "shield" affiché (alias UI de la durée)
    /// - déclenche les initialisations liées au vaisseau
    ///   (background, score reset/binding, etc.)
    ///
    /// IMPORTANT :
    /// - Le Hull (current + max) n'est PAS géré ici.
    /// - Source de vérité Hull = RunSessionState.
    /// - Le HUD Hull est synchronisé via HullBinder.
    ///
    /// Cette méthode configure l'environnement de mission,
    /// pas les ressources de survie.
    /// </summary>
    private bool ApplyShipMissionProfile()
    {
        if (shipRuntimeSetup == null)
        {
            Debug.LogError("[LevelManager] shipRuntimeSetup non assigne.");
            return false;
        }

        int unusedMaxHull; // conservé pour compat / debug éventuel
        float resolvedDuration;
        float resolvedShieldSeconds;

        if (!shipRuntimeSetup.TryApply(out unusedMaxHull, out resolvedDuration, out resolvedShieldSeconds))
            return false;

        // Durée / pression de mission
        runDurationSec = resolvedDuration;
        shieldSeconds = resolvedShieldSeconds;

        Debug.Log("[LevelManager] ApplyShipMissionProfile OK"
            + " runDurationSec=" + runDurationSec
            + " shieldSeconds=" + shieldSeconds);

        return true;
    }



    // =====================================================================
    // SETUP GAMEPLAY (Timer / Spawner / Obstacles)
    // =====================================================================

    private void SetupTimer()
    {
        if (levelTimer == null)
            return;

        // Desactive avant le start (le StartLevel l arme)
        levelTimer.enabled = false;

        // Duree runtime fournie par ShipRuntimeSetup
        Debug.Log("[LevelManager] SetupTimer: runDurationSec=" + runDurationSec);
        levelTimer.StartTimer(runDurationSec);

    }

    private void SetupSpawnerAndProgress()
    {
        if (ballSpawner == null || data == null)
            return;

        // Hook events une seule fois
        HookFlushRegisteredOnce();
        HookPlannedReadyOnce();
        HookPhaseChangedOnce();

        // Configuration du spawner
        ballSpawner.ConfigureFromLevel(data, runDurationSec);

        // Prewarm pour limiter les spikes de perf
        ballSpawner.StartPrewarm(256);

        // Phase plan infos (affichees dans briefing/pause)
        phasePlanInfos = ballSpawner.GetPhasePlans();
    }

    /// <summary>
    /// OnPlannedReady est emis lorsque le spawner a calcule:
    /// - PlannedNonBlackSpawnCount
    /// - phases planning
    /// On s abonne une seule fois et on detache dans OnDestroy.
    /// </summary>
    private void HookPlannedReadyOnce()
    {
        if (plannedReadyHooked || ballSpawner == null)
            return;

        plannedReadyHooked = true;

        onPlannedReadyHandler = HandlePlannedReady;
        ballSpawner.OnPlannedReady += onPlannedReadyHandler;
    }

    private void HookPhaseChangedOnce()
    {
        if (phaseChangedHooked || ballSpawner == null)
            return;

        phaseChangedHooked = true;

        onPhaseChangedHandler = HandlePhaseChanged;
        ballSpawner.OnPhaseChanged += onPhaseChangedHandler;
    }

    /// <summary>
    /// BallSpawner envoie un phaseIndex 0-based.
    /// On convertit en 1-based pour SecondaryObjectivesManager (PhaseIndex JSON = 1..N).
    /// </summary>
    private void HandlePhaseChanged(int phaseIndex0Based, string phaseName)
    {
        if (secondaryObjectivesController == null)
            return;

        int phaseIndex1Based = phaseIndex0Based + 1;

        var mgr = secondaryObjectivesController.Manager;
        if (mgr != null)
            mgr.SetCurrentPhaseIndex1Based(phaseIndex1Based);
    }



    /// <summary>
    /// Callback OnPlannedReady.
    /// Le parametre event est ignore mais conserve pour matcher la signature.
    /// </summary>
    private void HandlePlannedReady(int _)
    {
        if (scoreManager == null || ballSpawner == null || data == null)
            return;

        // Planned balls (non-black) = denominateur des progress bars
        int plannedNonBlack = ballSpawner.PlannedNonBlackSpawnCount;
        scoreManager.SetPlannedBalls(plannedNonBlack);

        // Threshold objectif principal (count)
        int threshold = (data.MainObjective != null) ? data.MainObjective.ThresholdCount : 0;

        // Progress bar (configure + refresh immediat)
        if (progressBarUI != null)
        {
            progressBarUI.Configure(plannedNonBlack, threshold);
            progressBarUI.Refresh();
        }
        if (progressCountUI != null)
        {
            progressCountUI.Configure(threshold);
            progressCountUI.Refresh();
        }

        // ScoreManager retient l objectif pour trigger onGoalReached au bon moment
        scoreManager.SetObjectiveThreshold(threshold);
    }

    /// <summary>
    /// Hook du refresh progress sur chaque snapshot de flush.
    /// </summary>
    private void HookFlushRegisteredOnce()
    {
        if (flushHooked || scoreManager == null)
            return;

        flushHooked = true;

        // Idempotent: retire puis ajoute pour eviter les doublons en debug rerun
        scoreManager.OnFlushSnapshotRegistered -= HandleFlushRegistered;
        scoreManager.OnFlushSnapshotRegistered += HandleFlushRegistered;
    }

    private void HandleFlushRegistered(BinSnapshot snapshot)
    {
        // La progress bar depend des flush snapshots / pertes, donc refresh
        progressBarUI?.Refresh();
        progressCountUI?.Refresh();
    }

    private void SetupObstacles()
    {
        if (obstacleManager == null)
            return;

        if (data == null || data.Obstacles == null || data.Obstacles.Length == 0)
            return;

        obstacleManager.BuildObstacles(data.Obstacles);
    }

    // =====================================================================
    // INTRO (Briefing -> Intro -> StartLevel)
    // =====================================================================

    private void SetupIntroOrAutoStart()
    {
        // Briefing obligatoire dans ton flow.
        // Fallback en debug / scene incomplete: start direct.
        if (briefingController == null || data == null)
        {
            Debug.LogWarning("[LevelManager] Briefing manquant ou data null. Demarrage direct du niveau.");
            StartLevel();
            return;
        }

        // Injecte les valeurs hull runtime dans le briefing (affichage current/max).
        // Source : currentHull depuis SaveManager (runState), maxHull depuis ShipRuntimeSetup.

        if (briefingController != null)
        {
            int currentHull = 0;
            if (SaveManager.Instance != null)
                currentHull = SaveManager.Instance.GetRemainingHullInRun();

            briefingController.SetShipRuntimeHull(currentHull, maxHull);

            // >>> AJOUT ICI <<<
            briefingController.SetShipRuntimeShield(shieldSeconds);
        }


        briefingController.Show(
            levelMeta,
            data,
            phasePlanInfos,
            onPlay: () =>
            {
                // Intro optionnelle
                if (introSequenceController != null)
                {
                    // Configure le levelId au cas ou (safe)
                    introSequenceController.ConfigureLevelId(levelID);

                    // Quand l intro se termine -> tuto -> start level
                    introSequenceController.Play(BeginTutorialOrStartLevel);
                }
                else
                {
                    StartLevel();
                }
            },
            onMenu: () => BootRoot.GameFlow.GoToTitle()
        );
    }

    // =====================================================================
    // PAUSE
    // =====================================================================

    private void BindPauseOverlay()
    {
        if (pauseController == null)
            return;

        // Idempotent: retire puis ajoute
        pauseController.OnPauseOpening -= HandlePauseOpening;
        pauseController.OnPauseOpening += HandlePauseOpening;
    }

    private void HandlePauseOpening()
    {
        if (pauseFlowHandler == null)
        {
            Debug.LogWarning("[LevelManager] pauseFlowHandler manquant.");
            return;
        }

        // Délègue la cuisine du contenu + callbacks Menu/Retry
        pauseFlowHandler.ShowPause(levelMeta, data, phasePlanInfos);
    }

    // =====================================================================
    // FLOW (Start -> TimerEnd -> Evac -> Finalize)
    // =====================================================================

    public void StartLevel()
    {
        hardStopped = false;
        endSequenceRunning = false;
        gameplaySealed = false;
        Time.timeScale = 1f;

        // Analytics :
        // - BeginRun uniquement sur le premier node de la run
        // - BeginLevel a chaque niveau
        if (runSessionState != null && runSessionState.CurrentNodeIndex == 0)
            AlphaAnalytics.Instance?.BeginRun();

        AlphaAnalytics.Instance?.BeginLevel();

        levelMusicDirector?.PlayGameplayMusic();

        // Evac controller reset (evite les restes d etat si rerun)
        evacuationController?.ResetState();

        // Arme le timer (il doit avoir ete StartTimer(...) avant)
        if (levelTimer != null)
            levelTimer.enabled = true;

        runStateController?.MarkLevelStarted();

        EnableGameplayControls();

        // Demarre le spawner
        ballSpawner?.StartSpawning();
    }

    private void HandleTimerEnd()
    {
        // Protections:
        // - endSequenceRunning: evite double trigger
        // - hardStopped: si GameOver immediate, on ignore la fin de timer
        if (endSequenceRunning || hardStopped)
            return;

        ballSpawner?.StopSpawning();
        endSequenceRunning = true;

        // Phase evacuation (si controller present), puis finalize
        if (evacuationController != null)
        {
            evacuationController.BeginEvacuationPhase(() =>
            {
                StartCoroutine(EndOfLevelFinalizeRoutine());
            });
        }
        else
        {
            StartCoroutine(EndOfLevelFinalizeRoutine());
        }
    }

    private IEnumerator EndOfLevelFinalizeRoutine()
    {
        // On attend que le gameplay soit officiellement scelle (flush final fini)
        float timeout = 2f;
        float t = 0f;
        while (!gameplaySealed && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!gameplaySealed)
            Debug.LogWarning("[LevelManager] GameplaySealed non recu avant finalize. On continue quand meme.");

        // Cleanup final (mark lost + recycle) avant evaluation
        if (finalBallCleanupService != null)
            yield return StartCoroutine(finalBallCleanupService.Execute(ballSpawner, scoreManager));

        ballSpawner?.LogStats();

        // Evaluation + declenchement UI de fin
        yield return StartCoroutine(EvaluateLevelResultRoutine());
    }

    private IEnumerator PlayPreBoardOutroBonuses()
    {
        if (endModuleBonusController == null)
            yield break;

        bool mainGoalAchieved = scoreManager != null && scoreManager.MainGoalAchieved;
        yield return StartCoroutine(endModuleBonusController.PlayPreCeremonyBonuses(mainGoalAchieved));
    }

    /// <summary>
    /// Hard stop (GameOver Hull <= 0) : stop gameplay immediat sans evacuation.
    /// </summary>
    public void HardStopGameplay()
    {
        if (hardStopped)
            return;

        hardStopped = true;
        endSequenceRunning = true;

        CursorController.Unlock();

        runStateController?.MarkLevelEnded();

        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkGameOverInRun();

        Time.timeScale = 1f;

        if (levelTimer != null)
            levelTimer.enabled = false;

        // IMPORTANT :
        // on annule explicitement toute evacuation / countdown / final flush en cours
        evacuationController?.AbortEvacuation();

        ballSpawner?.StopSpawning();

        // Fige les bins
        closeBinController?.ForceCloseAndLock();

        DisableGameplayControls();

        // Coupe les coroutines du LevelManager
        StopAllCoroutines();

        // Stop intro si encore en cours
        introSequenceController?.StopIntro();
    }

    // ============================================================
    // NEW (RESUME) :
    // Rejoue la revealRoutine depuis la save si un snapshot pending existe.
    // Important :
    // - On ne démarre pas le gameplay.
    // - Le commit se fera plus tard via LevelEndFlowController
    //   quand EndLevelUI.OnCeremonyFinished arrivera.
    // ============================================================
    private bool TryResumeEndCeremonyFromSnapshot()
    {
        if (SaveManager.Instance == null)
            return false;

        if (!SaveManager.Instance.TryGetPendingEndSnapshot(out EndLevelSnapshot snap))
            return false;

        if (snap == null)
            return false;

        // Si déjà committé, on ne rejoue pas la cérémonie.
        // (Normalement, SaveManager.Reconcile au Load aura déjà purgé ce snapshot.)
        if (snap.RewardsCommitted)
            return false;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return false;

        // Garde-fou : on ne resume QUE si on est sur le même node attendu.
        if (run.currentNodeIndex != snap.Token.NodeIndex)
            return false;

        // Stoppe toute tentative de démarrage de gameplay.
        ballSpawner?.StopSpawning();
        if (levelTimer != null)
            levelTimer.enabled = false;

        DisableGameplayControls();

        // (Optionnel) on peut aussi désactiver la pause pendant la cérémonie
        // si tu veux éviter une pause overlay sur l'end screen.
        // pauseController?.EnablePause(false);

        if (endLevelUI == null)
            return false;

        // Injecte le token scellé dans EndLevelUI, pour que OnCeremonyFinished le renvoie.
        endLevelUI.SetEndLevelToken(snap.Token);

        // Reconstitue la liste des secondaires depuis l'array (JsonUtility-safe).
        List<SecondaryObjectiveResult> sec = null;
        if (snap.Secondary != null && snap.Secondary.Length > 0)
            sec = new List<SecondaryObjectiveResult>(snap.Secondary);

        // NEW: remettre la scène dans l'état visuel "post gameplay"
        // (board rangé + HUD gameplay caché) pour que la cérémonie soit crédible.
        if (boardOutro != null)
            boardOutro.ForceOutroStateInstant();

        if (gameplayHudRoot != null)
            gameplayHudRoot.SetActive(false);


        // Rejoue la cérémonie (revealRoutine) à partir du snapshot.
        endLevelUI.Show(
            snap.Stats,
            levelMeta,
            data,
            snap.MainObjective,
            sec
        );

        Debug.Log("[LevelManager] RESUME: replay cérémonie depuis EndLevelSnapshot (pending, non-committed).");
        return true;
    }

    // =====================================================================
    // EVALUATION (Resultats)
    // =====================================================================

    private IEnumerator EvaluateLevelResultRoutine()
    {
        if (scoreManager == null || data == null)
        {
            Debug.LogWarning("[LevelManager] ScoreManager ou LevelData manquants, evaluation impossible.");
            yield break;
        }

        int elapsed = Mathf.RoundToInt(levelTimer != null ? levelTimer.GetElapsedTime() : 0f);

        SecondaryObjectivesManager secManager = null;
        if (secondaryObjectivesController != null)
            secManager = secondaryObjectivesController.Manager;

        var evalResult = LevelResultEvaluator.Evaluate(
            scoreManager,
            data,
            secManager,
            elapsed,
            finalComboConfig
        );

        if (evalResult.Stats == null)
        {
            Debug.LogWarning("[LevelManager] Evaluation de fin de niveau invalide (Stats null).");
            yield break;
        }


        // ===================================================
        // TOKEN (source de vérité = SaveManager.runState)
        // ===================================================

        string runId = "debug";
        string worldId = (levelMeta != null) ? levelMeta.worldId : "";
        int nodeIndex = -1;

        if (SaveManager.Instance != null)
        {
            RunStateData run = SaveManager.Instance.GetRunState();
            if (run != null)
            {
                if (!string.IsNullOrEmpty(run.runId))
                    runId = run.runId;

                if (!string.IsNullOrEmpty(run.worldId))
                    worldId = run.worldId;

                nodeIndex = run.currentNodeIndex;
            }
        }

        endLevelToken = new EndLevelToken
        {
            RunId = runId,
            WorldId = worldId,
            LevelId = (data != null) ? data.LevelID : "",
            NodeIndex = nodeIndex,

            IsVictory = evalResult.MainObjective.Achieved,
            FinalScore = evalResult.Stats.FinalScore,

            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Push results secondaires au controller (pour affichage end UI)
        if (secondaryObjectivesController != null)
            secondaryObjectivesController.SetResults(evalResult.SecondaryObjectives);

        // -----------------------------------------------
        // SNAPSHOT + TOKEN PERSISTANT (crash-safe)
        // -----------------------------------------------
        if (SaveManager.Instance != null && endLevelToken.HasValue)
        {
            SaveManager.Instance.SetPendingEndToken(endLevelToken.Value);

            List<SecondaryObjectiveResult> secondary = GetSecondaryObjectiveResults();

            SecondaryObjectiveResult[] secondaryArr = null;
            if (secondary != null && secondary.Count > 0)
            {
                secondaryArr = new SecondaryObjectiveResult[secondary.Count];
                for (int i = 0; i < secondary.Count; i++)
                    secondaryArr[i] = secondary[i];
            }

            EndLevelSnapshot snapshot = new EndLevelSnapshot
            {
                Token = endLevelToken.Value,
                LevelId = endLevelToken.Value.LevelId,
                Stats = evalResult.Stats,
                MainObjective = evalResult.MainObjective,
                Secondary = secondaryArr,

                // IMPORTANT : false => COMMIT pas fait, on peut replay cérémonie après crash.
                RewardsCommitted = false,

                EvaluatedTimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            SaveManager.Instance.SetPendingEndSnapshot(snapshot);
        }

        if (endLevelUI != null)
        {
            // Si le Hull est deja a 0, on annule la ceremonie normale.
            if (runSessionState != null && runSessionState.Hull <= 0)
            {
                Debug.Log("[LevelManager] EvaluateLevelResult -> ceremonie annulee (Hull <= 0)");
                yield break;
            }

            CursorController.Unlock();

            if (endLevelToken.HasValue)
                endLevelUI.SetEndLevelToken(endLevelToken.Value);

            List<SecondaryObjectiveResult> secondary = GetSecondaryObjectiveResults();

            endLevelUI.Show(
                evalResult.Stats,
                levelMeta,
                data,
                evalResult.MainObjective,
                secondary
            );
        }
    }

    // =====================================================================
    // CONTROLS
    // =====================================================================

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

    private void EnableGameplayControls()
    {
        if (controlsController != null)
        {
            controlsController.EnableGameplayControls();
            return;
        }

        player?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);
    }

    // =====================================================================
    // UTILS
    // =====================================================================

    public string GetLevelID()
    {
        return data != null ? data.LevelID : levelID;
    }

    public List<SecondaryObjectiveResult> GetSecondaryObjectiveResults()
    {
        if (secondaryObjectivesController == null)
            return null;

        return secondaryObjectivesController.GetLastResults();
    }

    public void NotifyComboTriggered(string comboId)
    {
        secondaryObjectivesController?.NotifyComboTriggered(comboId);
    }

    /// <summary>
    /// Quand l objectif principal est atteint, on stocke le timestamp sur ScoreManager.
    /// (utilise par certains final combos / end ceremony)
    /// </summary>
    private void HandleMainGoalReached()
    {
        if (levelTimer == null || scoreManager == null)
            return;

        int elapsedSec = Mathf.RoundToInt(levelTimer.GetElapsedTime());
        scoreManager.SetMainGoalReachedTime(elapsedSec);
    }

    private void HandleGameplaySealed()
    {
        gameplaySealed = true;
    }



    public LevelCatalogService.LevelCatalogEntry GetLevelMeta()
    {
        return levelMeta;
    }

    private void OnDestroy()
    {
        CursorController.ForceUnlock();

        // Detach flush hook
        if (scoreManager != null)
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushRegistered;

        // Detach planned handler
        if (ballSpawner != null && onPlannedReadyHandler != null)
            ballSpawner.OnPlannedReady -= onPlannedReadyHandler;

        // Detach pause hook
        if (pauseController != null)
            pauseController.OnPauseOpening -= HandlePauseOpening;

        // Detach phase handler
        if (ballSpawner != null && onPhaseChangedHandler != null)
            ballSpawner.OnPhaseChanged -= onPhaseChangedHandler;
    }

    private void BeginTutorialOrStartLevel()
    {
        if (tutorialController != null && tutorialController.ShouldRunForLevel(levelID))
        {
            tutorialController.PlayTutorial(StartLevel);
            return;
        }

        StartLevel();
    }
}
