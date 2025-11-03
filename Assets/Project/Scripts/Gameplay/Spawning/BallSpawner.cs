using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Zone & cadence")]
    [SerializeField] private float xRange = 4.6f;
    [SerializeField] private float ySpawn = 17.8f;
    [SerializeField] private float zSpawn = -0.2f;
    [SerializeField] private float interval = 0.6f;

    [Header("Politique de ticks")]
    [SerializeField] private bool spawnAtT0 = false;
    [SerializeField] private bool includeEndTick = false;

    [Header("UI Phase Display")]
    [SerializeField] private GameObject messageOverlay;
    [SerializeField] private TMPro.TMP_Text messageTxt;
    [SerializeField] private float messageDuration = 1.0f;

    private Coroutine phaseMsgRoutine;


    public int PlannedSpawnCount { get; private set; }

    private Coroutine loop;
    private bool running;

    private readonly Dictionary<BallType, int> pointsByType = new();
    private LevelData levelData;
    private bool usePhases;
    private int currentPhaseIndex;
    private float currentPhaseElapsed;

    private struct PhasePick { public BallType type; public float w; }
    private readonly List<PhasePick> currentMix = new();
    private float currentMixTotal;

    public void ConfigureFromLevel(LevelData data)
    {
        levelData = data;
        pointsByType.Clear();
        usePhases = (data != null && data.Phases != null && data.Phases.Length > 0);
        currentPhaseIndex = 0;
        currentPhaseElapsed = 0f;

        if (data == null)
        {
            Debug.LogWarning("[BallSpawner] ConfigureFromLevel: LevelData NULL");
            return;
        }

        if (data.Billes != null)
        {
            foreach (var b in data.Billes)
            {
                if (string.IsNullOrWhiteSpace(b.Type)) continue;
                if (!System.Enum.TryParse<BallType>(b.Type, true, out var t)) continue;
                pointsByType[t] = b.Points;
            }
        }
        if (pointsByType.Count == 0)
            pointsByType[BallType.White] = 100;

        if (data.Spawn != null && data.Spawn.Intervalle > 0f)
            interval = data.Spawn.Intervalle;

        if (usePhases)
            BuildCurrentMix(data.Phases[0]);

        RecalculatePlan(data.LevelDurationSec, interval);
    }

    private void BuildCurrentMix(PhaseData ph)
    {
        if (messageOverlay != null && messageTxt != null)
        {
            if (phaseMsgRoutine != null) StopCoroutine(phaseMsgRoutine);
            phaseMsgRoutine = StartCoroutine(ShowPhaseMessage(levelData.Phases[currentPhaseIndex].Name));
        }

        currentMix.Clear();
        currentMixTotal = 0f;
        if (ph == null || ph.Mix == null || ph.Mix.Length == 0)
        {
            foreach (var kv in pointsByType)
                currentMix.Add(new PhasePick { type = kv.Key, w = 1f });
            currentMixTotal = currentMix.Count;
            return;
        }
        foreach (var m in ph.Mix)
        {
            if (string.IsNullOrWhiteSpace(m.Type)) continue;
            if (!System.Enum.TryParse<BallType>(m.Type, true, out var t)) continue;
            float w = Mathf.Max(0f, m.Poids);
            if (w <= 0f) continue;
            currentMix.Add(new PhasePick { type = t, w = w });
            currentMixTotal += w;
        }
        if (currentMix.Count == 0)
        {
            foreach (var kv in pointsByType)
                currentMix.Add(new PhasePick { type = kv.Key, w = 1f });
            currentMixTotal = currentMix.Count;
        }
    }

    public void RecalculatePlan(float durationSec, float newInterval)
    {
        interval = newInterval;
        PlannedSpawnCount = ComputePlannedCount(durationSec, interval, spawnAtT0, includeEndTick);
    }

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
            loop = usePhases ? StartCoroutine(SpawnLoopPhased()) : StartCoroutine(SpawnLoop());
        }
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
        if (spawnAtT0 && running)
            SpawnOne();

        while (running)
        {
            yield return new WaitForSeconds(interval);
            if (!running) break;
            SpawnOne();
        }

        loop = null;
    }

    private IEnumerator SpawnLoopPhased()
    {
        float total = 0f;
        float t = 0f;

        if (spawnAtT0 && running)
            SpawnOne();

        float totalDuration = Mathf.Max(0f, levelData != null ? levelData.LevelDurationSec : 0f);

        while (running && total < totalDuration)
        {
            float dt = Time.deltaTime;
            total += dt;
            currentPhaseElapsed += dt;
            t += dt;

            var ph = GetCurrentPhase();
            if (ph != null && currentPhaseElapsed >= ph.DurationSec)
            {
                AdvancePhase();
                t = 0f;
            }

            float curInterval = GetPhaseInterval(ph);
            if (t >= curInterval)
            {
                SpawnOne();
                t = 0f;
            }
            yield return null;
        }

        loop = null;
    }

    private IEnumerator ShowPhaseMessage(string name)
    {
        messageOverlay.SetActive(true);
        messageTxt.text = name.ToUpperInvariant();

        yield return new WaitForSeconds(messageDuration);

        messageOverlay.SetActive(false);
    }


    private void SpawnOne()
    {
        float x = Random.Range(-xRange, xRange);
        Vector3 spawnPos = new(x, ySpawn, zSpawn);
        var go = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        var st = go.GetComponent<BallState>();
        if (st != null)
        {
            var choice = PickChoicePhasedOrEqual();
            st.Initialize(choice.type, choice.points);
        }
    }

    private BallChoice PickChoicePhasedOrEqual()
    {
        if (usePhases && currentMix.Count > 0 && currentMixTotal > 0f)
        {
            float r = Random.value * currentMixTotal;
            float acc = 0f;
            for (int i = 0; i < currentMix.Count; i++)
            {
                acc += currentMix[i].w;
                if (r <= acc)
                {
                    var t = currentMix[i].type;
                    return new BallChoice { type = t, points = pointsByType.TryGetValue(t, out var p) ? p : 0 };
                }
            }
        }
        if (pointsByType.Count > 0)
        {
            int idx = Random.Range(0, pointsByType.Count);
            int i = 0;
            foreach (var kv in pointsByType)
            {
                if (i++ == idx)
                    return new BallChoice { type = kv.Key, points = kv.Value };
            }
        }
        return new BallChoice { type = BallType.White, points = 100 };
    }

    private PhaseData GetCurrentPhase()
    {
        if (!usePhases || levelData == null || levelData.Phases == null || levelData.Phases.Length == 0) return null;
        return levelData.Phases[Mathf.Clamp(currentPhaseIndex, 0, levelData.Phases.Length - 1)];
    }

    private void AdvancePhase()
    {
        if (levelData == null || levelData.Phases == null) return;
        if (currentPhaseIndex < levelData.Phases.Length - 1)
        {
            currentPhaseIndex++;
            currentPhaseElapsed = 0f;
            BuildCurrentMix(levelData.Phases[currentPhaseIndex]);
        }
    }

    private float GetPhaseInterval(PhaseData ph)
    {
        if (ph != null && ph.Intervalle > 0f) return ph.Intervalle;
        return interval;
    }

    private int ComputePlannedCount(float duration, float z, bool atT0, bool allowEnd)
    {
        if (duration <= 0f || z <= 0f) return 0;
        const float eps = 1e-6f;
        if (atT0)
            return allowEnd ? Mathf.FloorToInt(duration / z) + 1 : Mathf.FloorToInt((duration - eps) / z) + 1;
        else
            return allowEnd ? Mathf.FloorToInt(duration / z) : Mathf.FloorToInt((duration - eps) / z);
    }

    private struct BallChoice
    {
        public BallType type;
        public int points;
    }
}