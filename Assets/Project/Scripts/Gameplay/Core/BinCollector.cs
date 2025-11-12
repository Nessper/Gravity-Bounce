using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BinCollector : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ComboEngine comboEngine;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;
    [SerializeField] private BallSpawner spawner;   // requis pour le recycle

    [Header("Options de flush")]
    [SerializeField] private float delayBeforeFlush = 1.2f; // délai en run normal (pas en fin de niveau)

    // État de flush par côté (évite les débuts en double)
    private bool flushingLeft;
    private bool flushingRight;

    public bool IsAnyFlushActive => flushingLeft || flushingRight;
    public bool IsLeftFlushing() => flushingLeft;
    public bool IsRightFlushing() => flushingRight;

    /// <summary>Active/désactive l'auto-flush sur les deux bacs (utilisé par la fin de niveau).</summary>
    public void SetAutoFlushEnabled(bool enabled)
    {
        if (leftBin != null) leftBin.SetAutoFlushEnabled(enabled);
        if (rightBin != null) rightBin.SetAutoFlushEnabled(enabled);
    }

    // ------------------------------
    // Public API
    // ------------------------------
    public void CollectFromBin(Side side, bool force = false, bool skipDelay = false)
    {
        if (side == Side.Left) CollectLeft(force, skipDelay);
        if (side == Side.Right) CollectRight(force, skipDelay);
    }

    public void CollectAll(bool force = false, bool skipDelay = false)
    {
        CollectLeft(force, skipDelay);
        CollectRight(force, skipDelay);
    }

    // ------------------------------
    // Pipelines internes
    // ------------------------------
    private void CollectLeft(bool force, bool skipDelay)
    {
        if (leftBin == null || flushingLeft) return;
        flushingLeft = true;
        StartCoroutine(CollectWithOptions(Side.Left, force, skipDelay));
    }

    private void CollectRight(bool force, bool skipDelay)
    {
        if (rightBin == null || flushingRight) return;
        flushingRight = true;
        StartCoroutine(CollectWithOptions(Side.Right, force, skipDelay));
    }

    private IEnumerator CollectWithOptions(Side side, bool force, bool skipDelay)
    {
        try
        {
            if (!skipDelay)
                yield return new WaitForSecondsRealtime(delayBeforeFlush);

            var trigger = GetTrigger(side);
            if (trigger == null) yield break;

            // En mode normal, on valide le seuil; en force (fin de niveau), on prend tout.
            if (!force && trigger.Count < trigger.flushThreshold)
                yield break;

            // Snapshot (et purge) du contenu du bac
            List<BallState> lot = trigger.TakeSnapshotAndClear();
            if (lot == null || lot.Count == 0)
                yield break;

            // 1) Construire le snapshot logique (score & combos)
            var snapshot = BuildSnapshot(lot, side);
            scoreManager?.GetSnapshot(snapshot);
            comboEngine?.OnFlush(snapshot);

            // 2) Recyclage: on désactive toutes les billes du lot (même si déjà "collected")
            if (spawner == null)
            {
                Debug.LogError("[BinCollector] Spawner non assigné : impossible de recycler. (Fallback: Destroy)");
                foreach (var st in lot)
                {
                    if (st == null) continue;
                    st.collected = true;
                    Destroy(st.gameObject); // fallback dev pour ne pas polluer la scène
                }
                yield break;
            }

            foreach (var st in lot)
            {
                if (st == null) continue;
                st.collected = true;                 // anti double-compte ailleurs
                st.transform.SetParent(null, true);  // par sécurité
                spawner.Recycle(st.gameObject, collected: true);
            }
        }
        finally
        {
            SetFlushing(side, false);
        }
    }

    // ------------------------------
    // Utilitaires internes
    // ------------------------------
    private BinSnapshot BuildSnapshot(List<BallState> lot, Side side)
    {
        var snapshot = new BinSnapshot
        {
            binSide = (side == Side.Right) ? BinSide.Right : BinSide.Left,
            timestamp = Time.time
        };

        int total = 0;
        for (int i = 0; i < lot.Count; i++)
        {
            var st = lot[i];
            if (st == null) continue;

            total += st.points;
            string typeName = st.type.ToString();

            if (!snapshot.parType.ContainsKey(typeName))
                snapshot.parType[typeName] = 0;
            snapshot.parType[typeName]++;

            if (!snapshot.pointsParType.ContainsKey(typeName))
                snapshot.pointsParType[typeName] = 0;
            snapshot.pointsParType[typeName] += st.points;

            snapshot.nombreDeBilles++;
        }

        snapshot.totalPointsDuLot = total;
        return snapshot;
    }

    private BinTrigger GetTrigger(Side side)
    {
        return side == Side.Left ? leftBin
             : side == Side.Right ? rightBin
             : null;
    }

    private void SetFlushing(Side side, bool value)
    {
        if (side == Side.Left) flushingLeft = value;
        if (side == Side.Right) flushingRight = value;
    }
}
