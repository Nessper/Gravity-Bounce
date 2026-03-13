using System;
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

    public event Action<BallState, Side> OnBallEnteredBin;

    private readonly HashSet<BallState> present = new HashSet<BallState>();
    private bool autoFlushEnabled = true;

    public int Count => present.Count;

    public void SetAutoFlushEnabled(bool enabled)
    {
        autoFlushEnabled = enabled;
    }

    public int PeekTotalPoints()
    {
        int total = 0;
        foreach (var st in present)
        {
            if (st == null) continue;
            total += st.points;
        }
        return total;
    }

    public bool ContainsBlack()
    {
        foreach (var st in present)
        {
            if (st == null) continue;
            if (st.type == BallType.Black)
                return true;
        }
        return false;
    }

    public List<BallState> TakeSnapshotAndClear()
    {
        var snapshot = new List<BallState>(present.Count);

        foreach (var st in present)
        {
            if (st == null) continue;
            snapshot.Add(st);

            if (st.currentSide == side)
            {
                st.inBin = false;
                st.currentSide = Side.None;
            }
        }

        present.Clear();

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

            OnBallEnteredBin?.Invoke(state, side);

            if (autoFlushEnabled && autoFlushOnThreshold && collector != null)
            {
                int effectiveThreshold = collector.GetEffectiveFlushThresholdFor(this);
                if (present.Count >= effectiveThreshold)
                {
                    bool sideBusy = (side == Side.Left) ? collector.IsLeftFlushing() : collector.IsRightFlushing();
                    if (!sideBusy)
                        collector.CollectFromBin(side);
                }
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