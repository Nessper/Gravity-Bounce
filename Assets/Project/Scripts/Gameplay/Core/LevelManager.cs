using System.Collections;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    // ------------------------------
    //   RÉFÉRENCES GAMEPLAY
    // ------------------------------
    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private BinCollector collector;
    [SerializeField] private EndSequenceController endSequence;

    // ------------------------------
    //   RÉFÉRENCES UI / OVERLAYS
    // ------------------------------
    [Header("UI / Overlays")]
    [SerializeField] private IntroLevelUI levelIntroUI;
    [SerializeField] private CountdownUI countdownUI;    // intro + évacuation
    [SerializeField] private PauseController pauseController;
    [SerializeField] private LevelIdUI levelIdUI;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private LivesUI livesUI;
    [SerializeField] private ProgressBarUI progressBarUI;
    [SerializeField] private PhaseBannerUI phaseBannerUI;

    [Header("UI - Fin de niveau")]
    [SerializeField] private EndLevelUI endLevelUI;

    // ------------------------------
    //   CONFIGURATION DU NIVEAU
    // ------------------------------
    [Header("Configuration")]
    [SerializeField] private TextAsset levelJson;

    private LevelData data;
    private string levelID;
    private float levelDurationSec;

    private bool endSequenceRunning;
    private int currentLives;

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

        // --- LIAISON SCORE UI --> ScoreManager ---
        if (scoreManager != null && scoreUI != null)
            scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);

        // --- INITIALISATION AVANT LE COUNTDOWN ---
        // 1) Score à 0 (l'UI est déjà abonnée, donc elle affiche 0 tout de suite)
        scoreManager?.ResetScore(0);

        // 2) Vies depuis JSON
        currentLives = data != null ? Mathf.Max(0, data.Lives) : 0;
        livesUI?.SetLives(currentLives);

        // Timer prêt mais figé
        if (levelTimer != null && data != null)
        {
            levelTimer.enabled = false;
            levelTimer.StartTimer(levelDurationSec);
        }

        // Spawner & progress bar
        if (ballSpawner != null && data != null)
        {
            ballSpawner.ConfigureFromLevel(data);
            scoreManager?.SetPlannedBalls(ballSpawner.PlannedSpawnCount);
        }
        if (progressBarUI != null && scoreManager != null && data != null)
        {
            int threshold = (data.MainObjective != null) ? data.MainObjective.ThresholdPct : 0;
            progressBarUI.Configure(scoreManager.TotalBillesPrevues, threshold);
            progressBarUI.Refresh();
        }

        // Verrou gameplay jusqu’au GO
        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);
        pauseController?.EnablePause(false);

        // Configure orchestrateur (évacuation 10 s)
        float evacDuration = Mathf.Max(0.1f, data.Evacuation.DurationSec);
        string evacName = data.Evacuation.Name; // peut être null/empty

        endSequence?.Configure(
            collector,
            player,
            closeBinController,
            pauseController,
            evacDuration: evacDuration,
            tickInterval: 1f,
            onEvacStartCb: () =>
            {
                // Affiche la bannière seulement si un nom est fourni dans le JSON
                if (!string.IsNullOrWhiteSpace(evacName))
                    phaseBannerUI?.ShowPhaseText(evacName, evacDuration);

                // Compte à rebours calé sur la durée JSON
                if (countdownUI != null)
                    StartCoroutine(countdownUI.PlayCountdownSeconds(evacDuration));
            },
            onEvacTickCb: null
        );

        // Intro + countdown départ
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
                onBack: () => { Debug.Log("[LevelManager] Retour menu (non implémenté)."); }
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
        levelDurationSec = data.LevelDurationSec;

        levelIdUI?.SetLevelId(levelID);
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

        endSequence?.BeginEvacuationPhase(() =>
        {
            EvaluateLevelResult();
        });
    }

    private void EvaluateLevelResult()
    {
        if (scoreManager == null || data == null)
        {
            Debug.LogWarning("[LevelManager] ScoreManager ou data manquants.");
            return;
        }

        int spawnedPlan = scoreManager.TotalBillesPrevues;
        int spawnedReal = scoreManager.GetRealSpawned();
        int collected = scoreManager.TotalBilles;
        int lost = scoreManager.TotalPertes;

        int spawnedForEval = spawnedReal > 0 ? spawnedReal : spawnedPlan;
        if (spawnedForEval <= 0)
        {
            Debug.LogWarning("[LevelManager] Aucune bille (réel ni prévu), évaluation ignorée.");
            return;
        }

        int thresholdPct = (data.MainObjective != null) ? data.MainObjective.ThresholdPct : 0;
        int required = Mathf.CeilToInt((thresholdPct / 100f) * spawnedForEval);
        bool success = collected >= required;

        var elapsed = Mathf.RoundToInt(levelTimer != null ? levelTimer.GetElapsedTime() : 0f);
        var stats = scoreManager.BuildEndLevelStats(elapsed);

        int handled = collected + lost;
        if (spawnedReal > 0 && Mathf.Abs(spawnedReal - handled) > 3)
            Debug.LogWarning($"[ScoreCheck] Ecart: Real={spawnedReal}, Handled={handled} (Collected={collected}, Lost={lost}), Plan={spawnedPlan}");

        var mainObj = new MainObjectiveResult
        {
            Text = (data.MainObjective != null) ? data.MainObjective.Text : string.Empty,
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
