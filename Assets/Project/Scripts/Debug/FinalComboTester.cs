using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(210)]
public class FinalComboSessionTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FlushResolutionEngine flushResolutionEngine;
    [SerializeField] private FinalComboConfig finalComboConfig;

    [Header("Sequence de test")]
    public List<TestAction> actions = new List<TestAction>();

    [Header("Points par type")]
    public int ptsWhite = 100;
    public int ptsBlue = 150;
    public int ptsRed = 200;
    public int ptsBlack = -120;

    [Header("Contexte pour combos de fin")]
    public int fakeTimerSec = 60;
    public int fakeTotalBilles = 40;

    [Header("Options")]
    public bool autoStart = false;
    public bool logEachStep = true;
    public bool resetScoreAtStart = true;
    public bool resetCombosAtStart = true;
    public float startDelay = 0.25f;

    [ContextMenu("Run Final Test")]
    public void RunTestContext()
    {
        StartCoroutine(RunSessionAndEvaluate());
    }

    private void Awake()
    {
        if (scoreManager == null)
            scoreManager = Object.FindFirstObjectByType<ScoreManager>();

        if (flushResolutionEngine == null)
            flushResolutionEngine = Object.FindFirstObjectByType<FlushResolutionEngine>();
    }

    private void Start()
    {
        if (autoStart)
            StartCoroutine(RunSessionAndEvaluate());
    }

    private IEnumerator RunSessionAndEvaluate()
    {
        if (scoreManager == null)
        {
            Debug.LogWarning("[FinalComboSessionTester] Missing ScoreManager ref.");
            yield break;
        }

        if (flushResolutionEngine == null)
        {
            Debug.LogWarning("[FinalComboSessionTester] Missing FlushResolutionEngine ref.");
            yield break;
        }

        if (finalComboConfig == null)
        {
            Debug.LogWarning("[FinalComboSessionTester] Missing FinalComboConfig ref.");
        }

        if (resetScoreAtStart)
            scoreManager.ResetScore(0);

        if (resetCombosAtStart)
            flushResolutionEngine.ResetRuntimeState();

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        Debug.Log("[FinalComboSessionTester] Starting final-combo test with " + actions.Count + " steps.");

        int plannedNonBlack = 0;

        for (int i = 0; i < actions.Count; i++)
        {
            TestAction act = actions[i];

            if (act == null)
                continue;

            if (act.registerLoss)
            {
                RegisterLoss(act);
            }
            else
            {
                RunFlush(act);
                plannedNonBlack += act.white + act.blue + act.red;
            }

            if (act.delayAfter > 0f)
                yield return new WaitForSeconds(act.delayAfter);
        }

        Debug.Log("[FinalComboSessionTester] Planned non black from actions = " + plannedNonBlack);
        Debug.Log("[FinalComboSessionTester] Session finished. Evaluating finals...");

        Debug.Log(
            "[FinalComboSessionTester] ScoreManager state before finals: " +
            "TotalBilles=" + scoreManager.TotalBilles +
            " TotalNonBlackBilles=" + scoreManager.TotalNonBlackBilles +
            " TotalBillesPrevues=" + scoreManager.TotalBillesPrevues +
            " TotalPertes=" + scoreManager.TotalPertes);

        FinalComboContext ctx = new FinalComboContext
        {
            timeElapsedSec = fakeTimerSec,
            totalBilles = fakeTotalBilles
        };

        List<FinalComboResult> finals =
            FinalComboEvaluator.Evaluate(
                scoreManager,
                ctx,
                finalComboConfig);

        int finalsPoints = 0;

        for (int i = 0; i < finals.Count; i++)
        {
            FinalComboResult result = finals[i];

            scoreManager.AddPoints(result.points, result.id);
            finalsPoints += result.points;

            Debug.Log("[FINAL COMBO] " + result.id + " +" + result.points);
        }

        Debug.Log("[FinalComboSessionTester] Finals applied: +" + finalsPoints + " points");
        Debug.Log("[FinalComboSessionTester] FinalScore = " + scoreManager.CurrentScore);
    }

    private void RunFlush(TestAction act)
    {
        BinSnapshot snap = BuildSnapshot(act);

        if (logEachStep)
        {
            Debug.Log(
                "[FinalComboSessionTester] Flush: " + act.label +
                " Side=" + snap.binSide +
                " W=" + act.white +
                " B=" + act.blue +
                " R=" + act.red +
                " K=" + act.black +
                " base=" + snap.totalPointsDuLot);
        }

        flushResolutionEngine.OnFlush(snap);
    }

    private void RegisterLoss(TestAction act)
    {
        scoreManager.RegisterLost("black");

        if (logEachStep)
            Debug.Log("[FinalComboSessionTester] Loss: " + act.label);
    }

    private BinSnapshot BuildSnapshot(TestAction act)
    {
        BinSnapshot s = new BinSnapshot
        {
            binSide = act.binSide,
            timestamp = Time.time,
            parBallId = new Dictionary<string, int>(),
            pointsParBallId = new Dictionary<string, int>(),
            nombreDeBilles = act.white + act.blue + act.red + act.black,
            totalPointsDuLot =
                (act.white * ptsWhite) +
                (act.blue * ptsBlue) +
                (act.red * ptsRed) +
                (act.black * ptsBlack)
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