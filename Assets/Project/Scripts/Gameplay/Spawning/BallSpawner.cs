using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameObject ballPrefab;

    [Header("Spawn Area & Cadence (fallbacks)")]
    [SerializeField] private float xRange = 4.6f;
    [SerializeField] private float ySpawn = 17.8f;
    [SerializeField] private float zSpawn = -0.2f;
    [SerializeField] private float intervalDefault = 0.6f;
    [SerializeField] private bool spawnAtT0 = false;

    public int PlannedSpawnCount { get; private set; }
    public int CurrentPhaseIndex { get; private set; } = 0;

    public event Action<int, string> OnPhaseChanged; // (index, name)
    public event Action<int> OnPlannedReady;         // total planned
    public event Action<int> OnActivated;            // real activated so far

    private LevelData data;
    private readonly Dictionary<BallType, int> pointsByType = new();

    private struct PhasePlan
    {
        public int Index;
        public string Name;
        public float DurationSec;
        public float Interval;
        public int Quota;
    }
    private readonly List<PhasePlan> plans = new();

    private struct MixEntry { public BallType t; public float w; }
    private readonly List<List<MixEntry>> mixes = new();
    private readonly List<float> mixTotals = new();

    private readonly List<Queue<BallType>> typeQueues = new();

    // Pool
    private readonly Stack<GameObject> pool = new();
    private Coroutine prewarmCoro;
    private Coroutine loop;
    private bool running;

    // Telemetry
    private int plannedTotal;
    private int prewarmedCount;
    private int activatedCount;
    private int recycledCollected;
    private int recycledLost;

    // ------------------ CONFIG ------------------
    public void ConfigureFromLevel(LevelData levelData, float totalRunSec)
    {
        data = levelData;

        // points par type
        pointsByType.Clear();
        if (data?.Balls != null)
        {
            foreach (var b in data.Balls)
            {
                if (string.IsNullOrWhiteSpace(b.Type)) continue;
                if (!Enum.TryParse(b.Type, true, out BallType t)) continue;
                pointsByType[t] = b.Points;
            }
        }
        if (pointsByType.Count == 0) pointsByType[BallType.White] = 100;

        BuildPlansFromWeights(totalRunSec);
        BuildMixes();
        BuildTypeQueues(); // fixe quotas + files de types par phase

        PlannedSpawnCount = plannedTotal;
        prewarmedCount = 0;
        activatedCount = 0;
        recycledCollected = 0;
        recycledLost = 0;
        pool.Clear();
        CurrentPhaseIndex = plans.Count > 0 ? 0 : -1;

        OnPlannedReady?.Invoke(PlannedSpawnCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Spawner/Plan] plannedTotal={plannedTotal} (spawnAtT0={(spawnAtT0 ? 1 : 0)})");
        for (int i = 0; i < plans.Count; i++)
        {
            var p = plans[i];
            Debug.Log($"[Spawner/Plan] Phase {p.Index} \"{p.Name}\": quota={p.Quota}, dur={p.DurationSec:F2}s, iv={p.Interval:F3}s");
        }
#endif
    }

    private void BuildPlansFromWeights(float totalRunSec)
    {
        plans.Clear();
        if (data?.Phases == null || totalRunSec <= 0f) return;

        float sumW = 0f;
        foreach (var ph in data.Phases) sumW += Mathf.Max(0f, ph.Weight);
        if (sumW <= 0f) return;

        float acc = 0f;
        for (int i = 0; i < data.Phases.Length; i++)
        {
            var ph = data.Phases[i];

            float dur = (ph.Weight / sumW) * totalRunSec;
            if (i == data.Phases.Length - 1) dur = Mathf.Max(0f, totalRunSec - acc);

            float iv = ph.Intervalle > 0f ? ph.Intervalle :
                       (data.Spawn != null && data.Spawn.Intervalle > 0f ? data.Spawn.Intervalle : intervalDefault);

            plans.Add(new PhasePlan
            {
                Index = i,
                Name = string.IsNullOrWhiteSpace(ph.Name) ? $"Phase {i + 1}" : ph.Name,
                DurationSec = Mathf.Max(0f, dur),
                Interval = Mathf.Max(0.0001f, iv),
                Quota = 0 // défini ensuite
            });

            acc += dur;
        }
    }

    private void BuildMixes()
    {
        mixes.Clear();
        mixTotals.Clear();
        if (data?.Phases == null) return;

        foreach (var ph in data.Phases)
        {
            var list = new List<MixEntry>();
            float total = 0f;

            if (ph?.Mix != null && ph.Mix.Length > 0)
            {
                foreach (var m in ph.Mix)
                {
                    if (string.IsNullOrWhiteSpace(m.Type)) continue;
                    if (!Enum.TryParse(m.Type, true, out BallType t)) continue;
                    float w = Mathf.Max(0f, m.Poids);
                    if (w <= 0f) continue;
                    list.Add(new MixEntry { t = t, w = w });
                    total += w;
                }
            }
            if (list.Count == 0)
            {
                foreach (var kv in pointsByType) { list.Add(new MixEntry { t = kv.Key, w = 1f }); total += 1f; }
            }

            mixes.Add(list);
            mixTotals.Add(total);
        }
    }

    private void BuildTypeQueues()
    {
        typeQueues.Clear();
        plannedTotal = 0;

        for (int i = 0; i < plans.Count; i++)
        {
            var p = plans[i];
            int quota = Mathf.Max(0, Mathf.FloorToInt(p.DurationSec / p.Interval));
            plans[i] = new PhasePlan { Index = p.Index, Name = p.Name, DurationSec = p.DurationSec, Interval = p.Interval, Quota = quota };
            plannedTotal += quota;
        }
        if (spawnAtT0) plannedTotal += 1;

        for (int i = 0; i < plans.Count; i++)
        {
            int count = plans[i].Quota;
            var mix = mixes[i];
            float totalW = mixTotals[i];

            var queue = new Queue<BallType>(count);
            if (count <= 0 || totalW <= 0f || mix.Count == 0)
            {
                for (int k = 0; k < count; k++) queue.Enqueue(DefaultType());
                typeQueues.Add(queue);
                continue;
            }

            int n = mix.Count;
            int[] alloc = new int[n];
            float[] residuals = new float[n];
            int sum = 0;

            for (int k = 0; k < n; k++)
            {
                float target = (mix[k].w / totalW) * count;
                int baseInt = Mathf.FloorToInt(target);
                alloc[k] = baseInt;
                residuals[k] = target - baseInt;
                sum += baseInt;
            }

            int remain = count - sum;
            if (remain > 0)
            {
                var idx = new List<int>(n);
                for (int k = 0; k < n; k++) idx.Add(k);
                idx.Sort((a, b) => residuals[b].CompareTo(residuals[a]));
                for (int r = 0; r < remain; r++) alloc[idx[r % n]]++;
            }

            int[] left = (int[])alloc.Clone();
            int leftTotal = count;
            int cursor = 0;
            while (leftTotal > 0)
            {
                int tries = 0;
                while (tries < n && left[cursor] == 0) { cursor = (cursor + 1) % n; tries++; }
                if (tries >= n) break;

                queue.Enqueue(mix[cursor].t);
                left[cursor]--;
                leftTotal--;
                cursor = (cursor + 1) % n;
            }
            while (queue.Count < count) queue.Enqueue(DefaultType());
            typeQueues.Add(queue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string breakdown = "";
            for (int k = 0; k < n; k++) breakdown += $"{mix[k].t}:{alloc[k]} ";
            Debug.Log($"[Spawner/Types] Phase {plans[i].Index} \"{plans[i].Name}\" -> {count} | {breakdown}");
#endif
        }
    }

    private BallType DefaultType()
    {
        if (pointsByType.ContainsKey(BallType.White)) return BallType.White;
        foreach (var kv in pointsByType) return kv.Key;
        return BallType.White;
    }

    // ------------------ PREWARM ------------------
    public void StartPrewarm(int budgetPerFrame = 256)
    {
        if (prewarmCoro != null) StopCoroutine(prewarmCoro);
        prewarmCoro = StartCoroutine(PrewarmCoroutine(budgetPerFrame));
    }

    private IEnumerator PrewarmCoroutine(int budgetPerFrame)
    {
        if (ballPrefab == null || PlannedSpawnCount <= 0) { prewarmCoro = null; yield break; }

        int toCreate = PlannedSpawnCount;
        var rt = new WaitForEndOfFrame();

        while (toCreate > 0)
        {
            int batch = Mathf.Min(budgetPerFrame, toCreate);
            for (int i = 0; i < batch; i++)
            {
                var go = Instantiate(ballPrefab);
                go.SetActive(false);
                pool.Push(go);
                prewarmedCount++;
            }
            toCreate -= batch;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Prewarm] {prewarmedCount}/{PlannedSpawnCount}");
#endif

            yield return rt;
        }

        prewarmCoro = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Prewarm] Completed.");
