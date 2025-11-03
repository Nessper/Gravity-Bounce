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
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private BallTracker ballTracker;
    [SerializeField] private BinCollector collector;
    [SerializeField] private EndLevel endLevel;

    // ------------------------------
    //   RÉFÉRENCES UI / OVERLAYS
    // ------------------------------
    [Header("UI / Overlays")]
    [SerializeField] private IntroLevelUI levelIntroUI;    // panneau d’intro avant le niveau
    [SerializeField] private CountdownUI countdownUI;      // décompte 3-2-1 GO
    [SerializeField] private PauseController pauseController;
    [SerializeField] private LevelIdUI levelIdUI;          // affiche l’ID du niveau dans le HUD
    [SerializeField] private LivesUI livesUI;              // affiche le nombre de vies dans le HUD

    // ------------------------------
    //   CONFIGURATION DU NIVEAU
    // ------------------------------
    [Header("Configuration")]
    [SerializeField] private TextAsset levelJson;          // fichier JSON contenant les données du niveau

    // Données chargées depuis le JSON
    private LevelData data;
    private string levelID;
    private float levelDurationSec;

    // État du niveau
    private bool endSequenceRunning;
    private bool timerElapsed;
    private int currentLives;

    // ------------------------------
    //   UNITY EVENTS
    // ------------------------------
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
        // 1. Réinitialiser le score à 0 (UI mise à jour automatiquement)
        scoreManager?.ResetScore(0);

        // 2. Afficher les vies issues du JSON
        currentLives = data != null ? Mathf.Max(0, data.Lives) : 0;
        if (livesUI != null)
            livesUI.SetLives(currentLives);

        // 3. Pré-afficher le timer sans qu’il ne défile encore
        if (levelTimer != null && data != null)
        {
            levelTimer.enabled = false;               // bloque Update() avant le GO
            levelTimer.StartTimer(levelDurationSec);  // fixe la valeur de départ (affichée dans l’UI)
        }

        // 4. Bloquer le joueur et la pause tant que l’intro et le countdown ne sont pas terminés
        player?.SetActiveControl(false);
        pauseController?.EnablePause(false);

        // --- DÉROULEMENT DE L’INTRO ---
        if (levelIntroUI != null && data != null)
        {
            levelIntroUI.Show(
                data,
                onPlay: () =>
                {
                    // Clic sur "Jouer" : cacher l’intro et lancer le countdown
                    levelIntroUI.Hide();

                    if (countdownUI != null)
                    {
                        player?.SetActiveControl(false);
                        pauseController?.EnablePause(false);

                        // Lance le décompte 3-2-1 puis start le niveau
                        StartCoroutine(countdownUI.PlayCountdown(() =>
                        {
                            player?.SetActiveControl(true);
                            pauseController?.EnablePause(true);
                            StartLevel();
                        }));
                    }
                    else
                    {
                        // Pas de countdown : on démarre directement
                        player?.SetActiveControl(true);
                        pauseController?.EnablePause(true);
                        StartLevel();
                    }
                },
                onBack: () =>
                {
                    Debug.Log("[LevelManager] Retour menu (non implémenté).");
                }
            );
        }
        else
        {
            // Pas d’intro : démarrage immédiat
            player?.SetActiveControl(true);
            pauseController?.EnablePause(true);
            StartLevel();
        }
    }

    // ------------------------------
    //   CHARGEMENT DU FICHIER JSON
    // ------------------------------
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

        // Lecture des champs principaux
        levelID = data.LevelID;
        levelDurationSec = data.LevelDurationSec;

        // Afficher le LevelID dans le HUD
        if (levelIdUI != null)
            levelIdUI.SetLevelId(levelID);
    }

    // ------------------------------
    //   DÉMARRAGE DU NIVEAU
    // ------------------------------
    public void StartLevel()
    {
        Time.timeScale = 1f;
        endSequenceRunning = false;
        timerElapsed = false;

        endLevel?.Hide();
        scoreManager?.ResetScore(0);
        if (livesUI != null) livesUI.SetLives(currentLives);

        // Configure le spawner depuis les données JSON
        if (ballSpawner != null && data != null)
        {
            ballSpawner.ConfigureFromLevel(data);

            if (scoreManager != null)
                scoreManager.SetPlannedBalls(ballSpawner.PlannedSpawnCount);
        }

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
        // Attente : plus de flush en cours + toutes les billes traitées
        yield return new WaitUntil(() =>
            timerElapsed &&
            collector != null && !collector.IsAnyFlushActive &&
            ballTracker != null && ballTracker.AllBallsInBinOrCollected()
        );

        endSequenceRunning = true;

        // Petite latence visuelle avant le flush final
        yield return new WaitForSeconds(0.35f);

        // Flush final forcé (récupère les billes restantes)
        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: false);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        // Bloque la pause pendant l’overlay de fin
        pauseController?.EnablePause(false);

        // Évalue les résultats du niveau
        EvaluateLevelResult();

        // Désactive le contrôle joueur
        player?.SetActiveControl(false);
    }

    private void EvaluateLevelResult()
    {
        if (scoreManager == null || data == null)
        {
            Debug.LogWarning("[LevelManager] ScoreManager ou data manquants.");
            return;
        }

        int totalSpawned = scoreManager.TotalBillesPrevues;
        int totalCollected = scoreManager.TotalBilles;

        if (totalSpawned <= 0)
        {
            Debug.LogWarning("[LevelManager] Aucune bille prévue, évaluation ignorée.");
            return;
        }

        // Calcul du ratio de réussite
        float ratio = (float)totalCollected / totalSpawned * 100f;
        bool success = ratio >= data.ObjectifPourcentage;

        var stats = scoreManager.BuildEndLevelStats();
        endLevel?.ShowResult(success, ratio, data.ObjectifPourcentage, stats);
    }

    // ------------------------------
    //   GESTION DES VIES
    // ------------------------------
    public void LoseLife(int amount = 1)
    {
        currentLives = Mathf.Max(0, currentLives - Mathf.Max(1, amount));
        if (livesUI != null)
            livesUI.SetLives(currentLives);
    }

    public void AddLife(int amount = 1)
    {
        currentLives += Mathf.Max(1, amount);
        if (livesUI != null)
            livesUI.SetLives(currentLives);
    }

    // ------------------------------
    //   OUTILS / GETTERS
    // ------------------------------
    public string GetLevelID() => data != null ? data.LevelID : levelID;
}
