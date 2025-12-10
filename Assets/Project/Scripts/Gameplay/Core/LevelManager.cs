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
    [Header("Run / Persistance")]
    [SerializeField] private LevelRunStateController runStateController;


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

    [Header("Objectifs secondaires")]
    [SerializeField] private LevelSecondaryObjectivesController secondaryObjectivesController;


    public UnityEvent<EndLevelStats, LevelData, MainObjectiveResult> OnEndComputed
        = new UnityEvent<EndLevelStats, LevelData, MainObjectiveResult>();

    // =====================================================================
    // DEBUG
    // =====================================================================
    [Header("Debug Main scene")]
    [SerializeField] private bool debugSkipBriefing;
    [SerializeField]
    private bool debugSkipIntro;

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
    }

    private void Awake()
    {
        // RunSessionState doit etre assigne dans l Inspector.
        if (runSession == null)
        {
            Debug.LogError("[LevelManager] RunSessionState non assigne. La scene Main doit toujours disposer d un RunSessionState.");
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled)
            return;

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
        if (secondaryObjectivesController == null)
            return;

        secondaryObjectivesController.SetupFromLevel(data);
    }


    private void SetupScoreAndHull()
    {
        // Score -> UI
        if (scoreManager != null && scoreUI != null)
            scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);

        scoreManager?.ResetScore(0);

        // RunSession obligatoire pour Main (controle en Awake).
        if (runSession == null)
        {
            Debug.LogError("[LevelManager] RunSessionState null dans SetupScoreAndHull. Cette scene doit passer par le flow complet.");
            return;
        }

        // Recupere maxHull et duree depuis le vaisseau
        ResolveShipStats(out maxHull, out runDurationSec);

        int hullForHud = runSession.Hull;

        // Initialise le systeme de coque (qui mettra la UI a jour)
        if (hullSystem != null)
        {
            hullSystem.Initialize(hullForHud, maxHull);
        }
        else if (hullUI != null)
        {
            hullUI.SetMaxHull(maxHull);
            hullUI.SetHull(hullForHud);
        }

        // Fond de vaisseau
        if (shipBackgroundLoader != null && !string.IsNullOrEmpty(currentShipImageFile))
        {
            shipBackgroundLoader.Init(currentShipImageFile);
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

        // On ne bloque que si le catalog est vraiment absent ou vide.
        if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            Debug.LogWarning("[LevelManager] ShipCatalog manquant ou vide, valeurs par defaut (0).");
            return;
        }

        // Si RunConfig est absent ou SelectedShipId vide, on tombe sur CORE_SCOUT.
        string shipId = "CORE_SCOUT";

        if (run != null && !string.IsNullOrEmpty(run.SelectedShipId))
        {
            shipId = run.SelectedShipId;
        }

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

    /// <summary>
    /// Desactive les controles gameplay (paddle, close bin, etc.).
    /// Utilise LevelControlsController si present, sinon fallback direct sur Player / CloseBinController.
    /// </summary>
    private void DisableGameplayControls()
    {
        if (controlsController != null)
        {
            controlsController.DisableGameplayControls();
            return;
        }

        if (player != null)
            player.SetActiveControl(false);

        if (closeBinController != null)
            closeBinController.SetActiveControl(false);
    }

    /// <summary>
    /// Active les controles gameplay (paddle, close bin, etc.).
    /// Utilise LevelControlsController si present, sinon fallback direct sur Player / CloseBinController.
    /// </summary>
    private void EnableGameplayControls()
    {
        if (controlsController != null)
        {
            controlsController.EnableGameplayControls();
            return;
        }

        if (player != null)
            player.SetActiveControl(true);

        if (closeBinController != null)
            closeBinController.SetActiveControl(true);
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
    /// <summary>
    /// Prepare et affiche le briefing de debut de niveau.
    /// - Injecte le hull courant pour affichage.
    /// - Configure le bouton Start pour lancer l intro puis le niveau.
    /// - Configure le bouton Menu pour retourner au Title via GameFlow.
    /// </summary>
    /// <summary>
    /// Prepare et affiche le briefing de debut de niveau.
    /// - Injecte le hull courant pour affichage.
    /// - Configure le bouton Start pour lancer l intro puis le niveau.
    /// - Configure le bouton Menu pour retourner au Title via GameFlow.
    /// </summary>
    /// <summary>
    /// Injecte le hull courant dans le briefing, puis affiche le briefing.
    /// En mode debug, peut skip le briefing et / ou l intro.
    /// </summary>
    private void SetupIntroOrAutoStart()
    {
        // Mode debug : skip briefing et intro -> demarrage direct du niveau
        if (debugSkipBriefing && debugSkipIntro)
        {
            Debug.Log("[LevelManager] Debug: skip briefing + intro, demarrage direct du niveau.");
            StartLevel();
            return;
        }

        // Mode debug : skip briefing mais garder l intro
        if (debugSkipBriefing && !debugSkipIntro)
        {
            Debug.Log("[LevelManager] Debug: skip briefing, intro uniquement.");

            if (introSequenceController != null)
            {
                introSequenceController.Play(() =>
                {
                    StartLevel();
                });
            }
            else
            {
                Debug.LogWarning("[LevelManager] Debug: introSequenceController manquant, demarrage direct du niveau.");
                StartLevel();
            }

            return;
        }

        // Comportement normal avec briefing
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
                        Debug.LogWarning("[LevelManager] Aucun LevelIntroSequenceController assigne. Demarrage direct du niveau.");
                        StartLevel();
                    }
                },
                onMenu: null
            );
        }
        else
        {
            Debug.LogError("[LevelManager] Aucun LevelBriefingController assigne alors que le briefing est obligatoire. Demarrage direct du niveau sans intro.");
            StartLevel();
        }
    }


    // =====================================================================
    // BOUCLE DE JEU
    // =====================================================================

    public void StartLevel()
    {
        Time.timeScale = 1f;
        endSequenceRunning = false;

        if (endSequence != null)
            endSequence.ResetState();

        if (levelTimer != null)
            levelTimer.enabled = true;

        // Tres important pour le debug Main et le skip intro
        EnableGameplayControls();

        if (ballSpawner != null)
            ballSpawner.StartSpawning();

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

        SecondaryObjectivesManager secManager = null;
        if (secondaryObjectivesController != null)
        {
            secManager = secondaryObjectivesController.Manager;
        }

        var evalResult = LevelResultEvaluator.Evaluate(
            scoreManager,
            data,
            secManager,
            elapsed
        );

        if (evalResult.Stats == null)
        {
            Debug.LogWarning("[LevelManager] Evaluation de fin de niveau invalide (Stats null).");
            return;
        }

        // On pousse les resultats secondaires dans le controleur dedie
        if (secondaryObjectivesController != null)
        {
            secondaryObjectivesController.SetResults(evalResult.SecondaryObjectives);
        }

        OnEndComputed.Invoke(evalResult.Stats, data, evalResult.MainObjective);


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
        if (secondaryObjectivesController == null)
            return null;

        return secondaryObjectivesController.GetLastResults();
    }

    public void NotifyComboTriggered(string comboId)
    {
        if (secondaryObjectivesController == null)
            return;

        secondaryObjectivesController.NotifyComboTriggered(comboId);
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


    /// <summary>
    /// Configure les flags de debug pour skip briefing / intro.
    /// Utilise par MainDebugStarter quand la scene Main est lancee seule.
    /// </summary>
    public void SetDebugSkipFlags(bool skipBriefing, bool skipIntro)
    {
        debugSkipBriefing = skipBriefing;
        debugSkipIntro = skipIntro;
    }

    /// <summary>
    /// Permet a l outil de debug de remplacer le JSON de niveau a runtime.
    /// </summary>
    public void DebugOverrideLevelJson(TextAsset json)
    {
        if (json == null)
            return;

        levelJson = json;
        Debug.Log("[LevelManager] DebugOverrideLevelJson -> " + json.name);
    }

}
