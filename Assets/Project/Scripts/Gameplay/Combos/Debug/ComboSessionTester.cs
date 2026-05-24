using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class TestAction
{
    public string label = "Step";

    public int white;
    public int blue;
    public int red;
    public int black;

    public bool registerLoss;

    public BinSide binSide = BinSide.Left;

    public float delayAfter = 0.5f;
}

[DefaultExecutionOrder(200)]
public class ComboSessionTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FlushResolutionEngine flushResolutionEngine;

    [Header("Sequence de test")]
    public List<TestAction> actions = new List<TestAction>();

    [Header("Points par type")]
    public int ptsWhite = 100;
    public int ptsBlue = 150;
    public int ptsRed = 200;
    public int ptsBlack = -120;

    [Header("Options")]
    public bool autoStart = false;
    public bool logEachStep = true;
    public bool resetScoreAtStart = true;
    public bool resetCombosAtStart = true;

    private readonly Dictionary<string, int> comboCounts =
        new Dictionary<string, int>();

    private int totalCombos;
    private int totalComboPoints;
    private int totalBasePoints;
    private int totalFinalPoints;

    private void Awake()
    {
        if (scoreManager == null)
            scoreManager = Object.FindFirstObjectByType<ScoreManager>();

        if (flushResolutionEngine == null)
            flushResolutionEngine = Object.FindFirstObjectByType<FlushResolutionEngine>();
    }

    private void OnEnable()
    {
        if (flushResolutionEngine != null)
            flushResolutionEngine.OnFlushResolved += HandleFlushResolved;
    }

    private void OnDisable()
    {
        if (flushResolutionEngine != null)
            flushResolutionEngine.OnFlushResolved -= HandleFlushResolved;
    }

    private void Start()
    {
        if (autoStart)
            StartCoroutine(RunSession());
    }

    [ContextMenu("Run Test Session")]
    public void RunTestContext()
    {
        StartCoroutine(RunSession());
    }

    [ContextMenu("Clear Results")]
    public void ClearResults()
    {
        comboCounts.Clear();

        totalCombos = 0;
        totalComboPoints = 0;
        totalBasePoints = 0;
        totalFinalPoints = 0;

        Debug.Log("[ComboSessionTester] Results cleared.");
    }

    [ContextMenu("Print Summary")]
    public void PrintSummary()
    {
        Debug.Log("----- Combo Summary -----");
        Debug.Log($"Total combos: {totalCombos}");
        Debug.Log($"Total base points: {totalBasePoints}");
        Debug.Log($"Total combo points: {totalComboPoints}");
        Debug.Log($"Total final points: {totalFinalPoints}");

        foreach (var kv in comboCounts)
            Debug.Log($"{kv.Key}: {kv.Value}");

        Debug.Log("-------------------------");
    }

    private IEnumerator RunSession()
    {
        if (scoreManager == null || flushResolutionEngine == null)
        {
            Debug.LogWarning(
                "[ComboSessionTester] Missing references.");

            yield break;
        }

        if (resetScoreAtStart)
            scoreManager.ResetScore(0);

        if (resetCombosAtStart)
            flushResolutionEngine.ResetRuntimeState();

        ClearResults();

        Debug.Log(
            $"[ComboSessionTester] Starting session with {actions.Count} steps.");

        for (int i = 0; i < actions.Count; i++)
        {
            TestAction act = actions[i];

            if (act == null)
                continue;

            if (act.registerLoss)
                RegisterLoss(act);
            else
                RunFlush(act);

            if (act.delayAfter > 0f)
                yield return new WaitForSeconds(act.delayAfter);
        }

        Debug.Log("[ComboSessionTester] Session finished.");
        PrintSummary();
    }

    private void RunFlush(TestAction act)
    {
        BinSnapshot snap = BuildSnapshot(act);

        if (logEachStep)
        {
            Debug.Log(
                $"[ComboSessionTester] Flush: {act.label} " +
                $"Side={snap.binSide} " +
                $"W={act.white} B={act.blue} R={act.red} K={act.black} " +
                $"base={snap.totalPointsDuLot}");
        }

        flushResolutionEngine.OnFlush(snap);
    }

    private void RegisterLoss(TestAction act)
    {
        if (scoreManager == null)
            return;

        scoreManager.RegisterLost("white");

        if (logEachStep)
            Debug.Log($"[ComboSessionTester] Loss: {act.label}");
    }

    private void HandleFlushResolved(FlushResolution resolution)
    {
        if (resolution == null)
            return;

        totalBasePoints += resolution.BaseTotal;
        totalComboPoints += resolution.ComboTotal;
        totalFinalPoints += resolution.FinalTotal;

        if (resolution.ComboEvents == null)
            return;

        for (int i = 0; i < resolution.ComboEvents.Count; i++)
        {
            ComboEvent combo = resolution.ComboEvents[i];

            if (!comboCounts.ContainsKey(combo.Id))
                comboCounts[combo.Id] = 0;

            comboCounts[combo.Id] += 1;
            totalCombos += 1;

            if (logEachStep)
            {
                Debug.Log(
                    $"[ComboSessionTester] Combo: " +
                    $"{combo.Id} +{combo.Points} " +
                    $"Family={combo.Family} " +
                    $"Chain={combo.ChainValue}");
            }
        }
    }

    private BinSnapshot BuildSnapshot(TestAction act)
    {
        BinSnapshot s = new BinSnapshot
        {
            binSide = act.binSide,
            timestamp = Time.time,

            parBallId = new Dictionary<string, int>(),
            pointsParBallId = new Dictionary<string, int>(),

            nombreDeBilles =
                act.white + act.blue + act.red + act.black,

            totalPointsDuLot =
                (act.white * ptsWhite) +
                (act.blue * ptsBlue) +
                (act.red * ptsRed) +
                (act.black * ptsBlack),

            isFinalFlush = false
        };

        AddBall(s, "white", act.white, ptsWhite);
        AddBall(s, "blue", act.blue, ptsBlue);
        AddBall(s, "red", act.red, ptsRed);
        AddBall(s, "black", act.black, ptsBlack);

        return s;
    }

    private void AddBall(
        BinSnapshot snapshot,
        string ballId,
        int count,
        int pointsPerBall)
    {
        if (snapshot == null)
            return;

        if (count <= 0)
            return;

        snapshot.parBallId[ballId] = count;
        snapshot.pointsParBallId[ballId] =
            count * pointsPerBall;
    }
}