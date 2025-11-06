using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BinCollector : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform displayContainer;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;
    [SerializeField] private ComboEngine comboEngine;


    [Header("Spawn / Téléport")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector2 xzJitter = new Vector2(0.25f, 0.15f);
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private float delayBeforeTeleport = 1.2f;

    // États de flush par bin
    private bool flushingLeft = false;
    private bool flushingRight = false;

    public bool IsAnyFlushActive => flushingLeft || flushingRight;
    public bool IsLeftFlushing() => flushingLeft;
    public bool IsRightFlushing() => flushingRight;

    // --- API publique ---
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

    // --- Pipelines séparés (verrou armé AVANT StartCoroutine) ---
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

    // --- Coroutine unifiée avec options ---
    private IEnumerator CollectWithOptions(Side side, bool force, bool skipDelay)
    {
        if (!skipDelay)
            yield return new WaitForSecondsRealtime(delayBeforeTeleport);

        var trigger = GetTrigger(side);
        if (trigger == null)
        {
            SetFlushing(side, false);
            yield break;
        }

        // En mode normal : revalider le seuil au moment du flush
        if (!force && trigger.Count < trigger.flushThreshold)
        {
            SetFlushing(side, false);
            yield break;
        }

        List<BallState> lot = trigger.TakeSnapshotAndClear();
        if (lot == null || lot.Count == 0)
        {
            SetFlushing(side, false);
            yield break;
        }

        yield return StartCoroutine(FlushLotAndScore(lot, side));
        SetFlushing(side, false);
    }

    // --- Traitement commun ---
    private IEnumerator FlushLotAndScore(List<BallState> lot, Side side)
    {
        var snapshot = BuildSnapshot(lot, side);

        foreach (var st in lot)
        {
            if (st == null || st.collected) continue;
            DropIntoContainer(st);
            // Optionnel : étaler visuellement si nécessaire
            // yield return null;
        }

        if (scoreManager != null)
            scoreManager.GetSnapshot(snapshot);

        if (comboEngine != null)
            comboEngine.OnFlush(snapshot);

        yield break;
    }

    private void DropIntoContainer(BallState st)
    {
        st.collected = true;

        var go = st.gameObject;
        var rb = go.GetComponent<Rigidbody>();
        var col = go.GetComponent<Collider>();

        // Point de spawn
        Vector3 basePos = spawnPoint != null
            ? spawnPoint.position
            : (displayContainer != null ? displayContainer.position : transform.position);

        Vector3 spawnPos = new Vector3(
            basePos.x + Random.Range(-xzJitter.x, xzJitter.x),
            basePos.y + verticalOffset,
            basePos.z + Random.Range(-xzJitter.y, xzJitter.y)
        );

        // Détacher et restaurer l'échelle du BallState
        go.transform.SetParent(null, true);
        go.transform.localScale = st.Scale;

        if (rb)
        {
            // Téléport "propre" pour Rigidbody (API moderne)
            bool hadCollider = col && col.enabled;
            if (hadCollider) col.enabled = false;

            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;

            rb.position = spawnPos;
            // Optionnel: rb.rotation = Quaternion.identity;

            if (hadCollider) col.enabled = true;

            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Assurer le mouvement libre (notamment sur Z)
            rb.constraints = RigidbodyConstraints.None;

            rb.WakeUp();
        }
        else
        {
            // Fallback sans Rigidbody
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

            // NEW: accumulate points by type
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