using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private int totalBillesPrevues;
    public int TotalBillesPrevues => totalBillesPrevues;
    public void SetPlannedBalls(int count)
    {
        totalBillesPrevues = Mathf.Max(0, count);
        Debug.Log($"[ScoreManager] TotalBillesPrevues = {count}");
    }

    private int totalBilles;
    private Dictionary<string, int> totauxParType = new();
    private List<BinSnapshot> historique = new();

    // NEW: pertes
    private Dictionary<string, int> pertesParType = new();
    private int totalPertes;
    private int pointsPerdus;

    public int CurrentScore { get; private set; }
    public int TotalBilles => totalBilles;

    [System.Serializable] public class IntEvent : UnityEngine.Events.UnityEvent<int> { }
    public IntEvent onScoreChanged;

    public void ResetScore(int start = 0)
    {
        CurrentScore = start;
        totalBilles = 0;
        historique.Clear();
        totauxParType.Clear();

        pertesParType.Clear();
        totalPertes = 0;
        pointsPerdus = 0;

        onScoreChanged?.Invoke(CurrentScore);
    }

    // --- NOUVELLE MÉTHODE UNIQUE DE CALCUL ---
    public void AddPoints(int amount, string reason = null)
    {
        CurrentScore += amount;
        onScoreChanged?.Invoke(CurrentScore);
        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[ScoreManager] +{amount} ({reason}) -> Total = {CurrentScore}");
    }

    // --- SNAPSHOT --- (ne gère plus le score directement)
    public void GetSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0) return;

        historique.Add(snapshot);
        totalBilles += snapshot.nombreDeBilles;

        // Comptabilise les billes par type
        if (snapshot.parType != null)
        {
            foreach (var kv in snapshot.parType)
            {
                if (!totauxParType.ContainsKey(kv.Key))
                    totauxParType[kv.Key] = 0;
                totauxParType[kv.Key] += kv.Value;
            }
        }

        //  Ajout du score via la nouvelle méthode
        AddPoints(snapshot.totalPointsDuLot, "Flush Base");
    }

    public void RegisterLost(BallState ball)
    {
        if (ball == null) return;

        string key = ball.type.ToString();
        if (!pertesParType.ContainsKey(key))
            pertesParType[key] = 0;
        pertesParType[key] += 1;

        totalPertes += 1;
        pointsPerdus += ball.points;
    }

    public EndLevelStats BuildEndLevelStats()
    {
        var stats = new EndLevelStats
        {
            totalCollectees = totalBilles,
            totalPrevues = totalBillesPrevues,
            totalPerdues = totalPertes,
            scoreFinal = CurrentScore,
            pointsPerdus = pointsPerdus,
            collecteesParType = new Dictionary<string, int>(totauxParType),
            perduesParType = new Dictionary<string, int>(pertesParType)
        };
        return stats;
    }

    public bool IsCountConsistent() => true;
}
