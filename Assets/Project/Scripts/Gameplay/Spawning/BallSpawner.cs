using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Infos publiques du plan de phase, exposees pour les UI (IntroLevelUI, debug, etc.).
/// </summary>
[Serializable]
public struct PhasePlanInfo
{
    public int Index;
    public string Name;
    public float DurationSec;
    public float IntervalSec;
    public int Quota;

    public int WhiteCount;
    public int BlueCount;
    public int RedCount;
    public int BlackCount;
}

/// <summary>
/// BallSpawner:
/// - Construit un plan par phase (duree via weights, intervalle via JSON).
/// - Construit des queues de types discretes (quota exact).
/// - Spawn runtime en mode quota-driven (exact, sans derive dt).
///
/// Important:
/// - Le tuto ne passe PAS par le pipeline de pool normal.
/// - Les billes de tuto sont instanciees a part puis detruites.
/// </summary>
public class BallSpawner : MonoBehaviour
{
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

    private readonly Dictionary<BallType, int> recycledCollectedByType = new Dictionary<BallType, int>();
    private readonly Dictionary<BallType, int> recycledLostByType = new Dictionary<BallType, int>();

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

    public void ConfigureFromLevel(LevelData levelData, float totalRunSec)
    {
        data = levelData;

        recycledCollectedByType.Clear();
        recycledLostByType.Clear();

        BuildPointsByType();
        BuildPlansFromWeights(totalRunSec);
        BuildMixes();
        BuildTypeQueues();

        PlannedSpawnCount = plannedTotal;

        prewarmedCount = 0;
        activatedCount = 0;
        recycledCollected = 0;
        recycledLost = 0;
        pool.Clear();

        CurrentPhaseIndex = plans.Count > 0 ? 0 : -1;

        OnPlannedReady?.Invoke(PlannedSpawnCount);
    }

