using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameObject ballPrefab;

    [Header("Spawn Area & Cadence (fallbacks)")]
    [SerializeField] private float xRange = 4.6f;
    [SerializeField] private float ySpawn = 17.8f;
    [SerializeField] private float zSpawn = -0.2f;
    [SerializeField] private float intervalDefault = 0.6f;
    [SerializeField] private bool spawnAtT0 = false;

    public int PlannedSpawnCount { get; private set; }

    private Coroutine loop;
    private bool running;

    private LevelData data;
    private readonly Dictionary<BallType, int> pointsByType = new Dictionary<BallType, int>();

    // 3 phases fixes
    private readonly float[] phaseDur = new float[3];
    private readonly float[] phaseIv = new float[3];
    private readonly float[] phaseEnd = new float[3]; // cumul des durées
    private float levelDur;

    // Mix pondéré par phase
    private struct W { public BallType t; public float w; }
    private readonly List<W>[] mixes = { new List<W>(), new List<W>(), new List<W>() };
    private readonly float[] mixTotals = new float[3];

    // Événement UI de phase
    public event Action<int, string> OnPhaseChanged; // index 0..2, nom
    private readonly string[] phaseNames = new string[3];
    public int CurrentPhaseIndex { get; private set; } = 0;
    public string GetPhaseName(int index) => (index >= 0 && index < 3) ? phaseNames[index] : "";

    // ------------------------------
    //   CONFIGURATION
    // ------------------------------
    public void ConfigureFromLevel(LevelData levelData)
    {
        data = levelData;

        // Points par type (fallback: White=100)
        pointsByType.Clear();
        if (data != null && data.Balls != null)
        {
            foreach (var b in data.Balls)
            {
                if (string.IsNullOrWhiteSpace(b.Type)) continue;
                if (!Enum.TryParse<BallType>(b.Type, true, out var t)) continue;
                pointsByType[t] = b.Points;
            }
        }
        if (pointsByType.Count == 0) pointsByType[BallType.White] = 100;

        // Récupère 3 phases : durées, intervalles, mixes
        for (int i = 0; i < 3; i++)
        {
            PhaseData ph = (data != null && data.Phases != null && i < data.Phases.Length) ? data.Phases[i] : null;

            phaseDur[i] = (ph != null && ph.DurationSec > 0f) ? ph.DurationSec : 0f;

            float iv = intervalDefault;
            if (ph != null && ph.Intervalle > 0f) iv = ph.Intervalle;
            else if (data != null && data.Spawn != null && data.Spawn.Intervalle > 0f) iv = data.Spawn.Intervalle;
            phaseIv[i] = Mathf.Max(0.0001f, iv);

            BuildMix(i, ph);

            phaseNames[i] = (ph != null && !string.IsNullOrWhiteSpace(ph.Name)) ? ph.Name : $"Phase {i + 1}";
        }

        // Cumul & durée totale
        phaseEnd[0] = phaseDur[0];
        phaseEnd[1] = phaseEnd[0] + phaseDur[1];
        phaseEnd[2] = phaseEnd[1] + phaseDur[2];
        levelDur = phaseEnd[2] > 0f ? phaseEnd[2] : (data != null ? Mathf.Max(0f, data.LevelDurationSec) : 0f);

        // Planned via util pur
        PlannedSpawnCount = PlanEstimator.Estimate(phaseDur, phaseIv, spawnAtT0);

        // Phase initiale exposée
        CurrentPhaseIndex = PhaseIndexAtTime(0f);
    }

    // ------------------------------
    //   CYCLE DE VIE
    // ------------------------------
    public void StartSpawning()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[BallSpawner] Aucun prefab assigné.");
            return;
        }
        if (loop == null)
        {
            running = true;
            loop = StartCoroutine(SpawnLoopThreePhases());
        }
    }

    public void StopSpawning()
    {
        running = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    // ------------------------------
    //   BOUCLE DE SPAWN (3 phases)
    // ------------------------------
    private IEnumerator SpawnLoopThreePhases()
    {
        float t = 0f;
        float tick = 0f;

        int lastPhase = PhaseIndexAtTime(0f);
        CurrentPhaseIndex = lastPhase;
        OnPhaseChanged?.Invoke(CurrentPhaseIndex, phaseNames[CurrentPhaseIndex]);

        if (spawnAtT0 && running)
            SpawnOne(CurrentPhaseIndex);

        while (running && (levelDur <= 0f || t < levelDur))
        {
            float dt = Time.deltaTime;
            t += dt;
            if (levelDur > 0f && t >= levelDur) break;

            tick += dt;

            int p = PhaseIndexAtTime(t);
            if (p != lastPhase)
            {
                tick = 0f;                 // réaligne le tempo sur la nouvelle phase
                lastPhase = p;
                CurrentPhaseIndex = p;
                OnPhaseChanged?.Invoke(p, phaseNames[p]);
            }

            if (tick >= phaseIv[p])
            {
                SpawnOne(p);
                tick = 0f;
            }

            yield return null;
        }

        loop = null;
    }

    // ------------------------------
    //   SPAWN & PICK
    // ------------------------------
    private void SpawnOne(int phaseIdx)
    {
        float x = UnityEngine.Random.Range(-xRange, xRange);
        var go = Instantiate(ballPrefab, new Vector3(x, ySpawn, zSpawn), Quaternion.identity);

        scoreManager?.RegisterRealSpawn();

        var st = go.GetComponent<BallState>();
        if (st != null)
        {
            var c = Pick(phaseIdx);
            go.transform.localScale = st.Scale;
            st.Initialize(c.type, c.points);
        }
    }

    private (BallType type, int points) Pick(int phaseIdx)
    {
        var list = mixes[phaseIdx];
        float total = mixTotals[phaseIdx];

        if (list.Count == 0 || total <= 0f)
        {
            foreach (var kv in pointsByType) return (kv.Key, kv.Value);
            return (BallType.White, 100);
        }

        float r = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            acc += list[i].w;
            if (r <= acc)
            {
                int pts = pointsByType.TryGetValue(list[i].t, out var p) ? p : 0;
                return (list[i].t, pts);
            }
        }
        foreach (var kv in pointsByType) return (kv.Key, kv.Value);
        return (BallType.White, 100);
    }

    private void BuildMix(int idx, PhaseData ph)
    {
        var list = mixes[idx];
        list.Clear();
        mixTotals[idx] = 0f;

        if (ph == null || ph.Mix == null || ph.Mix.Length == 0)
        {
            foreach (var kv in pointsByType)
            {
                list.Add(new W { t = kv.Key, w = 1f });
                mixTotals[idx] += 1f;
            }
            return;
        }

        foreach (var m in ph.Mix)
        {
            if (string.IsNullOrWhiteSpace(m.Type)) continue;
            if (!Enum.TryParse<BallType>(m.Type, true, out var t)) continue;
            float w = Mathf.Max(0f, m.Poids);
            if (w <= 0f) continue;
            list.Add(new W { t = t, w = w });
            mixTotals[idx] += w;
        }

        if (list.Count == 0)
        {
            foreach (var kv in pointsByType)
            {
                list.Add(new W { t = kv.Key, w = 1f });
                mixTotals[idx] += 1f;
            }
        }
    }

    // ------------------------------
    //   UTILS DE PHASE
    // ------------------------------
    private int PhaseIndexAtTime(float t)
    {
        if (t < phaseEnd[0]) return 0;
        if (t < phaseEnd[1]) return 1;
        return 2;
    }
}
