using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Public phase planning info exposed for UIs (IntroLevelUI, debug panels, etc.).
/// This is an immutable "view" of the internal spawn plan.
/// </summary>
[Serializable]
public struct PhasePlanInfo
{
    public int Index;
    public string Name;
    public float DurationSec;
    public float IntervalSec;
    public int Quota;
}

/// <summary>
/// BallSpawner:
/// - Construit un plan par phase (duree via weights, intervalle via JSON).
/// - Construit des queues de types discretes (quota exact).
/// - Spawn runtime en mode quota-driven (exact, sans derive dt).
/// </summary>
public class BallSpawner : MonoBehaviour
{
    // =====================================================================
    // SERIALIZED REFERENCES
    // =====================================================================

    [Header("Refs")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform ballsParent;

    [Header("Camera / Ceiling")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Collider ceilingCollider;
    [SerializeField] private float spawnOffsetAboveScreen = 0.3f;

    [Header("Spawn Area & Cadence (fallbacks)")]
    [SerializeField] private float xRange = 2.18f;
    [SerializeField] private float ySpawn = 6.3f;
    [SerializeField] private float zSpawn = -0.2f;
    [SerializeField] private float intervalDefault = 0.6f;

    // =====================================================================
    // PUBLIC STATE / EVENTS
    // =====================================================================

    public int PlannedSpawnCount { get; private set; }
    public int PlannedNonBlackSpawnCount { get; private set; }
    public int PlannedBlackSpawnCount { get; private set; }

    public int CurrentPhaseIndex { get; private set; } = 0;

    public event Action<int, string> OnPhaseChanged;
    public event Action<int> OnPlannedReady;
    public event Action<int> OnActivated;

    // =====================================================================
    // INTERNAL DATA
    // =====================================================================

    private LevelData data;

    private readonly Dictionary<BallType, int> pointsByType = new Dictionary<BallType, int>();

    private struct PhasePlan
    {
        public int Index;
        public string Name;
        public float DurationSec;
        public float Interval;
        public int Quota;
        public float Weight;
    }

    private readonly List<PhasePlan> plans = new List<PhasePlan>();

    private struct MixEntry
    {
        public BallType t;
        public float w;
    }

    private readonly List<List<MixEntry>> mixes = new List<List<MixEntry>>();
    private readonly List<float> mixTotals = new List<float>();
    private readonly List<Queue<BallType>> typeQueues = new List<Queue<BallType>>();

    private readonly Stack<GameObject> pool = new Stack<GameObject>();

    private Coroutine prewarmCoro;
    private Coroutine loop;
    private bool running;

    private int plannedTotal;
    private int prewarmedCount;
    private int activatedCount;
    private int recycledCollected;
    private int recycledLost;

    private PhasePlanInfo[] publicPhasePlans = Array.Empty<PhasePlanInfo>();

    // =====================================================================
    // CONFIGURATION
    // =====================================================================

    public void ConfigureFromLevel(LevelData levelData, float totalRunSec)
    {
        data = levelData;

        BuildPointsByType();

        // 1) Durees par phase via weights (depend du vaisseau via totalRunSec)
        BuildPlansFromWeights(totalRunSec);

        // 2) Mix par phase
        BuildMixes();

        // 3) Quotas exacts par phase en fonction de (duree / intervalle JSON)
        BuildTypeQueues();

        PlannedSpawnCount = plannedTotal;

        prewarmedCount = 0;
        activatedCount = 0;
        recycledCollected = 0;
        recycledLost = 0;
        pool.Clear();
        CurrentPhaseIndex = plans.Count > 0 ? 0 : -1;

        OnPlannedReady?.Invoke(PlannedSpawnCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Spawner/Plan] plannedTotal={plannedTotal}");
        Debug.Log($"[Spawner/Plan] plannedNonBlack={PlannedNonBlackSpawnCount}, plannedBlack={PlannedBlackSpawnCount}");
        for (int i = 0; i < plans.Count; i++)
        {
            var p = plans[i];
            Debug.Log($"[Spawner/Plan] Phase {p.Index} \"{p.Name}\": quota={p.Quota}, dur={p.DurationSec:F2}s, iv={p.Interval:F3}s");
        }
#endif
    }

    private void BuildPointsByType()
    {
        pointsByType.Clear();

        if (data?.Balls != null)
        {
            foreach (var b in data.Balls)
            {
                if (string.IsNullOrWhiteSpace(b.Type))
                    continue;

                if (!Enum.TryParse(b.Type, true, out BallType t))
                    continue;

                pointsByType[t] = b.Points;
            }
        }

        if (pointsByType.Count == 0)
            pointsByType[BallType.White] = 100;
    }

    private float GetGlobalSpawnInterval()
    {
        if (data != null && data.Spawn != null && data.Spawn.Intervalle > 0f)
            return data.Spawn.Intervalle;

        return intervalDefault;
    }

    private float GetDesignIntervalForPhase(int phaseIndex)
    {
        // Priorite 1: intervalle de phase (JSON)
        if (data != null && data.Phases != null && phaseIndex >= 0 && phaseIndex < data.Phases.Length)
        {
            float phaseIv = data.Phases[phaseIndex].Intervalle;
            if (phaseIv > 0f)
                return phaseIv;
        }

        // Priorite 2: intervalle global (JSON)
        return GetGlobalSpawnInterval();
    }

    private void BuildPlansFromWeights(float totalRunSec)
    {
        plans.Clear();

        if (data?.Phases == null || totalRunSec <= 0f)
            return;

        float sumW = 0f;
        foreach (var ph in data.Phases)
            sumW += Mathf.Max(0f, ph.Weight);

        if (sumW <= 0f)
            return;

        float accumulated = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            var ph = data.Phases[i];

            float dur = (ph.Weight / sumW) * totalRunSec;

            // Derniere phase: recupere le reste exact
            if (i == data.Phases.Length - 1)
                dur = Mathf.Max(0f, totalRunSec - accumulated);

            float iv = GetDesignIntervalForPhase(i);

            plans.Add(new PhasePlan
            {
                Index = i,
                Name = string.IsNullOrWhiteSpace(ph.Name) ? $"Phase {i + 1}" : ph.Name,
                DurationSec = Mathf.Max(0f, dur),
                Interval = Mathf.Max(0.0001f, iv),
                Quota = 0,
                Weight = Mathf.Max(0f, ph.Weight)
            });

            accumulated += dur;
        }
    }

    private void BuildMixes()
    {
        mixes.Clear();
        mixTotals.Clear();

        if (data?.Phases == null)
            return;

        foreach (var ph in data.Phases)
        {
            var list = new List<MixEntry>();
            float total = 0f;

            if (ph?.Mix != null && ph.Mix.Length > 0)
            {
                foreach (var m in ph.Mix)
                {
                    if (string.IsNullOrWhiteSpace(m.Type))
                        continue;

                    if (!Enum.TryParse(m.Type, true, out BallType t))
                        continue;

                    float w = Mathf.Max(0f, m.Poids);
                    if (w <= 0f)
                        continue;

                    list.Add(new MixEntry { t = t, w = w });
                    total += w;
                }
            }

            if (list.Count == 0)
            {
                foreach (var kv in pointsByType)
                {
                    list.Add(new MixEntry { t = kv.Key, w = 1f });
                    total += 1f;
                }
            }

            mixes.Add(list);
            mixTotals.Add(total);
        }
    }

    private int ComputePhaseQuota(float durationSec, float intervalSec)
    {
        if (durationSec <= 0f || intervalSec <= 0f)
            return 0;

        float eps = 0.0001f;

        // Spawns a: interval, 2*interval, ... tant que < duration
        int count = Mathf.FloorToInt((durationSec - eps) / intervalSec);
        return Mathf.Max(0, count);
    }


    private void BuildTypeQueues()
    {
        typeQueues.Clear();
        plannedTotal = 0;

        PlannedNonBlackSpawnCount = 0;
        PlannedBlackSpawnCount = 0;

        // 1) Quota par phase = f(duree, intervalle JSON)
        for (int i = 0; i < plans.Count; i++)
        {
            var p = plans[i];

            // Intervalle = valeur JSON (phase ou global)
            p.Interval = Mathf.Max(0.0001f, GetDesignIntervalForPhase(i));

            int quota = ComputePhaseQuota(p.DurationSec, p.Interval);

            p.Quota = quota;
            plans[i] = p;

            plannedTotal += quota;
        }

        // 2) Construire les queues de types discretes selon quota et mix
        for (int i = 0; i < plans.Count; i++)
        {
            int count = plans[i].Quota;
            var mix = mixes[i];
            float totalW = mixTotals[i];

            var queue = new Queue<BallType>(count);

            if (count <= 0 || totalW <= 0f || mix.Count == 0)
            {
                if (count > 0)
                {
                    BallType def = DefaultType();
                    for (int k = 0; k < count; k++)
                        queue.Enqueue(def);

                    if (def == BallType.Black)
                        PlannedBlackSpawnCount += count;
                    else
                        PlannedNonBlackSpawnCount += count;
                }

                typeQueues.Add(queue);
                continue;
            }

            int n = mix.Count;
            int[] alloc = new int[n];
            float[] residuals = new float[n];
            int sum = 0;

            // Base floor
            for (int k = 0; k < n; k++)
            {
                float target = (mix[k].w / totalW) * count;
                int baseInt = Mathf.FloorToInt(target);
                alloc[k] = baseInt;
                residuals[k] = target - baseInt;
                sum += baseInt;
            }

            // Plus grands residus
            int remain = count - sum;
            if (remain > 0)
            {
                var idx = new List<int>(n);
                for (int k = 0; k < n; k++)
                    idx.Add(k);

                idx.Sort((a, b) => residuals[b].CompareTo(residuals[a]));

                for (int r = 0; r < remain; r++)
                    alloc[idx[r % n]]++;
            }

            // Stats black vs non-black
            for (int k = 0; k < n; k++)
            {
                if (mix[k].t == BallType.Black)
                    PlannedBlackSpawnCount += alloc[k];
                else
                    PlannedNonBlackSpawnCount += alloc[k];
            }

            // Interleave simple
            int[] left = (int[])alloc.Clone();
            int leftTotal = count;
            int cursor = 0;

            while (leftTotal > 0)
            {
                int tries = 0;

                while (tries < n && left[cursor] == 0)
                {
                    cursor = (cursor + 1) % n;
                    tries++;
                }

                if (tries >= n)
                    break;

                queue.Enqueue(mix[cursor].t);
                left[cursor]--;
                leftTotal--;
                cursor = (cursor + 1) % n;
            }

            while (queue.Count < count)
            {
                BallType def = DefaultType();
                queue.Enqueue(def);

                if (def == BallType.Black)
                    PlannedBlackSpawnCount++;
                else
                    PlannedNonBlackSpawnCount++;
            }

            typeQueues.Add(queue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string breakdown = "";
            for (int k = 0; k < n; k++)
                breakdown += $"{mix[k].t}:{alloc[k]} ";

            Debug.Log($"[Spawner/Types] Phase {plans[i].Index} \"{plans[i].Name}\" -> {count} | {breakdown}");
#endif
        }

        // 3) Public plan copy
        publicPhasePlans = new PhasePlanInfo[plans.Count];
        for (int i = 0; i < plans.Count; i++)
        {
            var p = plans[i];
            publicPhasePlans[i] = new PhasePlanInfo
            {
                Index = p.Index,
                Name = p.Name,
                DurationSec = p.DurationSec,
                IntervalSec = p.Interval,
                Quota = p.Quota
            };
        }
    }

    private BallType DefaultType()
    {
        if (pointsByType.ContainsKey(BallType.White))
            return BallType.White;

        foreach (var kv in pointsByType)
            return kv.Key;

        return BallType.White;
    }

    // =====================================================================
    // PREWARM
    // =====================================================================

    public void StartPrewarm(int budgetPerFrame = 256)
    {
        if (prewarmCoro != null)
            StopCoroutine(prewarmCoro);

        prewarmCoro = StartCoroutine(PrewarmCoroutine(budgetPerFrame));
    }

    private IEnumerator PrewarmCoroutine(int budgetPerFrame)
    {
        if (ballPrefab == null || PlannedSpawnCount <= 0)
        {
            prewarmCoro = null;
            yield break;
        }

        int toCreate = PlannedSpawnCount;
        WaitForEndOfFrame rt = new WaitForEndOfFrame();

        while (toCreate > 0)
        {
            int batch = Mathf.Min(budgetPerFrame, toCreate);

            for (int i = 0; i < batch; i++)
            {
                GameObject go = Instantiate(ballPrefab, ballsParent, false);
                go.SetActive(false);
                pool.Push(go);
                prewarmedCount++;
            }

            toCreate -= batch;
            yield return rt;
        }

        prewarmCoro = null;
    }

    // =====================================================================
    // RUNTIME SPAWNING
    // =====================================================================

    public void StartSpawning()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[BallSpawner] No ballPrefab assigned.");
            return;
        }

        if (plans.Count == 0)
        {
            Debug.LogWarning("[BallSpawner] No phase plan. Did you call ConfigureFromLevel?");
            return;
        }

        if (loop != null)
            return;

        running = true;
        loop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        running = false;

        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        // Quota-driven: exact.
        for (int phase = 0; phase < plans.Count; phase++)
        {
            if (!running)
                break;

            var p = plans[phase];

            CurrentPhaseIndex = p.Index;
            OnPhaseChanged?.Invoke(CurrentPhaseIndex, p.Name);

            int toSpawn = Mathf.Max(0, p.Quota);

            for (int s = 0; s < toSpawn && running; s++)
            {
                yield return WaitSecondsAccurate(p.Interval);
                if (!running)
                    break;

                ActivateOne(CurrentPhaseIndex);
            }

        }

        loop = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (activatedCount != plannedTotal)
            Debug.LogError($"[Spawner/Runtime] Activated mismatch: activated={activatedCount}, planned={plannedTotal}");
#endif
    }

