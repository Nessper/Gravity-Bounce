using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BallPlanCount
{
    public string BallId;
    public int Count;
}

[Serializable]
public struct PhasePlanInfo
{
    public int Index;
    public string Name;
    public float DurationSec;
    public float IntervalSec;
    public int Quota;
    public BallPlanCount[] Counts;
}

public class BallSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform ballsParent;

    [Header("Ball Definitions")]
    [SerializeField] private BallDefinitionCatalog ballCatalog;

    [Header("Camera / Ceiling")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Collider ceilingCollider;
    [SerializeField] private float spawnOffsetAboveScreen = 0.3f;

    [Header("Spawn Area & Cadence")]
    [SerializeField] private float xRange = 2.18f;
    [SerializeField] private float ySpawn = 6.3f;
    [SerializeField] private float zSpawn = -0.2f;
    [SerializeField] private float intervalDefault = 0.6f;

    [Header("Debug Tutorial Spawn")]
    [SerializeField] private bool logTutorialSpawn = false;
    [SerializeField] private bool tutorialReleasePhysicsNextFrame = true;

    public int PlannedSpawnCount { get; private set; }
    public int PlannedNonBlackSpawnCount { get; private set; }
    public int PlannedBlackSpawnCount { get; private set; }
    public int CurrentPhaseIndex { get; private set; } = 0;

    public event Action<int, string> OnPhaseChanged;
    public event Action<int> OnPlannedReady;
    public event Action<int> OnActivated;

    private readonly Dictionary<string, int> recycledCollectedByBallId = new Dictionary<string, int>();
    private readonly Dictionary<string, int> recycledLostByBallId = new Dictionary<string, int>();

    private LevelData data;

    private struct PhasePlan
    {
        public int Index;
        public string Name;
        public float DurationSec;
        public float Interval;
        public int Quota;
        public float Weight;
    }

    private struct MixEntry
    {
        public BallDefinition definition;
        public float weight;
    }

    private struct ForcedInsertion
    {
        public int index;
        public BallDefinition definition;
    }

    private readonly List<PhasePlan> plans = new List<PhasePlan>();
    private readonly List<List<MixEntry>> mixes = new List<List<MixEntry>>();
    private readonly List<float> mixTotals = new List<float>();
    private readonly List<Queue<BallDefinition>> definitionQueues = new List<Queue<BallDefinition>>();
    private readonly Stack<GameObject> pool = new Stack<GameObject>();
    private readonly List<BallState> activeBalls = new List<BallState>();

    private Coroutine prewarmCoro;
    private Coroutine loop;
    private bool running;

    private int plannedTotal;
    private int prewarmedCount;
    private int activatedCount;
    private int recycledCollected;
    private int recycledLost;

    private PhasePlanInfo[] publicPhasePlans = Array.Empty<PhasePlanInfo>();

    public void ConfigureFromLevel(LevelData levelData, float totalRunSec)
    {
        data = levelData;

        if (ballCatalog != null)
            ballCatalog.Initialize();

        recycledCollectedByBallId.Clear();
        recycledLostByBallId.Clear();

        BuildPlansFromWeights(totalRunSec);
        BuildMixes();
        BuildDefinitionQueues();

        PlannedSpawnCount = plannedTotal;

        prewarmedCount = 0;
        activatedCount = 0;
        recycledCollected = 0;
        recycledLost = 0;

        pool.Clear();
        activeBalls.Clear();

        CurrentPhaseIndex = plans.Count > 0 ? 0 : -1;

        OnPlannedReady?.Invoke(PlannedSpawnCount);
    }

    private float GetGlobalSpawnInterval()
    {
        if (data != null && data.Spawn != null && data.Spawn.Intervalle > 0f)
            return data.Spawn.Intervalle;

        return intervalDefault;
    }

    private float GetDesignIntervalForPhase(int phaseIndex)
    {
        if (data != null && data.Phases != null && phaseIndex >= 0 && phaseIndex < data.Phases.Length)
        {
            float phaseInterval = data.Phases[phaseIndex].Intervalle;
            if (phaseInterval > 0f)
                return phaseInterval;
        }

        return GetGlobalSpawnInterval();
    }

    private void BuildPlansFromWeights(float totalRunSec)
    {
        plans.Clear();

        if (data == null || data.Phases == null || totalRunSec <= 0f)
            return;

        float sumWeight = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
            sumWeight += Mathf.Max(0f, data.Phases[i].Weight);

        if (sumWeight <= 0f)
            return;

        float accumulated = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            PhaseData phase = data.Phases[i];

            float duration = (phase.Weight / sumWeight) * totalRunSec;
            if (i == data.Phases.Length - 1)
                duration = Mathf.Max(0f, totalRunSec - accumulated);

            float interval = GetDesignIntervalForPhase(i);

            plans.Add(new PhasePlan
            {
                Index = i,
                Name = string.IsNullOrWhiteSpace(phase.Name) ? "PHASE " + (i + 1) : phase.Name,
                DurationSec = Mathf.Max(0f, duration),
                Interval = Mathf.Max(0.0001f, interval),
                Quota = 0,
                Weight = Mathf.Max(0f, phase.Weight)
            });

            accumulated += duration;
        }
    }

    private void BuildMixes()
    {
        mixes.Clear();
        mixTotals.Clear();

        if (data == null || data.Phases == null)
            return;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            PhaseData phase = data.Phases[i];

            List<MixEntry> list = new List<MixEntry>();
            float total = 0f;

            if (phase != null && phase.Mix != null)
            {
                for (int k = 0; k < phase.Mix.Length; k++)
                {
                    PhaseMixEntry mix = phase.Mix[k];

                    if (mix == null || string.IsNullOrWhiteSpace(mix.BallId))
                        continue;

                    BallDefinition definition = GetDefinition(mix.BallId);
                    if (definition == null)
                        continue;

                    float weight = Mathf.Max(0f, mix.Poids);
                    if (weight <= 0f)
                        continue;

                    list.Add(new MixEntry
                    {
                        definition = definition,
                        weight = weight
                    });

                    total += weight;
                }
            }

            if (list.Count == 0)
            {
                BallDefinition fallback = DefaultDefinition();
                if (fallback != null)
                {
                    list.Add(new MixEntry
                    {
                        definition = fallback,
                        weight = 1f
                    });

                    total = 1f;
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

        return Mathf.Max(0, Mathf.FloorToInt((durationSec - 0.0001f) / intervalSec));
    }

    private void BuildDefinitionQueues()
    {
        definitionQueues.Clear();

        plannedTotal = 0;
        PlannedNonBlackSpawnCount = 0;
        PlannedBlackSpawnCount = 0;

        Dictionary<string, int>[] phaseCounts = new Dictionary<string, int>[plans.Count];

        for (int i = 0; i < phaseCounts.Length; i++)
            phaseCounts[i] = new Dictionary<string, int>();

        for (int i = 0; i < plans.Count; i++)
        {
            PhasePlan plan = plans[i];
            plan.Interval = Mathf.Max(0.0001f, GetDesignIntervalForPhase(i));
            plan.Quota = ComputePhaseQuota(plan.DurationSec, plan.Interval);
            plans[i] = plan;

            plannedTotal += plan.Quota;
        }

        for (int i = 0; i < plans.Count; i++)
        {
            int count = plans[i].Quota;
            List<MixEntry> mix = mixes[i];
            float totalWeight = mixTotals[i];

            Queue<BallDefinition> queue = new Queue<BallDefinition>(count);

            if (count <= 0)
            {
                definitionQueues.Add(queue);
                continue;
            }

            List<ForcedInsertion> forced = BuildForcedInsertionsForPhase(i, count);

            if (forced.Count > count)
                forced.RemoveRange(count, forced.Count - count);

            int remaining = Mathf.Max(0, count - forced.Count);

            List<BallDefinition> baseList = BuildBaseListFromMix(mix, totalWeight, remaining);
            List<BallDefinition> finalDefinitions = BuildFinalListWithForced(baseList, forced, count);

            for (int k = 0; k < finalDefinitions.Count; k++)
            {
                BallDefinition definition = finalDefinitions[k];

                RegisterPlannedDefinition(definition);
                AddPhaseCount(phaseCounts[i], definition);

                queue.Enqueue(definition);
            }

            definitionQueues.Add(queue);
        }

        publicPhasePlans = new PhasePlanInfo[plans.Count];

        for (int i = 0; i < plans.Count; i++)
        {
            PhasePlan plan = plans[i];

            publicPhasePlans[i] = new PhasePlanInfo
            {
                Index = plan.Index,
                Name = plan.Name,
                DurationSec = plan.DurationSec,
                IntervalSec = plan.Interval,
                Quota = plan.Quota,
                Counts = BuildBallPlanCounts(phaseCounts[i])
            };
        }
    }

    private List<BallDefinition> BuildBaseListFromMix(List<MixEntry> mix, float totalWeight, int count)
    {
        List<BallDefinition> result = new List<BallDefinition>(count);

        if (count <= 0)
            return result;

        if (mix == null || mix.Count == 0 || totalWeight <= 0f)
        {
            BallDefinition fallback = DefaultDefinition();

            for (int i = 0; i < count; i++)
                result.Add(fallback);

            return result;
        }

        int n = mix.Count;
        int[] alloc = new int[n];
        float[] residuals = new float[n];

        int sum = 0;

        for (int i = 0; i < n; i++)
        {
            float target = (mix[i].weight / totalWeight) * count;
            int baseInt = Mathf.FloorToInt(target);

            alloc[i] = baseInt;
            residuals[i] = target - baseInt;
            sum += baseInt;
        }

        int remaining = count - sum;

        if (remaining > 0)
        {
            List<int> indexes = new List<int>(n);

            for (int i = 0; i < n; i++)
                indexes.Add(i);

            indexes.Sort((a, b) => residuals[b].CompareTo(residuals[a]));

            for (int r = 0; r < remaining; r++)
                alloc[indexes[r % n]]++;
        }

        int cursor = 0;
        int leftTotal = count;

        while (leftTotal > 0)
        {
            int tries = 0;

            while (tries < n && alloc[cursor] == 0)
            {
                cursor = (cursor + 1) % n;
                tries++;
            }

            if (tries >= n)
                break;

            result.Add(mix[cursor].definition);
            alloc[cursor]--;
            leftTotal--;

            cursor = (cursor + 1) % n;
        }

        BallDefinition fallbackDefinition = DefaultDefinition();

        while (result.Count < count)
            result.Add(fallbackDefinition);

        return result;
    }

    private List<ForcedInsertion> BuildForcedInsertionsForPhase(int phaseIndex, int count)
    {
        List<ForcedInsertion> list = new List<ForcedInsertion>();

        if (data == null || data.Phases == null || phaseIndex < 0 || phaseIndex >= data.Phases.Length)
            return list;

        PhaseData phase = data.Phases[phaseIndex];

        if (phase == null || phase.ForcedSpawns == null)
            return list;

        for (int i = 0; i < phase.ForcedSpawns.Length; i++)
        {
            ForcedSpawnEntry forced = phase.ForcedSpawns[i];

            if (forced == null || string.IsNullOrWhiteSpace(forced.BallId))
                continue;

            BallDefinition definition = GetDefinition(forced.BallId);
            if (definition == null)
                continue;

            int forcedCount = Mathf.Max(0, forced.Count);
            if (forcedCount == 0)
                continue;

            int baseIndex;

            if (forced.AtPercent >= 0f && forced.AtPercent <= 1f)
                baseIndex = Mathf.Clamp(Mathf.RoundToInt(forced.AtPercent * (count - 1)), 0, Mathf.Max(0, count - 1));
            else
                baseIndex = Mathf.Clamp(count / 2, 0, Mathf.Max(0, count - 1));

            for (int k = 0; k < forcedCount; k++)
            {
                int offset = k == 0 ? 0 : (k % 2 == 1 ? (k + 1) / 2 : -k / 2);
                int index = Mathf.Clamp(baseIndex + offset, 0, Mathf.Max(0, count - 1));

                list.Add(new ForcedInsertion
                {
                    index = index,
                    definition = definition
                });
            }
        }

        list.Sort((a, b) => a.index.CompareTo(b.index));
        return list;
    }

    private List<BallDefinition> BuildFinalListWithForced(
        List<BallDefinition> baseList,
        List<ForcedInsertion> forced,
        int finalCount)
    {
        List<BallDefinition> result = new List<BallDefinition>(finalCount);

        int baseCursor = 0;
        int forcedCursor = 0;

        for (int outIndex = 0; outIndex < finalCount; outIndex++)
        {
            bool inserted = false;

            if (forced != null)
            {
                while (forcedCursor < forced.Count && forced[forcedCursor].index == outIndex)
                {
                    result.Add(forced[forcedCursor].definition);
                    forcedCursor++;
                    inserted = true;

                    if (result.Count >= finalCount)
                        return result;
                }
            }

            if (inserted)
                continue;

            if (baseList != null && baseCursor < baseList.Count)
            {
                result.Add(baseList[baseCursor]);
                baseCursor++;
            }
            else
            {
                result.Add(DefaultDefinition());
            }
        }

        if (forced != null)
        {
            while (result.Count < finalCount && forcedCursor < forced.Count)
            {
                result.Add(forced[forcedCursor].definition);
                forcedCursor++;
            }
        }

        if (result.Count > finalCount)
            result.RemoveRange(finalCount, result.Count - finalCount);

        return result;
    }

    private void RegisterPlannedDefinition(BallDefinition definition)
    {
        if (definition == null)
            return;

        if (definition.IsDanger)
            PlannedBlackSpawnCount++;

        if (definition.CountsForProgress)
            PlannedNonBlackSpawnCount++;
    }

    private void AddPhaseCount(Dictionary<string, int> counts, BallDefinition definition)
    {
        if (counts == null || definition == null || string.IsNullOrWhiteSpace(definition.Id))
            return;

        if (!counts.ContainsKey(definition.Id))
            counts[definition.Id] = 0;

        counts[definition.Id]++;
    }

    private BallPlanCount[] BuildBallPlanCounts(Dictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
            return Array.Empty<BallPlanCount>();

        BallPlanCount[] result = new BallPlanCount[counts.Count];

        int index = 0;

        foreach (KeyValuePair<string, int> kv in counts)
        {
            result[index] = new BallPlanCount
            {
                BallId = kv.Key,
                Count = kv.Value
            };

            index++;
        }

        return result;
    }

    private BallDefinition NextDefinitionForPhase(int phaseIdx)
    {
        if (phaseIdx < 0 || phaseIdx >= definitionQueues.Count)
            return DefaultDefinition();

        Queue<BallDefinition> queue = definitionQueues[phaseIdx];

        if (queue == null || queue.Count == 0)
            return DefaultDefinition();

        return queue.Dequeue();
    }

    private BallDefinition DefaultDefinition()
    {
        if (ballCatalog != null && ballCatalog.DefaultBall != null)
            return ballCatalog.DefaultBall;

        return null;
    }

    private BallDefinition GetDefinition(string ballId)
    {
        if (ballCatalog == null)
            return null;

        if (string.IsNullOrWhiteSpace(ballId))
            return null;

        ballCatalog.TryGet(ballId, out BallDefinition definition);
        return definition;
    }

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
        WaitForEndOfFrame wait = new WaitForEndOfFrame();

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
            yield return wait;
        }

        prewarmCoro = null;
    }

    public void StartSpawning()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[BallSpawner] ballPrefab manquant.");
            return;
        }

        if (plans.Count == 0)
        {
            Debug.LogWarning("[BallSpawner] Aucun plan de phases. As-tu appele ConfigureFromLevel?");
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
        for (int phase = 0; phase < plans.Count; phase++)
        {
            if (!running)
                break;

            PhasePlan plan = plans[phase];
            CurrentPhaseIndex = plan.Index;

            OnPhaseChanged?.Invoke(CurrentPhaseIndex, plan.Name);

            int toSpawn = Mathf.Max(0, plan.Quota);

            for (int s = 0; s < toSpawn && running; s++)
            {
                yield return WaitSecondsAccurate(plan.Interval);

                if (!running)
                    break;

                ActivateOne(CurrentPhaseIndex);
            }
        }

        loop = null;
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
        GameObject go = pool.Count > 0 ? pool.Pop() : Instantiate(ballPrefab);

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

        BallDefinition definition = NextDefinitionForPhase(phaseIdx);

        if (go.TryGetComponent(out BallState st))
        {
            st.inBin = false;
            st.collected = false;
            st.currentSide = Side.None;
            st.isTutorialBall = false;

            st.Initialize(definition);
            activeBalls.Add(st);
        }

        activatedCount++;
        OnActivated?.Invoke(activatedCount);
        scoreManager?.RegisterRealSpawn();
    }

    public void Recycle(GameObject go, bool collected = false)
    {
        Recycle(
            go,
            collected
                ? BallRecycleReason.Collected
                : BallRecycleReason.Lost
        );
    }

    public bool Recycle(GameObject go, BallRecycleReason reason)
    {
        if (go == null || !go.activeSelf)
            return false;

        string ballId = "unknown";

        if (go.TryGetComponent(out BallState st))
        {
            ballId = st.BallId;
            activeBalls.Remove(st);
        }

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

        if (reason == BallRecycleReason.Neutralized)
            return true;

        bool collected = reason == BallRecycleReason.Collected;

        Dictionary<string, int> target = collected
            ? recycledCollectedByBallId
            : recycledLostByBallId;

        if (!target.ContainsKey(ballId))
            target[ballId] = 0;

        target[ballId]++;

        if (collected)
            recycledCollected++;
        else
            recycledLost++;

        return true;
    }

    public bool TryGetNearestActiveDangerBall(
        string ballId,
        Vector3 origin,
        out BallState nearest)
    {
        nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;
        bool nearestIsInBin = false;

        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            BallState candidate = activeBalls[i];

            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                activeBalls.RemoveAt(i);
                continue;
            }

            if (candidate.collected || !candidate.IsVisualDanger)
                continue;

            if (!string.Equals(
                    candidate.BallId,
                    ballId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            float sqrDistance =
                (candidate.transform.position - origin).sqrMagnitude;

            bool candidateIsInBin = candidate.inBin;
            bool hasHigherPriority =
                candidateIsInBin && !nearestIsInBin;
            bool hasSamePriority =
                candidateIsInBin == nearestIsInBin;

            if (!hasHigherPriority &&
                (!hasSamePriority || sqrDistance >= nearestSqrDistance))
            {
                continue;
            }

            nearest = candidate;
            nearestSqrDistance = sqrDistance;
            nearestIsInBin = candidateIsInBin;
        }

        return nearest != null;
    }

    public GameObject SpawnTutorialBall(
        Vector3 position,
        Vector3 velocity,
        BallDefinition definition,
        bool releasePhysics = true,
        bool applyInitialVelocity = true)
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[BallSpawner] ballPrefab manquant.");
            return null;
        }

        GameObject go = Instantiate(ballPrefab);

        if (ballsParent != null)
            go.transform.SetParent(ballsParent, true);

        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;
        go.SetActive(true);

        if (go.TryGetComponent(out Collider col))
            col.enabled = releasePhysics;

        if (go.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (!releasePhysics)
            {
                rb.isKinematic = true;
            }
            else if (tutorialReleasePhysicsNextFrame)
            {
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;

                if (applyInitialVelocity)
                    rb.linearVelocity = velocity;
            }
        }

        if (go.TryGetComponent(out BallCeilingGrace grace))
        {
            grace.SetCeiling(ceilingCollider);

            if (releasePhysics && !tutorialReleasePhysicsNextFrame)
                grace.StartGrace();
        }

        if (go.TryGetComponent(out BallState st))
        {
            st.inBin = false;
            st.collected = false;
            st.currentSide = Side.None;
            st.isTutorialBall = true;

            st.Initialize(definition);
        }

        if (releasePhysics && tutorialReleasePhysicsNextFrame)
            StartCoroutine(ReleaseTutorialBallNextFrame(go, velocity, applyInitialVelocity));

        if (logTutorialSpawn)
            StartCoroutine(LogTutorialSpawnNextFrame(go, definition));

        return go;
    }

    private IEnumerator ReleaseTutorialBallNextFrame(GameObject go, Vector3 velocity, bool applyInitialVelocity)
    {
        yield return null;

        if (go == null)
            yield break;

        if (go.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (applyInitialVelocity)
                rb.linearVelocity = velocity;
        }

        if (go.TryGetComponent(out Collider col))
            col.enabled = true;

        if (go.TryGetComponent(out BallCeilingGrace grace))
            grace.StartGrace();
    }

    private IEnumerator LogTutorialSpawnNextFrame(GameObject go, BallDefinition definition)
    {
        yield return null;
    }

    public void DestroyTutorialBall(GameObject go)
    {
        if (go == null)
            return;

        Destroy(go);
    }

    public PhasePlanInfo[] GetPhasePlans()
    {
        if (publicPhasePlans == null || publicPhasePlans.Length == 0)
            return Array.Empty<PhasePlanInfo>();

        PhasePlanInfo[] copy = new PhasePlanInfo[publicPhasePlans.Length];
        Array.Copy(publicPhasePlans, copy, publicPhasePlans.Length);

        return copy;
    }

    private string BuildStatsString(Dictionary<string, int> stats)
    {
        if (stats == null || stats.Count == 0)
            return "";

        string result = "";

        foreach (KeyValuePair<string, int> kv in stats)
        {
            if (kv.Value > 0)
                result += kv.Key + ":" + kv.Value + " ";
        }

        return result;
    }

    public void LogStats()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string collectedStr = BuildStatsString(recycledCollectedByBallId);
        string lostStr = BuildStatsString(recycledLostByBallId);

        Debug.Log(
            "[SpawnStats] Planned=" + plannedTotal +
            " | Prewarmed=" + prewarmedCount +
            " | Activated=" + activatedCount +
            " | Recycled=Collected:" + recycledCollected +
            " Lost:" + recycledLost +
            "\n  PlannedNonBlack=" + PlannedNonBlackSpawnCount +
            " PlannedBlack=" + PlannedBlackSpawnCount +
            "\n  CollectedByType: " + collectedStr +
            "\n  LostByType: " + lostStr
        );
#endif
    }
}