#endif
    }

    // ------------------ RUNTIME ------------------
    public void StartSpawning()
    {
        if (ballPrefab == null) { Debug.LogWarning("[BallSpawner] Aucun prefab assigné."); return; }
        if (plans.Count == 0) { Debug.LogWarning("[BallSpawner] Aucun plan de phase."); return; }
        if (loop != null) return;

        running = true;
        loop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        running = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
        // log final déplacé dans LogStats() (appelé par LevelManager après l’évac)
    }

    private IEnumerator SpawnLoop()
    {
        CurrentPhaseIndex = 0;
        OnPhaseChanged?.Invoke(CurrentPhaseIndex, plans[CurrentPhaseIndex].Name);

        if (spawnAtT0 && running) ActivateOne(CurrentPhaseIndex);

        foreach (var p in plans)
        {
            if (!running) break;

            CurrentPhaseIndex = p.Index;
            OnPhaseChanged?.Invoke(CurrentPhaseIndex, p.Name);

            float tick = 0f, elapsed = 0f;
            while (running && elapsed < p.DurationSec)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                tick += dt;

                if (tick >= p.Interval)
                {
                    ActivateOne(CurrentPhaseIndex);
                    tick = 0f;
                }

                yield return null;
            }
        }

        loop = null;
    }

    private void ActivateOne(int phaseIdx)
    {
        GameObject go = (pool.Count > 0) ? pool.Pop() : Instantiate(ballPrefab);

        float x = UnityEngine.Random.Range(-xRange, xRange);
        go.transform.position = new Vector3(x, ySpawn, zSpawn);

        if (go.TryGetComponent(out Collider col)) col.enabled = true;
        if (go.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;

        BallType type = NextTypeForPhase(phaseIdx);
        int pts = pointsByType.TryGetValue(type, out var p) ? p : 0;

        if (go.TryGetComponent(out BallState st))
        {
            st.inBin = false;
            st.collected = false;
            st.currentSide = Side.None;

            st.Initialize(type, pts);
        }

        go.SetActive(true);

        activatedCount++;
        OnActivated?.Invoke(activatedCount);
        scoreManager?.RegisterRealSpawn();
    }

    private BallType NextTypeForPhase(int phaseIdx)
    {
        if (phaseIdx < 0 || phaseIdx >= typeQueues.Count) return DefaultType();
        var q = typeQueues[phaseIdx];
        return (q != null && q.Count > 0) ? q.Dequeue() : DefaultType();
    }

    // ------------------ RECYCLE ------------------
    public void Recycle(GameObject go, bool collected = false)
    {
        if (go == null) return;

        if (go.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        if (go.TryGetComponent(out Collider col))
            col.enabled = false;

        go.SetActive(false);
        pool.Push(go);

        if (collected) recycledCollected++; else recycledLost++;
    }

    // ------------------ STATS --------------------
    public void LogStats()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpawnStats] Planned={plannedTotal} | Prewarmed={prewarmedCount} | Activated={activatedCount} | Recycled=Collected:{recycledCollected} Lost:{recycledLost}");
#endif
    }
}
