using System.Collections;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private BinCollector collector;
    [SerializeField] private EndSequenceController endSequence;

    [Header("UI / Overlays")]
    [SerializeField] private IntroLevelUI levelIntroUI;
    [SerializeField] private CountdownUI countdownUI;
    [SerializeField] private PauseController pauseController;
    [SerializeField] private LevelIdUI levelIdUI;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private LivesUI livesUI;
    [SerializeField] private ProgressBarUI progressBarUI;
    [SerializeField] private PhaseBannerUI phaseBannerUI;

    [Header("UI - Fin de niveau")]
    [SerializeField] private EndLevelUI endLevelUI;

    [Header("Configuration")]
    [SerializeField] private TextAsset levelJson;

    private LevelData data;
    private string levelID;
    private float runDurationSec;
    private int currentLives;
    private bool endSequenceRunning;

    private void OnEnable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd += HandleTimerEnd;
    }

    private void OnDisable()
    {
        if (levelTimer != null)
            levelTimer.OnTimerEnd -= HandleTimerEnd;
    }

    private void Start()
    {
        LoadLevelConfig();

        if (scoreManager != null && scoreUI != null)
            scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);
        scoreManager?.ResetScore(0);

        ResolveShipStats(out currentLives, out runDurationSec);

        var quick = Object.FindFirstObjectByType<MainQuickStart>();
        if (quick != null && quick.enabled && quick.gameObject.activeInHierarchy)
        {
            if (quick.forcedLives > 0) currentLives = quick.forcedLives;
            if (quick.forcedTimerSec > 0f) runDurationSec = quick.forcedTimerSec;
            Debug.Log($"[LevelManager] QuickStart active — Lives={currentLives}, Timer={runDurationSec}s");
        }

        livesUI?.SetLives(currentLives);
        if (levelTimer != null)
        {
            levelTimer.enabled = false;
            levelTimer.StartTimer(runDurationSec);
        }

        if (ballSpawner != null && data != null)
        {
            ballSpawner.OnPlannedReady += total =>
            {
                scoreManager?.SetPlannedBalls(total);
                if (progressBarUI != null && data != null)
                {
                    int threshold = data.MainObjective != null ? data.MainObjective.ThresholdPct : 0;
                    progressBarUI.Configure(total, threshold);
                    progressBarUI.Refresh();
                }
            };
            ballSpawner.OnActivated += _ =>
            {
                progressBarUI?.Refresh();
            };

            ballSpawner.ConfigureFromLevel(data, runDurationSec);
            ballSpawner.StartPrewarm(256);
        }

        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);
        pauseController?.EnablePause(false);

        float evacDuration = (data != null) ? Mathf.Max(0.1f, data.Evacuation.DurationSec) : 10f;
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
                if (!string.IsNullOrWhiteSpace(evacName))
                    phaseBannerUI?.ShowPhaseText(evacName, evacDuration);
                if (countdownUI != null)
                    StartCoroutine(countdownUI.PlayCountdownSeconds(evacDuration));
            },
            onEvacTickCb: null
        );

        if (levelIntroUI != null && data != null)
        {
            levelIntroUI.Show(
                data,
                onPlay: () =>
                {
                    levelIntroUI.Hide();
                    if (countdownUI != null)
                    {
                        StartCoroutine(countdownUI.PlayCountdown(() =>
                        {
                            player?.SetActiveControl(true);
                            closeBinController?.SetActiveControl(true);
                            pauseController?.EnablePause(true);
                            StartLevel();
                        }));
                    }
                    else
                    {
                        player?.SetActiveControl(true);
                        closeBinController?.SetActiveControl(true);
                        pauseController?.EnablePause(true);
                        StartLevel();
                    }
                },
                onBack: () => { Debug.Log("[LevelManager] Retour menu non implémenté."); }
            );
        }
        else
        {
            player?.SetActiveControl(true);
            closeBinController?.SetActiveControl(true);
            pauseController?.EnablePause(true);
            StartLevel();
        }
    }

    private void LoadLevelConfig()
    {
        if (levelJson == null) { Debug.LogError("[LevelManager] Aucun JSON assigné !"); return; }

        data = JsonUtility.FromJson<LevelData>(levelJson.text);
        if (data == null) { Debug.LogError("[LevelManager] Erreur de parsing JSON."); return; }

        levelID = data.LevelID;
        levelIdUI?.SetLevelId(levelID);
    }

    private void ResolveShipStats(out int lives, out float durationSec)
    {
        lives = 0; durationSec = 0f;

        var run = RunConfig.Instance;
        var catalog = ShipCatalogService.Catalog;

        if (run == null || catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            Debug.LogWarning("[LevelManager] ShipCatalog manquant, valeurs par défaut (0).");
            return;
        }

        var shipId = string.IsNullOrEmpty(run.SelectedShipId) ? "CORE_SCOUT" : run.SelectedShipId;
        var ship = catalog.ships.Find(s => s.id == shipId);
        if (ship == null) { Debug.LogWarning("[LevelManager] Vaisseau introuvable : " + shipId); return; }

        lives = Mathf.Max(0, ship.lives);
        durationSec = Mathf.Max(0.1f, ship.shieldSecondsPerLevel);
    }

    public void StartLevel()
    {
        Time.timeScale = 1f;
        endSequenceRunning = false;

        endSequence?.ResetState();
        endLevelUI?.Hide();

        if (levelTimer != null) levelTimer.enabled = true;
        ballSpawner?.StartSpawning();
    }

    private void HandleTimerEnd()
    {
        if (endSequenceRunning) return;

        ballSpawner?.StopSpawning();
        endSequenceRunning = true;

        // Lance l'evacuation, puis finalize proprement (flush forcé + sweep + stats + evaluation)
        endSequence?.BeginEvacuationPhase(() =>
        {
            StartCoroutine(EndOfLevelFinalizeRoutine());
        });
    }

    private IEnumerator EndOfLevelFinalizeRoutine()
    {
        // 1) Flush final forcé (prend tout ce qu'il y a dans les bins, sans seuil ni délai)
        collector?.CollectAll(force: true, skipDelay: true);

        // 2) Balayage final: tout ce qui reste actif et non-collected est considéré perdu
        yield return StartCoroutine(FinalSweepMarkLostAndRecycle(ballSpawner, scoreManager));

        // 3) Stats spawner (après sweep)
        ballSpawner?.LogStats();

        // 4) Evaluation et UI de fin
        EvaluateLevelResult();
    }

    private IEnumerator FinalSweepMarkLostAndRecycle(BallSpawner spawner, ScoreManager score)
    {
        // Laisse 1–2 frames au flush forcé pour vider les sets / events
        yield return null;
        yield return null;

#if UNITY_6000_0_OR_NEWER
        var balls = UnityEngine.Object.FindObjectsByType<BallState>(FindObjectsSortMode.None);
#else
    var balls = UnityEngine.Object.FindObjectsOfType<BallState>();
#endif


        foreach (var st in balls)
        {
            if (st == null) continue;
            var go = st.gameObject;

            if (!go.activeInHierarchy) continue;

            // Si déjà collected par un flush, recycle en collected (pas de double-compte)
            if (st.collected)
            {
                spawner?.Recycle(go, collected: true);
                continue;
            }

            // Si encore marqué "inBin", on considère que le flush forcé les a pris (par sécurité, on ignore)
            if (st.inBin) continue;

            // Tout le reste = perdu
            score?.RegisterLost(st.TypeName);
            spawner?.Recycle(go, collected: false);
        }
    }

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
        int lost = scoreManager.TotalPertes;

        int spawnedForEval = spawnedReal > 0 ? spawnedReal : spawnedPlan;
        if (spawnedForEval <= 0)
        {
            Debug.LogWarning("[LevelManager] Aucune bille, évaluation ignorée.");
            return;
        }

        int thresholdPct = data.MainObjective != null ? data.MainObjective.ThresholdPct : 0;
        int required = Mathf.CeilToInt((thresholdPct / 100f) * spawnedForEval);
        bool success = collected >= required;

        var elapsed = Mathf.RoundToInt(levelTimer != null ? levelTimer.GetElapsedTime() : 0f);
        var stats = scoreManager.BuildEndLevelStats(elapsed);

        int handled = collected + lost;
        if (spawnedReal > 0 && Mathf.Abs(spawnedReal - handled) > 3)
            Debug.LogWarning($"[ScoreCheck] Ecart: Real={spawnedReal}, Handled={handled}, Plan={spawnedPlan}");

        var mainObj = new MainObjectiveResult
        {
            Text = data.MainObjective?.Text ?? string.Empty,
            ThresholdPct = thresholdPct,
            Required = required,
            Collected = collected,
            Achieved = success,
            BonusApplied = (success && data.MainObjective != null) ? data.MainObjective.Bonus : 0
        };

        endLevelUI.Show(stats, data, mainObj);
    }

    public void LoseLife(int amount = 1)
    {
        currentLives = Mathf.Max(0, currentLives - Mathf.Max(1, amount));
        livesUI?.SetLives(currentLives);
    }

    public void AddLife(int amount = 1)
    {
        currentLives += Mathf.Max(1, amount);
        livesUI?.SetLives(currentLives);
    }

    public string GetLevelID() => data != null ? data.LevelID : levelID;
}
