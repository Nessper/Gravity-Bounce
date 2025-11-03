using System.Collections.Generic;
using UnityEngine;
using Enum = System.Enum;

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

    public int PlannedSpawnCount { get; private set; }

    private Coroutine loop;
    private bool running;

    // -------- Config de distribution (depuis JSON universel) --------
    [System.Serializable]
    public class BallChoice
    {
        public BallType type;
        public float weight;
        public int points; // toujours fixé par le JSON
    }

    private readonly List<BallChoice> choices = new();
    private float totalWeight;

    /// <summary>
    /// Configure le spawner directement depuis LevelData (JSON universel).
    /// - Lit data.Billes[].Type (White/Blue/Red/Black), Points, Poids
    /// - Met à jour l'intervalle depuis data.Spawn.Intervalle (si >0)
    /// - Recalcule le plan prévu
    /// </summary>
    public void ConfigureFromLevel(LevelData data)
    {
        choices.Clear();
        totalWeight = 0f;

        if (data == null)
        {
            Debug.LogWarning("[BallSpawner] ConfigureFromLevel: LevelData NULL");
            return;
        }

        // 1) Charger la distribution depuis le JSON
        if (data.Billes != null)
        {
            foreach (var b in data.Billes)
            {
                if (string.IsNullOrWhiteSpace(b.Type)) continue;

                // JSON universel : Type = "White" | "Blue" | "Red" | "Black"
                if (!Enum.TryParse<BallType>(b.Type, true, out var t))
                {
                    Debug.LogWarning($"[BallSpawner] Type JSON inconnu: '{b.Type}' (attendu: White/Blue/Red/Black)");
                    continue;
                }

                var weight = Mathf.Max(0f, b.Poids);
                if (weight <= 0f) continue;

                choices.Add(new BallChoice
                {
                    type = t,
                    weight = weight,
                    points = b.Points
                });
                totalWeight += weight;
            }
        }

        // Fallback si JSON vide/malsaisi
        if (choices.Count == 0)
        {
            Debug.LogWarning("[BallSpawner] Aucune bille valide dans le JSON, fallback White.");
            choices.Add(new BallChoice { type = BallType.White, weight = 1f, points = 100 });
            totalWeight = 1f;
        }

        // 2) Cadence depuis JSON
        if (data.Spawn != null && data.Spawn.Intervalle > 0f)
            interval = data.Spawn.Intervalle;

        // 3) Plan de spawn estimé
        RecalculatePlan(data.LevelDurationSec, interval);
    }

    private BallChoice PickChoice()
    {
        if (choices.Count == 0) return new BallChoice { type = BallType.White, weight = 1f, points = 100 };

        float r = Random.value * totalWeight;
        float acc = 0f;
        foreach (var c in choices)
        {
            acc += c.weight;
            if (r <= acc) return c;
        }
        return choices[choices.Count - 1];
    }
    // ------------------------------------------------------

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
            loop = StartCoroutine(SpawnLoop());
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

    private System.Collections.IEnumerator SpawnLoop()
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

    private void SpawnOne()
    {
        float x = Random.Range(-xRange, xRange);
        Vector3 spawnPos = new Vector3(x, ySpawn, zSpawn);

        var go = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        var st = go.GetComponent<BallState>();

        if (st != null)
        {
            // Tirage pondéré selon les poids du JSON
            var choice = PickChoice();

            // Échelle
            go.transform.localScale = st.Scale;

            // Init depuis JSON universel (type + points)
            st.Initialize(choice.type, choice.points);
        }
    }

    private int ComputePlannedCount(float duration, float z, bool atT0, bool allowEnd)
    {
        if (duration <= 0f || z <= 0f) return 0;
        const float eps = 1e-6f;

        if (atT0)
        {
            if (allowEnd) return Mathf.FloorToInt(duration / z) + 1;
            else return Mathf.FloorToInt((duration - eps) / z) + 1;
        }
        else
        {
            if (allowEnd) return Mathf.FloorToInt(duration / z);
            else return Mathf.FloorToInt((duration - eps) / z);
        }
    }

    private void OnDisable()
    {
        StopSpawning();
    }
}
