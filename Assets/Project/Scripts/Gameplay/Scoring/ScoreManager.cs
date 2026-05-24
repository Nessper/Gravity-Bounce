using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    [Header("Planned Target")]
    [SerializeField] private int totalBillesPrevues;
    public int TotalBillesPrevues => totalBillesPrevues;

    [Header("Main Objective")]
    [SerializeField] private int objectiveThreshold;
    public int ObjectiveThreshold => objectiveThreshold;

    private bool goalReached = false;

    [Serializable]
    public class SimpleEvent : UnityEvent { }

    public SimpleEvent onGoalReached = new SimpleEvent();

    private int totalBilles;
    private int totalBillesNonNoires;
    private int totalPertes;
    private int currentScore;
    private int realSpawned;

    private int mainGoalReachedTimeSec = -1;
    public int MainGoalReachedTimeSec => mainGoalReachedTimeSec;
    public bool MainGoalAchieved => goalReached;

    private bool goalReachedInFinalFlush = false;
    public bool GoalReachedInFinalFlush => goalReachedInFinalFlush;

    public int TotalBilles => totalBilles;
    public int TotalNonBlackBilles => totalBillesNonNoires;
    public int TotalPertes => totalPertes;
    public int CurrentScore => currentScore;
    public int GetRealSpawned() => realSpawned;

    private readonly Dictionary<string, int> totauxParType = new Dictionary<string, int>();
    private readonly Dictionary<string, int> pertesParType = new Dictionary<string, int>();
    private readonly List<BinSnapshot> historique = new List<BinSnapshot>();
    private readonly HashSet<string> combosTriggered = new HashSet<string>();

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    [HideInInspector]
    public IntEvent onScoreChanged = new IntEvent();

    public event Action<string> OnBallLost;
    public event Action<BinSnapshot> OnFlushSnapshotRegistered;

    public IReadOnlyDictionary<string, int> GetTotalsByTypeSnapshot()
        => new Dictionary<string, int>(totauxParType);

    public IReadOnlyDictionary<string, int> GetLossesByTypeSnapshot()
        => new Dictionary<string, int>(pertesParType);

    public List<BinSnapshot> GetHistoriqueSnapshot()
        => new List<BinSnapshot>(historique);

    public void SetPlannedBalls(int count)
    {
        totalBillesPrevues = Mathf.Max(0, count);
    }

    public void SetObjectiveThreshold(int threshold)
    {
        objectiveThreshold = Mathf.Max(0, threshold);
        goalReached = false;
    }

    public void ResetScore(int start = 0)
    {
        currentScore = start;
        totalBilles = 0;
        totalBillesNonNoires = 0;
        totalPertes = 0;
        realSpawned = 0;
        goalReached = false;
        goalReachedInFinalFlush = false;
        mainGoalReachedTimeSec = -1;

        totauxParType.Clear();
        pertesParType.Clear();
        historique.Clear();
        combosTriggered.Clear();

        onScoreChanged?.Invoke(currentScore);
    }

    /// <summary>
    /// Reset explicite pour debut de niveau / reset post-tuto.
    /// </summary>
    public void ResetForLevelStart(int startScore = 0)
    {
        ResetScore(startScore);
    }

    public void RegisterRealSpawn()
    {
        realSpawned++;
    }

    public void AddPoints(int amount, string _ = null)
    {
        currentScore += amount;
        onScoreChanged?.Invoke(currentScore);
    }

    public void GetSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0)
            return;

        historique.Add(snapshot);
        totalBilles += snapshot.nombreDeBilles;

        int nonBlackThisFlush = 0;

        if (snapshot.parBallId != null)
        {
            foreach (var kv in snapshot.parBallId)
            {
                string typeKey = kv.Key;
                int count = kv.Value;

                if (!totauxParType.ContainsKey(typeKey))
                    totauxParType[typeKey] = 0;

                totauxParType[typeKey] += count;

                if (!IsBlackType(typeKey))
                    nonBlackThisFlush += count;
            }
        }

        totalBillesNonNoires += nonBlackThisFlush;

        if (!goalReached &&
            snapshot.isFinalFlush &&
            objectiveThreshold > 0 &&
            totalBillesNonNoires >= objectiveThreshold)
        {
            goalReachedInFinalFlush = true;
        }

        AddPoints(snapshot.totalPointsDuLot);
        CheckGoalReached();
        OnFlushSnapshotRegistered?.Invoke(snapshot);
    }

    private bool IsBlackType(string typeKey)
    {
        if (string.IsNullOrEmpty(typeKey))
            return false;

        return string.Equals(typeKey, "black", StringComparison.OrdinalIgnoreCase);
    }

    private void CheckGoalReached()
    {
        if (goalReached)
            return;

        if (objectiveThreshold <= 0)
            return;

        if (totalBillesNonNoires >= objectiveThreshold)
        {
            goalReached = true;
            Debug.Log("Goal reached (non-black threshold) !");
            onGoalReached?.Invoke();
        }
    }

    public void RegisterComboId(string comboId)
    {
        if (string.IsNullOrEmpty(comboId))
            return;

        combosTriggered.Add(comboId);
    }

    public IReadOnlyCollection<string> GetCombosTriggeredSnapshot()
    {
        return new List<string>(combosTriggered);
    }

    public void RegisterLost(string ballType)
    {
        if (string.IsNullOrEmpty(ballType))
            ballType = "Unknown";

        if (!pertesParType.ContainsKey(ballType))
            pertesParType[ballType] = 0;

        pertesParType[ballType]++;
        totalPertes++;

        OnBallLost?.Invoke(ballType);
    }

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

    public void SetMainGoalReachedTime(int elapsedTimeSec)
    {
        if (elapsedTimeSec < 0)
            elapsedTimeSec = 0;

        if (mainGoalReachedTimeSec < 0)
            mainGoalReachedTimeSec = elapsedTimeSec;
    }

    public void Debug_SetPlannedNonBlack(int count)
    {
        totalBillesPrevues = count;
    }
}