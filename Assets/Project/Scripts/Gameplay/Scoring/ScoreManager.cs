using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    [Header("Planned Target")]
    [SerializeField] private int plannedProgressBalls;
    public int TotalBillesPrevues => plannedProgressBalls;

    [Header("Main Objective")]
    [SerializeField] private int objectiveThreshold;
    public int ObjectiveThreshold => objectiveThreshold;

    [Serializable]
    public class SimpleEvent : UnityEvent { }

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    public SimpleEvent onGoalReached = new SimpleEvent();

    [HideInInspector]
    public IntEvent onScoreChanged = new IntEvent();

    private int totalBallsCollected;
    private int totalProgressBallsCollected;
    private int totalBallsLost;
    private int currentScore;
    private int realSpawned;

    private bool goalReached;
    private bool goalReachedInFinalFlush;

    private int mainGoalReachedTimeSec = -1;

    private readonly Dictionary<string, int> collectedByBallId =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> lostByBallId =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private readonly List<BinSnapshot> historique =
        new List<BinSnapshot>();

    private readonly HashSet<string> combosTriggered =
        new HashSet<string>();

    public int TotalBilles => totalBallsCollected;
    public int TotalNonBlackBilles => totalProgressBallsCollected;
    public int TotalPertes => totalBallsLost;
    public int CurrentScore => currentScore;
    public int MainGoalReachedTimeSec => mainGoalReachedTimeSec;
    public bool MainGoalAchieved => goalReached;
    public bool GoalReachedInFinalFlush => goalReachedInFinalFlush;

    public int GetRealSpawned()
    {
        return realSpawned;
    }

    public event Action<string> OnBallLost;
    public event Action<BinSnapshot> OnFlushSnapshotRegistered;

    public IReadOnlyDictionary<string, int> GetTotalsByTypeSnapshot()
    {
        return new Dictionary<string, int>(collectedByBallId);
    }

    public IReadOnlyDictionary<string, int> GetLossesByTypeSnapshot()
    {
        return new Dictionary<string, int>(lostByBallId);
    }

    public List<BinSnapshot> GetHistoriqueSnapshot()
    {
        return new List<BinSnapshot>(historique);
    }

    public void SetPlannedBalls(int count)
    {
        plannedProgressBalls = Mathf.Max(0, count);
    }

    public void SetObjectiveThreshold(int threshold)
    {
        objectiveThreshold = Mathf.Max(0, threshold);
        goalReached = false;
    }

    public void ResetScore(int start = 0)
    {
        currentScore = start;

        totalBallsCollected = 0;
        totalProgressBallsCollected = 0;
        totalBallsLost = 0;
        realSpawned = 0;

        goalReached = false;
        goalReachedInFinalFlush = false;
        mainGoalReachedTimeSec = -1;

        collectedByBallId.Clear();
        lostByBallId.Clear();
        historique.Clear();
        combosTriggered.Clear();

        onScoreChanged?.Invoke(currentScore);
    }

    public void ResetForLevelStart(int startScore = 0)
    {
        ResetScore(startScore);
    }

    public void RegisterRealSpawn()
    {
        realSpawned++;
    }

    public void AddPoints(int amount, string sourceId = null)
    {
        currentScore += amount;
        onScoreChanged?.Invoke(currentScore);
    }

    public void GetSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0)
            return;

        historique.Add(snapshot);

        totalBallsCollected += snapshot.nombreDeBilles;

        int progressBallsThisFlush =
            CountProgressBalls(snapshot);

        totalProgressBallsCollected += progressBallsThisFlush;

        AddCollectedTotals(snapshot);

        if (!goalReached &&
            snapshot.isFinalFlush &&
            objectiveThreshold > 0 &&
            totalProgressBallsCollected >= objectiveThreshold)
        {
            goalReachedInFinalFlush = true;
        }

        AddPoints(snapshot.totalPointsDuLot);

        CheckGoalReached();

        OnFlushSnapshotRegistered?.Invoke(snapshot);
    }

    private int CountProgressBalls(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.parBallId == null)
            return 0;

        int count = 0;

        foreach (KeyValuePair<string, int> kv in snapshot.parBallId)
        {
            if (IsDangerBallId(kv.Key))
                continue;

            count += kv.Value;
        }

        return count;
    }

    private void AddCollectedTotals(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.parBallId == null)
            return;

        foreach (KeyValuePair<string, int> kv in snapshot.parBallId)
        {
            AddToDictionary(
                collectedByBallId,
                kv.Key,
                kv.Value);
        }
    }

    private bool IsDangerBallId(string ballId)
    {
        if (string.IsNullOrWhiteSpace(ballId))
            return false;

        return string.Equals(
            ballId,
            "black",
            StringComparison.OrdinalIgnoreCase);
    }

    private void CheckGoalReached()
    {
        if (goalReached)
            return;

        if (objectiveThreshold <= 0)
            return;

        if (totalProgressBallsCollected >= objectiveThreshold)
        {
            goalReached = true;
            Debug.Log("Goal reached!");
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

    public void RegisterLost(string ballId)
    {
        if (string.IsNullOrWhiteSpace(ballId))
            ballId = "unknown";

        AddToDictionary(
            lostByBallId,
            ballId,
            1);

        totalBallsLost++;

        OnBallLost?.Invoke(ballId);
    }

    public EndLevelStats BuildEndLevelStats(int timeElapsedSec)
    {
        return new EndLevelStats
        {
            TimeElapsedSec = Mathf.Max(0, timeElapsedSec),
            BallsCollected = totalBallsCollected,
            BallsLost = totalBallsLost,
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
        plannedProgressBalls = Mathf.Max(0, count);
    }

    private void AddToDictionary(
        Dictionary<string, int> dict,
        string key,
        int amount)
    {
        if (dict == null || string.IsNullOrWhiteSpace(key))
            return;

        if (!dict.TryGetValue(key, out int current))
            dict[key] = amount;
        else
            dict[key] = current + amount;
    }
}