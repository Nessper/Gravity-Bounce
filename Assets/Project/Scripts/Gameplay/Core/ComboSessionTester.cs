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

    // Si true, au lieu d'un flush on simule une perte
    public bool registerLoss;

    // NEW: côté du flush (sélectionnable dans l'Inspector)
    public BinSide binSide = BinSide.Left; // Left / Right

    // Attente avant l'étape suivante (secondes)
    public float delayAfter = 0.5f;
}

[DefaultExecutionOrder(200)]
public class ComboSessionTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ComboEngine comboEngine;

    [Header("Sequence de test")]
    public List<TestAction> actions = new List<TestAction>();

    [Header("Points par type (aligne avec ton JSON)")]
    public int ptsWhite = 100;
    public int ptsBlue = 150;
    public int ptsRed = 200;
    public int ptsBlack = -120;

    [Header("Options")]
    public bool autoStart = false;
    public bool logEachStep = true;
    public bool resetScoreAtStart = true;



    // Recap combos
    private readonly Dictionary<string, int> comboCounts = new Dictionary<string, int>();
    private int totalCombos;
    private int totalBonusPoints;

    private void Awake()
    {
        if (scoreManager == null) scoreManager = Object.FindFirstObjectByType<ScoreManager>();
        // comboEngine: de preference assigne via l'Inspector à la même instance que BinCollector
    }

    private void OnDisable()
    {
        if (comboEngine != null)
            comboEngine.OnComboIdTriggered -= HandleComboTriggered;
    }

    private void Start()
    {
        if (autoStart) StartCoroutine(RunSession());
    }

    private void HandleComboTriggered(string comboId, int points)
    {
        if (!comboCounts.ContainsKey(comboId)) comboCounts[comboId] = 0;
        comboCounts[comboId] += 1;
        totalCombos += 1;
        totalBonusPoints += points;

        if (logEachStep)
            Debug.Log("[ComboSessionTester] Combo: " + comboId + " (+" + points + ")");
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
        totalBonusPoints = 0;
        Debug.Log("[ComboSessionTester] Results cleared.");
    }

    [ContextMenu("Print Summary")]
    public void PrintSummary()
    {
        Debug.Log("----- Combo Summary -----");
        Debug.Log("Total combos: " + totalCombos + ", Total bonus points: " + totalBonusPoints);
        foreach (var kv in comboCounts)
            Debug.Log(kv.Key + ": " + kv.Value);
        Debug.Log("-------------------------");
    }

    private IEnumerator RunSession()
    {
        if (scoreManager == null || comboEngine == null)
        {
            Debug.LogWarning("[ComboSessionTester] Missing references. Assign ScoreManager and ComboEngine in Inspector.");
            yield break;
        }

        if (resetScoreAtStart)
            scoreManager.ResetScore(0);

        comboCounts.Clear();
        totalCombos = 0;
        totalBonusPoints = 0;

        Debug.Log("[ComboSessionTester] Starting session with " + actions.Count + " steps.");

        comboEngine.OnComboIdTriggered -= HandleComboTriggered;
        comboEngine.OnComboIdTriggered += HandleComboTriggered;

        if (logEachStep)
            Debug.Log("[ComboSessionTester] Subscribed inline to ComboEngine #" + comboEngine.GetInstanceID() + " (" + comboEngine.name + ")");

        foreach (var act in actions)
        {
            if (act.registerLoss)
            {
                var fake = new BallState { type = BallType.White, points = ptsWhite };
                scoreManager.RegisterLost(fake);
                if (logEachStep) Debug.Log("[ComboSessionTester] Loss: " + act.label);
            }
            else
            {
                var snap = BuildSnapshot(act);

                if (logEachStep)
                {
                    Debug.Log("[ComboSessionTester] Flush: " + act.label +
                              " Side=" + snap.binSource +
                              " W=" + act.white + " B=" + act.blue + " R=" + act.red + " K=" + act.black +
                              " base=" + snap.totalPointsDuLot);
                }

                // Score de base (comme BinCollector)
                scoreManager.GetSnapshot(snap);

                // Détection des combos
                comboEngine.OnFlush(snap);
            }

            if (act.delayAfter > 0f)
                yield return new WaitForSeconds(act.delayAfter);
        }

        Debug.Log("[ComboSessionTester] Session finished.");
        PrintSummary();
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
            totalPointsDuLot = (act.white * ptsWhite) + (act.blue * ptsBlue) +
                               (act.red * ptsRed) + (act.black * ptsBlack)
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
            s.pointsParType["Black"] = act.black * ptsBlack; // négatif
        }

        typeof(BinSnapshot).GetField("binSource")?.SetValue(s, act.binSide.ToString());

        return s;
    }
}
