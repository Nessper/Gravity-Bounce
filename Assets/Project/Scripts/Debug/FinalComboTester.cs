using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(210)]
public class FinalComboSessionTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ComboEngine comboEngine;
    [SerializeField] private FinalComboConfig finalComboConfig;

    [Header("Sequence de test (meme format que ComboSessionTester)")]
    public List<TestAction> actions = new List<TestAction>();

    [Header("Points par type (aligne avec ton JSON)")]
    public int ptsWhite = 100;
    public int ptsBlue = 150;
    public int ptsRed = 200;
    public int ptsBlack = -120;

    [Header("Contexte (pour combos de fin)")]
    public int fakeTimerSec = 60;
    public int fakeTotalBilles = 40;

    [Header("Options")]
    public bool autoStart = false;
    public bool logEachStep = true;
    public bool resetScoreAtStart = true;
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

        if (comboEngine == null)
            comboEngine = Object.FindFirstObjectByType<ComboEngine>();
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

        if (finalComboConfig == null)
        {
            Debug.LogWarning("[FinalComboSessionTester] Missing FinalComboConfig ref.");
        }

        if (resetScoreAtStart)
            scoreManager.ResetScore(0);

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        Debug.Log("[FinalComboSessionTester] Starting final-combo test with " + actions.Count + " steps.");
        Debug.Log("[FinalComboSessionTester] ComboEngine = " + (comboEngine != null ? comboEngine.name : "null"));

        int plannedNonBlack = 0;

        foreach (var act in actions)
        {
            if (act.registerLoss)
            {
                // Perte simulee : ici on considere que la bille perdue est noire
                scoreManager.RegisterLost("Black");
                if (logEachStep)
                    Debug.Log("[FinalComboSessionTester] Loss: " + act.label);
            }
            else
            {
                var snap = BuildSnapshot(act);

                if (logEachStep)
                {
                    Debug.Log(
                        "[FinalComboSessionTester] Flush: " + act.label +
                        " Side=" + snap.binSide +
                        " W=" + act.white +
                        " B=" + act.blue +
                        " R=" + act.red +
                        " K=" + act.black +
                        " base=" + snap.totalPointsDuLot
                    );
                }

                // Partie ScoreManager (comme BinCollector)
                scoreManager.GetSnapshot(snap);

                // Partie ComboEngine (live combos, FlushChain, etc.)
                if (comboEngine != null)
                {
                    comboEngine.OnFlush(snap);
                }

                // Comptage des non-noires prevues (debug)
                plannedNonBlack += act.white + act.blue + act.red;
            }

            if (act.delayAfter > 0f)
                yield return new WaitForSeconds(act.delayAfter);
        }

        Debug.Log("[FinalComboSessionTester] Planned non black (from actions) = " + plannedNonBlack);
        Debug.Log("[FinalComboSessionTester] Session finished. Evaluating finals...");

        // Debug: etat du ScoreManager avant evaluation
        Debug.Log(
            "[FinalComboSessionTester] ScoreManager state before finals: " +
            "TotalBilles=" + scoreManager.TotalBilles +
            " TotalNonBlackBilles=" + scoreManager.TotalNonBlackBilles +
            " TotalBillesPrevues=" + scoreManager.TotalBillesPrevues +
            " TotalPertes=" + scoreManager.TotalPertes
        );

        // Contexte pour l evaluator
        var ctx = new FinalComboContext
        {
            timeElapsedSec = fakeTimerSec,
            totalBilles = fakeTotalBilles
        };

        var finals = FinalComboEvaluator.Evaluate(scoreManager, ctx, finalComboConfig);

        int finalsPoints = 0;
        foreach (var r in finals)
        {
            scoreManager.AddPoints(r.points, r.id);
            finalsPoints += r.points;
            Debug.Log("[FINAL COMBO] " + r.id + " +" + r.points);
        }

        Debug.Log("[FinalComboSessionTester] Finals applied: +" + finalsPoints + " points");
        Debug.Log("[FinalComboSessionTester] FinalScore = " + scoreManager.CurrentScore);
    }

    private BinSnapshot BuildSnapshot(TestAction act)
    {
        var s = new BinSnapshot
        {
            binSide = act.binSide,
            timestamp = Time.time,
            parType = new Dictionary<string, int>(),
            pointsParType = new Dictionary<string, int>(),
            nombreDeBilles = act.white + act.blue + act.red + act.black,
            totalPointsDuLot =
                (act.white * ptsWhite) +
                (act.blue * ptsBlue) +
                (act.red * ptsRed) +
                (act.black * ptsBlack)
        };

        if (act.white > 0)
        {
            s.parType["White"] = act.white;
            s.pointsParType["White"] = act.white * ptsWhite;
        }
        if (act.blue > 0)
        {
            s.parType["Blue"] = act.blue;
            s.pointsParType["Blue"] = act.blue * ptsBlue;
        }
        if (act.red > 0)
        {
            s.parType["Red"] = act.red;
            s.pointsParType["Red"] = act.red * ptsRed;
        }
        if (act.black > 0)
        {
            s.parType["Black"] = act.black;
            s.pointsParType["Black"] = act.black * ptsBlack;
        }

        return s;
    }
}
