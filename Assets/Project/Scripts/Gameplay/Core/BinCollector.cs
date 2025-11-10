using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BinCollector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform displayContainer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;
    [SerializeField] private ComboEngine comboEngine;

    [Header("Spawn / Teleport")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector2 xzJitter = new Vector2(0.25f, 0.15f);
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private float delayBeforeTeleport = 1.2f; // utilisé pour les flushs "normaux" en partie

    // Etat de flush par côté
    private bool flushingLeft;
    private bool flushingRight;

    public bool IsAnyFlushActive => flushingLeft || flushingRight;
    public bool IsLeftFlushing() => flushingLeft;
    public bool IsRightFlushing() => flushingRight;

    /// <summary>
    /// Active/désactive l'auto-flush sur les deux bacs (utilisé par la fin de niveau).
    /// </summary>
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
        // Démarre gauche et droite (en parallèle si les deux ont du contenu)
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
                yield return new WaitForSecondsRealtime(delayBeforeTeleport);

            var trigger = GetTrigger(side);
            if (trigger == null) yield break;

            // En mode normal, on revalide le seuil; en force (fin de niveau), on prend tout de suite
            if (!force && trigger.Count < trigger.flushThreshold)
                yield break;

            List<BallState> lot = trigger.TakeSnapshotAndClear();
            if (lot == null || lot.Count == 0)
                yield break;

            yield return FlushLotAndScore(lot, side);
        }
        finally
        {
            SetFlushing(side, false);
        }
    }

    private IEnumerator FlushLotAndScore(List<BallState> lot, Side side)
    {
        var snapshot = BuildSnapshot(lot, side);

        // Téléport/collecte visuelle
        foreach (var st in lot)
        {
            if (st == null || st.collected) continue;
            DropIntoContainer(st);
        }

        // Scoring + combos
        scoreManager?.GetSnapshot(snapshot);
        comboEngine?.OnFlush(snapshot);

        yield break;
    }

    private void DropIntoContainer(BallState st)
    {
        st.collected = true;

        var go = st.gameObject;
        var rb = go.GetComponent<Rigidbody>();
        var col = go.GetComponent<Collider>();

        Vector3 basePos = spawnPoint != null
            ? spawnPoint.position
            : (displayContainer != null ? displayContainer.position : transform.position);

        Vector3 spawnPos = new Vector3(
            basePos.x + Random.Range(-xzJitter.x, xzJitter.x),
            basePos.y + verticalOffset,
            basePos.z + Random.Range(-xzJitter.y, xzJitter.y)
        );

        go.transform.SetParent(null, true);
        go.transform.localScale = st.Scale;

        if (rb)
        {
            bool hadCollider = col && col.enabled;
            if (hadCollider) col.enabled = false;

            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.position = spawnPos;

            if (hadCollider) col.enabled = true;

            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.None;
            rb.WakeUp();
        }
        else
        {
            go.transform.position = spawnPos;
        }
    }

    private BinSnapshot BuildSnapshot(List<BallState> lot, Side side)
    {
        var snapshot = new BinSnapshot
        {
            binSide = (side == Side.Right) ? BinSide.Right : BinSide.Left,
            timestamp = Time.time
        };

        int total = 0;
        foreach (var st in lot)
        {
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
