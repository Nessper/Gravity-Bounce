using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    // ------------------------------
    //   PLANNED TARGET
    // ------------------------------
    [Header("Planned Target")]
    [SerializeField] private int totalBillesPrevues;
    public int TotalBillesPrevues => totalBillesPrevues;

    // ------------------------------
    //   RUNTIME
    // ------------------------------
    private int totalBilles;
    private int totalPertes;
    private int currentScore;
    private int realSpawned;

    public int TotalBilles => totalBilles;
    public int TotalPertes => totalPertes;
    public int CurrentScore => currentScore;
    public int GetRealSpawned() => realSpawned;

    // Détails agrégés
    private readonly Dictionary<string, int> totauxParType = new();
    private readonly Dictionary<string, int> pertesParType = new();
    private readonly List<BinSnapshot> historique = new();

    [Serializable] public class IntEvent : UnityEvent<int> { }
    [HideInInspector] public IntEvent onScoreChanged = new();

    // Perte de bille
    public event Action<string> OnBallLost;

    public IReadOnlyDictionary<string, int> GetTotalsByTypeSnapshot()
    => new Dictionary<string, int>(totauxParType);

    public IReadOnlyDictionary<string, int> GetLossesByTypeSnapshot()
        => new Dictionary<string, int>(pertesParType);

    public List<BinSnapshot> GetHistoriqueSnapshot()
        => new List<BinSnapshot>(historique);


    // ------------------------------
    //   INIT / RESET
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
        realSpawned = 0;

        totauxParType.Clear();
        pertesParType.Clear();
        historique.Clear();

        onScoreChanged?.Invoke(currentScore);
    }

    // ------------------------------
    //   SCORE & EVENTS
    // ------------------------------
    public void RegisterRealSpawn() => realSpawned++;

    public void AddPoints(int amount, string _ = null)
    {
        currentScore += amount;
        onScoreChanged?.Invoke(currentScore);
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
                if (!totauxParType.ContainsKey(kv.Key)) totauxParType[kv.Key] = 0;
                totauxParType[kv.Key] += kv.Value;
            }
        }

        AddPoints(snapshot.totalPointsDuLot);
    }

    // ------------------------------
    //   PERTE DE BILLE (VoidTrigger)
    // ------------------------------
    public void RegisterLost(string ballType)
    {
        if (string.IsNullOrEmpty(ballType)) ballType = "Unknown";

        if (!pertesParType.ContainsKey(ballType)) pertesParType[ballType] = 0;
        pertesParType[ballType]++;

        totalPertes++;
        OnBallLost?.Invoke(ballType);
    }

    // ------------------------------
    //   END LEVEL STATS
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
}
