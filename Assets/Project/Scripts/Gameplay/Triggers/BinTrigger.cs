using System.Collections.Generic;
using UnityEngine;

public class BinTrigger : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private Side side;

    [Header("Wiring")]
    [SerializeField] private BinCollector collector;

    [Header("Rules")]
    [SerializeField] public int flushThreshold = 5;
    [SerializeField] private bool autoFlushOnThreshold = true;

    private readonly HashSet<BallState> present = new HashSet<BallState>();
    private bool autoFlushEnabled = true;

    public int Count => present.Count;

    public void SetAutoFlushEnabled(bool enabled)
    {
        autoFlushEnabled = enabled;
    }

    /// <summary>
    /// Utilisé par le collector pour prendre le lot et vider le bin.
    /// </summary>
    public List<BallState> TakeSnapshotAndClear()
    {
        var snapshot = new List<BallState>(present.Count);

        foreach (var st in present)
        {
            if (st == null || st.collected) continue;
            snapshot.Add(st);
        }

        foreach (var st in snapshot)
        {
            present.Remove(st);
            if (st != null && st.currentSide == side)
            {
                st.inBin = false;
                st.currentSide = Side.None;
            }
        }

        return snapshot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        var state = other.GetComponent<BallState>();
        if (state == null || state.collected) return;

        if (present.Add(state))
        {
            state.inBin = true;
            state.currentSide = side;

            if (autoFlushEnabled && autoFlushOnThreshold && collector != null && present.Count >= flushThreshold)
            {
                bool sideBusy = (side == Side.Left) ? collector.IsLeftFlushing() : collector.IsRightFlushing();
                if (!sideBusy)
                    collector.CollectFromBin(side);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        var state = other.GetComponent<BallState>();
        if (state == null) return;

        if (present.Remove(state))
        {
            if (state.currentSide == side)
            {
                state.inBin = false;
                state.currentSide = Side.None;
            }
        }
    }
}
