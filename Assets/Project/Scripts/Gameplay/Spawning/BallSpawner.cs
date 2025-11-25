using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Public phase planning info exposed for UIs (IntroLevelUI, debug panels, etc.).
/// This is a readonly "view" of the internal spawn plan.
/// </summary>
[Serializable]
public struct PhasePlanInfo
{
    /// <summary>Index of the phase in the sequence (0, 1, 2, ...).</summary>
    public int Index;

    /// <summary>Readable name of the phase (ex: "PHASE 1").</summary>
    public string Name;

    /// <summary>Planned duration of the phase in seconds.</summary>
    public float DurationSec;

    /// <summary>Spawn interval used for this phase (seconds between spawns).</summary>
    public float IntervalSec;

    /// <summary>Total number of balls planned for this phase.</summary>
    public int Quota;
}

/// <summary>
/// BallSpawner is responsible for:
/// - Building a spawn plan per phase based on LevelData (weights, intervals, mixes).
/// - Prewarming a pool of balls.
/// - Spawning balls over time according to the plan.
/// - Recycling balls back into the pool.
/// - Exposing telemetry (planned vs real spawns) and phase plan data for UIs.
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


    [Header("Spawn Area & Cadence (fallbacks)")]
    [SerializeField] private float xRange = 2.18f;
    [SerializeField] private float ySpawn = 6.3f;
    [SerializeField] private float zSpawn = -0.2f;
    [SerializeField] private float intervalDefault = 0.6f;
    [SerializeField] private bool spawnAtT0 = false;

    // =====================================================================
    // PUBLIC STATE / EVENTS
    // =====================================================================

    /// <summary>
    /// Total number of balls planned for this level (all phases combined).
    /// Filled during ConfigureFromLevel.
    /// </summary>
    public int PlannedSpawnCount { get; private set; }

    /// <summary>
    /// Index of the current phase during runtime spawning (0, 1, 2, ...).
    /// </summary>
    public int CurrentPhaseIndex { get; private set; } = 0;

    /// <summary>
    /// Event raised when the current phase changes: (index, name).
    /// </summary>
    public event Action<int, string> OnPhaseChanged;

    /// <summary>
    /// Event raised once, when the total planned spawn count is known.
    /// </summary>
    public event Action<int> OnPlannedReady;

    /// <summary>
    /// Event raised every time a ball is activated/spawned.
    /// Parameter: total number of activated balls so far.
    /// </summary>
    public event Action<int> OnActivated;

    // =====================================================================
    // INTERNAL DATA
    // =====================================================================

    private LevelData data;

    // Maps BallType to score points (from LevelData.Balls).
    private readonly Dictionary<BallType, int> pointsByType = new Dictionary<BallType, int>();

    /// <summary>
    /// Internal plan for a single phase.
    /// </summary>
    private struct PhasePlan
    {
        public int Index;
        public string Name;
        public float DurationSec;
        public float Interval;
        public int Quota;
    }

    // Runtime spawn plan by phase (internal representation).
    private readonly List<PhasePlan> plans = new List<PhasePlan>();

    /// <summary>
    /// Single entry of a mix: type + weight.
    /// </summary>
    private struct MixEntry
    {
        public BallType t;
        public float w;
    }

    // For each phase, list of MixEntry describing the type mix.
    private readonly List<List<MixEntry>> mixes = new List<List<MixEntry>>();

    // For each phase, sum of mix weights (used to normalize allocations).
    private readonly List<float> mixTotals = new List<float>();

    // For each phase, queue of BallType in the exact spawn order.
    private readonly List<Queue<BallType>> typeQueues = new List<Queue<BallType>>();

    // Pool of GameObjects used for the balls.
    private readonly Stack<GameObject> pool = new Stack<GameObject>();

    // Coroutines
    private Coroutine prewarmCoro;
    private Coroutine loop;
    private bool running;

    // Telemetry counters
    private int plannedTotal;
    private int prewarmedCount;
    private int activatedCount;
    private int recycledCollected;
    private int recycledLost;

    /// <summary>
    /// Public copy of the spawn phase plan, used by UI (IntroLevelUI, debug).
    /// Filled after BuildTypeQueues, when quotas are known.
    /// </summary>
    private PhasePlanInfo[] publicPhasePlans = Array.Empty<PhasePlanInfo>();

    // =====================================================================
    // CONFIGURATION
    // =====================================================================

    /// <summary>
    /// Configures the spawner from LevelData and the total run duration (seconds).
    /// This builds:
    /// - points per ball type,
    /// - a phase plan (duration, interval),
    /// - per-phase type mixes,
    /// - per-phase type queues with exact quotas.
    /// Also computes PlannedSpawnCount and resets telemetry/pool.
    /// </summary>
    public void ConfigureFromLevel(LevelData levelData, float totalRunSec)
    {
        data = levelData;

        // 1) Build points per type from LevelData.Balls
        BuildPointsByType();

        // 2) Build phase plan (duration and interval per phase)
        BuildPlansFromWeights(totalRunSec);

        // 3) Build mixes per phase (relative type weights)
        BuildMixes();

        // 4) Build final type queues and quotas per phase
        BuildTypeQueues();

        // 5) Reset runtime state / telemetry
        PlannedSpawnCount = plannedTotal;
        prewarmedCount = 0;
        activatedCount = 0;
        recycledCollected = 0;
        recycledLost = 0;
        pool.Clear();
        CurrentPhaseIndex = plans.Count > 0 ? 0 : -1;

        // Notify listeners that the planned total is now known
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

    /// <summary>
    /// Builds pointsByType from LevelData.Balls.
    /// </summary>
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

        // Fallback: if no mapping defined, ensure White exists with some value.
        if (pointsByType.Count == 0)
        {
            pointsByType[BallType.White] = 100;
        }
    }

    /// <summary>
    /// Builds the base plan for each phase using weights and total run duration.
    /// Each phase gets:
    /// - a duration (based on its weight),
    /// - an interval (phase.SpawnInterval or Level Spawn.Intervalle or default),
    /// - quota is left at 0 here and filled later.
    /// </summary>
    private void BuildPlansFromWeights(float totalRunSec)
    {
        plans.Clear();

        if (data?.Phases == null || totalRunSec <= 0f)
            return;

        // Sum of positive weights
        float sumW = 0f;
        foreach (var ph in data.Phases)
        {
            sumW += Mathf.Max(0f, ph.Weight);
        }

        if (sumW <= 0f)
            return;

        float accumulated = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            var ph = data.Phases[i];

            // Base duration proportional to weight
            float dur = (ph.Weight / sumW) * totalRunSec;

            // Last phase gets the remaining time to avoid rounding drift
            if (i == data.Phases.Length - 1)
            {
                dur = Mathf.Max(0f, totalRunSec - accumulated);
            }

            // Interval: phase override > level spawn > default
            float iv = ph.Intervalle > 0f
                ? ph.Intervalle
                : (data.Spawn != null && data.Spawn.Intervalle > 0f
                    ? data.Spawn.Intervalle
                    : intervalDefault);

            plans.Add(
                new PhasePlan
                {
                    Index = i,
                    Name = string.IsNullOrWhiteSpace(ph.Name) ? $"Phase {i + 1}" : ph.Name,
                    DurationSec = Mathf.Max(0f, dur),
                    Interval = Mathf.Max(0.0001f, iv),
                    Quota = 0 // filled later in BuildTypeQueues
                }
            );

            accumulated += dur;
        }
    }

    /// <summary>
    /// Builds the type mixes for each phase, based on PhaseData.Mix.
    /// If a phase has no mix, we create a uniform mix over all known types.
    /// </summary>
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

            // If a mix is defined in JSON, parse it.
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

            // Fallback: if no valid mix, uniform mix over all known types
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

    /// <summary>
    /// Builds:
    /// - the quota (number of balls) for each phase,
    /// - the global plannedTotal,
    /// - the per-phase type queues,
    /// - and the publicPhasePlans snapshot for the UI.
    /// </summary>
    private void BuildTypeQueues()
    {
        typeQueues.Clear();
        plannedTotal = 0;

        // 1) Compute quota for each phase based on its duration and interval.
        for (int i = 0; i < plans.Count; i++)
        {
            var p = plans[i];

            int quota = Mathf.Max(0, Mathf.FloorToInt(p.DurationSec / p.Interval));
            plannedTotal += quota;

            // Update internal plan entry with computed quota
            plans[i] = new PhasePlan
            {
                Index = p.Index,
                Name = p.Name,
                DurationSec = p.DurationSec,
                Interval = p.Interval,
                Quota = quota
            };
        }

        // spawnAtT0 adds one extra spawn at time 0 if enabled
        if (spawnAtT0)
        {
            plannedTotal += 1;
        }

        // 2) Build per-phase type queues according to mixes and quotas.
        for (int i = 0; i < plans.Count; i++)
        {
            int count = plans[i].Quota;
            var mix = mixes[i];
            float totalW = mixTotals[i];

            var queue = new Queue<BallType>(count);

            // If no valid mix or no quota, fallback to default type.
            if (count <= 0 || totalW <= 0f || mix.Count == 0)
            {
                for (int k = 0; k < count; k++)
                    queue.Enqueue(DefaultType());

                typeQueues.Add(queue);
                continue;
            }

            int n = mix.Count;
            int[] alloc = new int[n];
            float[] residuals = new float[n];
            int sum = 0;

            // First pass: base integer allocation (floor of ideal counts).
            for (int k = 0; k < n; k++)
            {
                float target = (mix[k].w / totalW) * count;
                int baseInt = Mathf.FloorToInt(target);
                alloc[k] = baseInt;
                residuals[k] = target - baseInt;
                sum += baseInt;
            }

            // Distribute remaining slots based on largest residuals.
            int remain = count - sum;
            if (remain > 0)
            {
                var idx = new List<int>(n);
                for (int k = 0; k < n; k++)
                    idx.Add(k);

                idx.Sort((a, b) => residuals[b].CompareTo(residuals[a]));

                for (int r = 0; r < remain; r++)
                {
                    int slot = idx[r % n];
                    alloc[slot]++;
                }
            }

            // Second pass: construct the queue by interleaving types.
            int[] left = (int[])alloc.Clone();
            int leftTotal = count;
            int cursor = 0;

            while (leftTotal > 0)
            {
                int tries = 0;

                // Find the next type that still has remaining count.
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

            // Safety: if anything went wrong, pad with default type.
            while (queue.Count < count)
            {
                queue.Enqueue(DefaultType());
            }

            typeQueues.Add(queue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string breakdown = "";
            for (int k = 0; k < n; k++)
            {
                breakdown += $"{mix[k].t}:{alloc[k]} ";
            }
            Debug.Log($"[Spawner/Types] Phase {plans[i].Index} \"{plans[i].Name}\" -> {count} | {breakdown}");
#endif
        }

        // 3) Build the public copy of phase plans for UI (Intro level, debug).
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

    /// <summary>
    /// Returns the default ball type when no mix is defined.
    /// Priority to White if present.
    /// </summary>
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

    /// <summary>
    /// Starts prewarming the pool by instantiating the planned number of balls.
    /// The work is spread across multiple frames according to budgetPerFrame.
    /// </summary>
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

    // =====================================================================
    // RUNTIME SPAWNING
    // =====================================================================

    /// <summary>
    /// Starts the spawn loop according to the current plan.
    /// </summary>
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

    /// <summary>
    /// Stops the spawn loop. Final stats are logged via LogStats() from outside.
    /// </summary>
    public void StopSpawning()
    {
        running = false;

        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    /// <summary>
    /// Main coroutine that iterates over phases and spawns balls over time.
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        // Start at phase 0
        CurrentPhaseIndex = 0;
        OnPhaseChanged?.Invoke(CurrentPhaseIndex, plans[CurrentPhaseIndex].Name);

        // Optional immediate spawn at t0
        if (spawnAtT0 && running)
        {
            ActivateOne(CurrentPhaseIndex);
        }

        // Iterate over each phase
        foreach (var p in plans)
        {
            if (!running)
                break;

            CurrentPhaseIndex = p.Index;
            OnPhaseChanged?.Invoke(CurrentPhaseIndex, p.Name);

            float tick = 0f;
            float elapsed = 0f;

            // Spawn until the phase duration is reached
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

        // End of spawn loop
        loop = null;
    }

    /// <summary>
    /// Activates a single ball for the given phase index (position, type, score, state).
    /// </summary>
    private void ActivateOne(int phaseIdx)
    {
        GameObject go = (pool.Count > 0) ? pool.Pop() : Instantiate(ballPrefab);

        // Choose random X position in range, Y and Z fixed
        float x = UnityEngine.Random.Range(-xRange, xRange);
        go.transform.position = new Vector3(x, ySpawn, zSpawn);

        // Ensure physics are active
        if (go.TryGetComponent(out Collider col))
            col.enabled = true;

        if (go.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
        }

        // Determine ball type from the phase queue
        BallType type = NextTypeForPhase(phaseIdx);
        int pts = pointsByType.TryGetValue(type, out int p) ? p : 0;

        // Initialize BallState
        if (go.TryGetComponent(out BallState st))
        {
            st.inBin = false;
            st.collected = false;
            st.currentSide = Side.None;

            st.Initialize(type, pts);
        }

        go.SetActive(true);

        // Telemetry and notifications
        activatedCount++;
        OnActivated?.Invoke(activatedCount);
        scoreManager?.RegisterRealSpawn();
    }

    /// <summary>
    /// Dequeues the next ball type for the given phase index.
    /// Falls back to DefaultType if anything is wrong.
    /// </summary>
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

    /// <summary>
    /// Recycles a ball GameObject back into the pool.
    /// Resets physics and increments recycle counters.
    /// </summary>
    public void Recycle(GameObject go, bool collected = false)
    {
        if (go == null)
            return;

        // Reset physics
        if (go.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (go.TryGetComponent(out Collider col))
        {
            col.enabled = false;
        }

        // Disable and push back to pool
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

    /// <summary>
    /// Logs final spawn stats (planned vs real / recycled).
    /// </summary>
    public void LogStats()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[SpawnStats] Planned={plannedTotal} | Prewarmed={prewarmedCount} | Activated={activatedCount} | " +
            $"Recycled=Collected:{recycledCollected} Lost:{recycledLost}"
        );
#endif
    }

    /// <summary>
    /// Returns a copy of the phase plan array (Index, Name, Duration, Interval, Quota).
    /// This is intended for UI usage (IntroLevelUI) and must not be modified externally.
    /// </summary>
    public PhasePlanInfo[] GetPhasePlans()
    {
        if (publicPhasePlans == null || publicPhasePlans.Length == 0)
            return Array.Empty<PhasePlanInfo>();

        PhasePlanInfo[] copy = new PhasePlanInfo[publicPhasePlans.Length];
        Array.Copy(publicPhasePlans, copy, publicPhasePlans.Length);
        return copy;
    }
}