    private void BuildPointsByType()
    {
        pointsByType.Clear();

        if (data != null && data.Balls != null)
        {
            for (int i = 0; i < data.Balls.Length; i++)
            {
                var b = data.Balls[i];
                if (b == null || string.IsNullOrWhiteSpace(b.Type))
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
        if (data != null && data.Phases != null && phaseIndex >= 0 && phaseIndex < data.Phases.Length)
        {
            float phaseIv = data.Phases[phaseIndex].Intervalle;
            if (phaseIv > 0f)
                return phaseIv;
        }

        return GetGlobalSpawnInterval();
    }

    private void BuildPlansFromWeights(float totalRunSec)
    {
        plans.Clear();

        if (data == null || data.Phases == null || totalRunSec <= 0f)
            return;

        float sumW = 0f;
        for (int i = 0; i < data.Phases.Length; i++)
            sumW += Mathf.Max(0f, data.Phases[i].Weight);

        if (sumW <= 0f)
            return;

        float accumulated = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            var ph = data.Phases[i];

            float dur = (ph.Weight / sumW) * totalRunSec;
            if (i == data.Phases.Length - 1)
                dur = Mathf.Max(0f, totalRunSec - accumulated);

            float iv = GetDesignIntervalForPhase(i);

            plans.Add(new PhasePlan
            {
                Index = i,
                Name = string.IsNullOrWhiteSpace(ph.Name) ? ("PHASE " + (i + 1)) : ph.Name,
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

        if (data == null || data.Phases == null)
            return;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            var ph = data.Phases[i];

            List<MixEntry> list = new List<MixEntry>();
            float total = 0f;

            if (ph != null && ph.Mix != null && ph.Mix.Length > 0)
            {
                for (int k = 0; k < ph.Mix.Length; k++)
                {
                    var m = ph.Mix[k];
                    if (m == null || string.IsNullOrWhiteSpace(m.Type))
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
        int count = Mathf.FloorToInt((durationSec - eps) / intervalSec);
        return Mathf.Max(0, count);
    }

    private void BuildTypeQueues()
    {
        typeQueues.Clear();
        plannedTotal = 0;

        PlannedNonBlackSpawnCount = 0;
        PlannedBlackSpawnCount = 0;

        int[] phaseWhiteCounts = new int[plans.Count];
        int[] phaseBlueCounts = new int[plans.Count];
        int[] phaseRedCounts = new int[plans.Count];
        int[] phaseBlackCounts = new int[plans.Count];

        for (int i = 0; i < plans.Count; i++)
        {
            PhasePlan p = plans[i];
            p.Interval = Mathf.Max(0.0001f, GetDesignIntervalForPhase(i));
            p.Quota = ComputePhaseQuota(p.DurationSec, p.Interval);
            plans[i] = p;
            plannedTotal += p.Quota;
        }

        for (int i = 0; i < plans.Count; i++)
        {
            int count = plans[i].Quota;
            List<MixEntry> mix = mixes[i];
            float totalW = mixTotals[i];

            Queue<BallType> queue = new Queue<BallType>(count);

            int whiteCount = 0;
            int blueCount = 0;
            int redCount = 0;
            int blackCount = 0;

            if (count <= 0)
            {
                typeQueues.Add(queue);

                phaseWhiteCounts[i] = 0;
                phaseBlueCounts[i] = 0;
                phaseRedCounts[i] = 0;
                phaseBlackCounts[i] = 0;
                continue;
            }

            List<ForcedInsertion> forced = BuildForcedInsertionsForPhase(i, count);
            int forcedTotal = forced.Count;

            if (forcedTotal > count)
            {
                forcedTotal = count;
                if (forced.Count > count)
                    forced.RemoveRange(count, forced.Count - count);
            }

            int remaining = Mathf.Max(0, count - forcedTotal);

            if (remaining <= 0 || totalW <= 0f || mix == null || mix.Count == 0)
            {
                List<BallType> temp = new List<BallType>(count);
                for (int k = 0; k < remaining; k++)
                    temp.Add(DefaultType());

                List<BallType> finalList = BuildFinalListWithForced(temp, forced, count);

                for (int k = 0; k < finalList.Count; k++)
                {
                    BallType t = finalList[k];

                    if (t == BallType.Black) PlannedBlackSpawnCount++;
                    else PlannedNonBlackSpawnCount++;

                    if (t == BallType.White) whiteCount++;
                    else if (t == BallType.Blue) blueCount++;
                    else if (t == BallType.Red) redCount++;
                    else if (t == BallType.Black) blackCount++;

                    queue.Enqueue(t);
                }

                typeQueues.Add(queue);

                phaseWhiteCounts[i] = whiteCount;
                phaseBlueCounts[i] = blueCount;
                phaseRedCounts[i] = redCount;
                phaseBlackCounts[i] = blackCount;

                continue;
            }

            int n = mix.Count;
            int[] alloc = new int[n];
            float[] residuals = new float[n];
            int sum = 0;

            for (int k = 0; k < n; k++)
            {
                float target = (mix[k].w / totalW) * remaining;
                int baseInt = Mathf.FloorToInt(target);
                alloc[k] = baseInt;
                residuals[k] = target - baseInt;
                sum += baseInt;
            }

            int remain2 = remaining - sum;
            if (remain2 > 0)
            {
                List<int> idx = new List<int>(n);
                for (int k = 0; k < n; k++)
                    idx.Add(k);

                idx.Sort((a, b) => residuals[b].CompareTo(residuals[a]));

                for (int r = 0; r < remain2; r++)
                    alloc[idx[r % n]]++;
            }

            List<BallType> baseList = new List<BallType>(remaining);

            int[] left = (int[])alloc.Clone();
            int leftTotal = remaining;
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

                baseList.Add(mix[cursor].t);
                left[cursor]--;
                leftTotal--;
                cursor = (cursor + 1) % n;
            }

            while (baseList.Count < remaining)
                baseList.Add(DefaultType());

            List<BallType> finalTypes = BuildFinalListWithForced(baseList, forced, count);

            for (int k = 0; k < finalTypes.Count; k++)
            {
                BallType t = finalTypes[k];

                if (t == BallType.Black) PlannedBlackSpawnCount++;
                else PlannedNonBlackSpawnCount++;

                if (t == BallType.White) whiteCount++;
                else if (t == BallType.Blue) blueCount++;
                else if (t == BallType.Red) redCount++;
                else if (t == BallType.Black) blackCount++;

                queue.Enqueue(t);
            }

            typeQueues.Add(queue);

            phaseWhiteCounts[i] = whiteCount;
            phaseBlueCounts[i] = blueCount;
            phaseRedCounts[i] = redCount;
            phaseBlackCounts[i] = blackCount;
        }

        publicPhasePlans = new PhasePlanInfo[plans.Count];
        for (int i = 0; i < plans.Count; i++)
        {
            PhasePlan p = plans[i];
            publicPhasePlans[i] = new PhasePlanInfo
            {
                Index = p.Index,
                Name = p.Name,
                DurationSec = p.DurationSec,
                IntervalSec = p.Interval,
                Quota = p.Quota,
                WhiteCount = phaseWhiteCounts[i],
                BlueCount = phaseBlueCounts[i],
                RedCount = phaseRedCounts[i],
                BlackCount = phaseBlackCounts[i]
            };
        }
    }

    private struct ForcedInsertion
    {
        public int index;
        public BallType type;
    }

    private List<ForcedInsertion> BuildForcedInsertionsForPhase(int phaseIndex, int count)
    {
        List<ForcedInsertion> list = new List<ForcedInsertion>();

        if (data == null || data.Phases == null || phaseIndex < 0 || phaseIndex >= data.Phases.Length)
            return list;

        var ph = data.Phases[phaseIndex];
        if (ph == null || ph.ForcedSpawns == null || ph.ForcedSpawns.Length == 0)
            return list;

        for (int i = 0; i < ph.ForcedSpawns.Length; i++)
        {
            var f = ph.ForcedSpawns[i];
            if (f == null) continue;
            if (string.IsNullOrWhiteSpace(f.Type)) continue;

            if (!Enum.TryParse(f.Type, true, out BallType t))
                continue;

            int c = Mathf.Max(0, f.Count);
            if (c == 0) continue;

            float at = f.AtPercent;
            int baseIndex;

            if (at >= 0f && at <= 1f)
                baseIndex = Mathf.Clamp(Mathf.RoundToInt(at * (count - 1)), 0, Mathf.Max(0, count - 1));
            else
                baseIndex = Mathf.Clamp(count / 2, 0, Mathf.Max(0, count - 1));

            for (int k = 0; k < c; k++)
            {
                int offset = (k == 0) ? 0 : (k % 2 == 1 ? (k + 1) / 2 : -k / 2);
                int idx = Mathf.Clamp(baseIndex + offset, 0, Mathf.Max(0, count - 1));
                list.Add(new ForcedInsertion { index = idx, type = t });
            }
        }

        list.Sort((a, b) => a.index.CompareTo(b.index));
        return list;
    }

    private List<BallType> BuildFinalListWithForced(List<BallType> baseList, List<ForcedInsertion> forced, int finalCount)
    {
        List<BallType> result = new List<BallType>(finalCount);

        int baseCursor = 0;
        int forcedCursor = 0;

        for (int outIndex = 0; outIndex < finalCount; outIndex++)
        {
            bool inserted = false;

            if (forced != null)
            {
                while (forcedCursor < forced.Count && forced[forcedCursor].index == outIndex)
                {
                    result.Add(forced[forcedCursor].type);
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
                result.Add(DefaultType());
            }
        }

        if (forced != null)
        {
            while (result.Count < finalCount && forcedCursor < forced.Count)
            {
                result.Add(forced[forcedCursor].type);
                forcedCursor++;
            }
        }

        if (result.Count > finalCount)
            result.RemoveRange(finalCount, result.Count - finalCount);

        return result;
    }

    private BallType DefaultType()
    {
        if (pointsByType.ContainsKey(BallType.White))
            return BallType.White;

        foreach (var kv in pointsByType)
            return kv.Key;

        return BallType.White;
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

            PhasePlan p = plans[phase];
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
            st.isTutorialBall = false;
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

    public void Recycle(GameObject go, BallType type, bool collected = false)
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
        {
            recycledCollected++;

            if (!recycledCollectedByType.ContainsKey(type))
                recycledCollectedByType[type] = 0;

            recycledCollectedByType[type]++;
        }
        else
        {
            recycledLost++;

            if (!recycledLostByType.ContainsKey(type))
                recycledLostByType[type] = 0;

            recycledLostByType[type]++;
        }
    }

    public void Recycle(GameObject go, bool collected = false)
    {
        BallType t = DefaultType();
        if (go != null && go.TryGetComponent(out BallState st))
            t = st.type;

        Recycle(go, t, collected);
    }

    /// <summary>
    /// Spawn une bille de tutoriel totalement isolee du pool gameplay.
    ///
    /// releasePhysics :
    /// - true  = physique active
    /// - false = bille figée
    ///
    /// applyInitialVelocity :
    /// - true  = applique la velocity fournie (bille jouable)
    /// - false = aucune impulsion initiale, la bille tombe juste avec la gravité
    /// </summary>
    public GameObject SpawnTutorialBall(
        Vector3 position,
        Vector3 velocity,
        BallType type,
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

        Rigidbody rb = null;
        if (go.TryGetComponent(out rb))
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

        int pts = pointsByType.TryGetValue(type, out int p) ? p : 0;

        if (go.TryGetComponent(out BallState st))
        {
            st.inBin = false;
            st.collected = false;
            st.currentSide = Side.None;
            st.isTutorialBall = true;
            st.Initialize(type, pts);
        }

        if (releasePhysics && tutorialReleasePhysicsNextFrame)
            StartCoroutine(ReleaseTutorialBallNextFrame(go, velocity, applyInitialVelocity));

        if (logTutorialSpawn)
            StartCoroutine(LogTutorialSpawnNextFrame(go, type));

        return go;
    }

    private IEnumerator ReleaseTutorialBallNextFrame(GameObject go, Vector3 velocity, bool applyInitialVelocity)
    {
        yield return null;

        if (go == null)
            yield break;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (applyInitialVelocity)
                rb.linearVelocity = velocity;
        }

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            col.enabled = true;

        BallCeilingGrace grace = go.GetComponent<BallCeilingGrace>();
        if (grace != null)
            grace.StartGrace();
    }

    private IEnumerator LogTutorialSpawnNextFrame(GameObject go, BallType type)
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

    public void LogStats()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string collectedStr = "";
        foreach (BallType t in Enum.GetValues(typeof(BallType)))
        {
            if (recycledCollectedByType.TryGetValue(t, out int c) && c > 0)
                collectedStr += t + ":" + c + " ";
        }

        string lostStr = "";
        foreach (BallType t in Enum.GetValues(typeof(BallType)))
        {
            if (recycledLostByType.TryGetValue(t, out int c) && c > 0)
                lostStr += t + ":" + c + " ";
        }

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