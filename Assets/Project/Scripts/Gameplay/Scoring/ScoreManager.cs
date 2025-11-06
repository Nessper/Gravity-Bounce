using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    [Header("Planned Target")]
    [SerializeField, Tooltip("Nombre total de billes prévues dans le niveau (fixé par LevelManager)")]
    private int totalBillesPrevues;
    public int TotalBillesPrevues => totalBillesPrevues;

    // --- RUNTIME STATS (non sérialisés) ---
    private int totalBilles;
    private int totalPertes;
    private int currentScore;
    private int realSpawned;
    private int pointsPerdus;

    public int TotalBilles => totalBilles;
    public int TotalPertes => totalPertes;
    public int CurrentScore => currentScore;
    public int GetRealSpawned() => realSpawned;

    // --- DÉTAILS INTERNES ---
    private readonly Dictionary<string, int> totauxParType = new();
    private readonly Dictionary<string, int> pertesParType = new();
    private readonly List<BinSnapshot> historique = new();

    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    // Caché dans l'inspector : utilisé uniquement par le code (ProgressBarUI, etc.)
    [HideInInspector]
    public IntEvent onScoreChanged = new();

    // ------------------------------
    // INIT / RESET
    // ------------------------------
    public void SetPlannedBalls(int count)
    {
        totalBillesPrevues = Mathf.Max(0, count);
    }

    public void ResetScore(int start = 0)
    {
        currentScore = start;
        totalBilles = 0;
        totalPertes = 0;
        pointsPerdus = 0;
        realSpawned = 0;

        totauxParType.Clear();
        pertesParType.Clear();
        historique.Clear();

        onScoreChanged?.Invoke(currentScore);
    }

    // ------------------------------
    // SCORE & EVENTS
    // ------------------------------
    public void RegisterRealSpawn() => realSpawned++;

    public void AddPoints(int amount, string reason = null)
    {
        currentScore += amount;
        onScoreChanged?.Invoke(currentScore);

        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[ScoreManager] +{amount} ({reason}) -> Total = {currentScore}");
    }

    public void GetSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0) return;

        historique.Add(snapshot);
        totalBilles += snapshot.nombreDeBilles;

        if (snapshot.parType != null)
        {
            foreach (var kv in snapshot.parType)
            {
                if (!totauxParType.ContainsKey(kv.Key))
                    totauxParType[kv.Key] = 0;
                totauxParType[kv.Key] += kv.Value;
            }
        }

        AddPoints(snapshot.totalPointsDuLot, "Flush Base");
    }

    public void RegisterLost(BallState ball)
    {
        if (ball == null) return;

        string key = ball.type.ToString();
        if (!pertesParType.ContainsKey(key))
            pertesParType[key] = 0;
        pertesParType[key] += 1;

        totalPertes++;
        pointsPerdus += ball.points;
    }

    // ------------------------------
    // END LEVEL STATS
    // ------------------------------
    public EndLevelStats BuildEndLevelStats(int timeElapsedSec)
    {
        return new EndLevelStats
        {
            TimeElapsedSec = Mathf.Max(0, timeElapsedSec),
            BallsCollected = totalBilles,
            BallsLost = totalPertes,
            RawScore = currentScore,
            FinalScore = currentScore
        };
    }

    public bool IsCountConsistent()
    {
        // Optionnel : comparer realSpawned vs collected+lost avec tolérance
        return true;
    }
}
