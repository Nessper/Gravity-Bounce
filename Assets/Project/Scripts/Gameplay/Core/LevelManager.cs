using System.Collections;
using TMPro;
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
    [SerializeField] private BallTracker ballTracker;
    [SerializeField] private BinCollector collector;

    // ------------------------------
    //   RÉFÉRENCES UI / OVERLAYS
    // ------------------------------
    [Header("UI / Overlays")]
    [SerializeField] private IntroLevelUI levelIntroUI;
    [SerializeField] private CountdownUI countdownUI;
    [SerializeField] private PauseController pauseController;
    [SerializeField] private LevelIdUI levelIdUI;
    [SerializeField] private LivesUI livesUI;
    [SerializeField] private ProgressBarUI progressBarUI;

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

    // État du niveau
    private bool endSequenceRunning;
    private bool timerElapsed;
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
        // --- CHARGEMENT DES DONNÉES ---
        LoadLevelConfig();

        // --- INITIALISATION AVANT LE COUNTDOWN ---
        // 1) Score à 0
        scoreManager?.ResetScore(0);

        // 2) Vies depuis JSON
        currentLives = data != null ? Mathf.Max(0, data.Lives) : 0;
        if (livesUI != null) livesUI.SetLives(currentLives);

        // 3) Timer affiché mais figé
        if (levelTimer != null && data != null)
        {
            levelTimer.enabled = false;
            levelTimer.StartTimer(levelDurationSec);
        }

        // 4) Préparer spawner + planned pour la ProgressBar (avant le countdown)
        if (ballSpawner != null && data != null)
        {
            ballSpawner.ConfigureFromLevel(data); // prépare seulement
            scoreManager?.SetPlannedBalls(ballSpawner.PlannedSpawnCount);
        }

        // 5) ProgressBar = 0/planned + repère objectif (utilise MainObjective.ThresholdPct)
        if (progressBarUI != null && scoreManager != null && data != null)
        {
            int threshold = (data.MainObjective != null) ? data.MainObjective.ThresholdPct : 0;
            progressBarUI.Configure(scoreManager.TotalBillesPrevues, threshold);
            progressBarUI.Refresh(); // force l’affichage à 0 de suite
        }

        // 6) Verrou gameplay jusqu’au GO
        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);
        pauseController?.EnablePause(false);

        // --- INTRO + COUNTDOWN ---
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

        if (levelIdUI != null)
            levelIdUI.SetLevelId(levelID);
    }

    // ------------------------------
    //   DÉMARRAGE DU NIVEAU (après countdown)
    // ------------------------------
    public void StartLevel()
    {
        Time.timeScale = 1f;
        endSequenceRunning = false;
        timerElapsed = false;

        endLevelUI?.Hide();

        // Le score/vies/progressbar/spawner sont déjà configurés avant le countdown

        // Démarre réellement le timer maintenant
        if (levelTimer != null)
            levelTimer.enabled = true;

        // Lance la génération des billes
        ballSpawner?.StartSpawning();
    }

    // ------------------------------
    //   GESTION DE LA FIN DE NIVEAU
    // ------------------------------
    private void HandleTimerEnd()
    {
        if (endSequenceRunning) return;

        timerElapsed = true;
        ballSpawner?.StopSpawning();

        StartCoroutine(TryFinishWhenStable());
    }

    private IEnumerator TryFinishWhenStable()
    {
        yield return new WaitUntil(() =>
            timerElapsed &&
            collector != null && !collector.IsAnyFlushActive &&
            ballTracker != null && ballTracker.AllBallsInBinOrCollected()
        );

        endSequenceRunning = true;

        yield return new WaitForSeconds(0.35f);

        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: false);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        pauseController?.EnablePause(false);
        EvaluateLevelResult();
        player?.SetActiveControl(false);
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

        // Calcul objectif principal (seuil en nombre de billes)
        int thresholdPct = (data.MainObjective != null) ? data.MainObjective.ThresholdPct : 0;
        int required = Mathf.CeilToInt((thresholdPct / 100f) * spawnedForEval);
        bool success = collected >= required;

        var elapsed = Mathf.RoundToInt(levelTimer != null ? levelTimer.GetElapsedTime() : 0f);
        var stats = scoreManager.BuildEndLevelStats(elapsed);

        int handled = collected + lost;
        if (spawnedReal > 0 && Mathf.Abs(spawnedReal - handled) > 3)
            Debug.LogWarning($"[ScoreCheck] Ecart: Real={spawnedReal}, Handled={handled} (Collected={collected}, Lost={lost}), Plan={spawnedPlan}");

        // Construire le résultat d'objectif principal et afficher l'UI
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

    // ------------------------------
    //   VIES
    // ------------------------------
    public void LoseLife(int amount = 1)
    {
        currentLives = Mathf.Max(0, currentLives - Mathf.Max(1, amount));
        if (livesUI != null) livesUI.SetLives(currentLives);
    }

    public void AddLife(int amount = 1)
    {
        currentLives += Mathf.Max(1, amount);
        if (livesUI != null) livesUI.SetLives(currentLives);
    }

    public string GetLevelID() => data != null ? data.LevelID : levelID;
}