    private IEnumerator WaitSecondsAccurate(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float t = 0f;
        while (running && t < seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    // =====================================================================
    // SPAWN POSITION
    // =====================================================================

    private float ComputeSpawnY()
    {
        if (gameplayCamera != null && gameplayCamera.orthographic)
        {
            float topWorldY = gameplayCamera.transform.position.y + gameplayCamera.orthographicSize;
            return topWorldY + spawnOffsetAboveScreen;
        }

        return ySpawn;
    }

    private void ActivateOne(int phaseIdx)
    {
        GameObject go = (pool.Count > 0) ? pool.Pop() : Instantiate(ballPrefab);

        float x = UnityEngine.Random.Range(-xRange, xRange);
        float spawnY = ComputeSpawnY();
        go.transform.position = new Vector3(x, spawnY, zSpawn);

        go.SetActive(true);

        if (go.TryGetComponent(out Collider col))
            col.enabled = true;

        if (go.TryGetComponent(out Rigidbody rb))
            rb.isKinematic = false;

        if (go.TryGetComponent(out BallCeilingGrace grace))
        {
            grace.SetCeiling(ceilingCollider);
            grace.StartGrace();
        }

        BallType type = NextTypeForPhase(phaseIdx);
        int pts = pointsByType.TryGetValue(type, out int p) ? p : 0;

        if (go.TryGetComponent(out BallState st))
        {
            st.inBin = false;
            st.collected = false;
            st.currentSide = Side.None;
            st.Initialize(type, pts);
        }

        activatedCount++;
        OnActivated?.Invoke(activatedCount);
        scoreManager?.RegisterRealSpawn();
    }

    private BallType NextTypeForPhase(int phaseIdx)
    {
        if (phaseIdx < 0 || phaseIdx >= typeQueues.Count)
            return DefaultType();

        Queue<BallType> q = typeQueues[phaseIdx];
        if (q == null || q.Count == 0)
            return DefaultType();

        return q.Dequeue();
    }

    // =====================================================================
    // RECYCLE / POOLING
    // =====================================================================

    public void Recycle(GameObject go, bool collected = false)
    {
        if (go == null)
            return;

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

        if (collected)
            recycledCollected++;
        else
            recycledLost++;
    }

    // =====================================================================
    // STATS / PUBLIC PLAN VIEW
    // =====================================================================

    public PhasePlanInfo[] GetPhasePlans()
    {
        if (publicPhasePlans == null || publicPhasePlans.Length == 0)
            return Array.Empty<PhasePlanInfo>();

        PhasePlanInfo[] copy = new PhasePlanInfo[publicPhasePlans.Length];
        Array.Copy(publicPhasePlans, copy, publicPhasePlans.Length);
        return copy;
    }

    // =====================================================================
    // STATS
    // =====================================================================

    public void LogStats()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "[SpawnStats] Planned=" + plannedTotal +
            " | Prewarmed=" + prewarmedCount +
            " | Activated=" + activatedCount +
            " | Recycled=Collected:" + recycledCollected +
            " Lost:" + recycledLost
        );
#endif
    }

}
