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
    public event Action OnContentChanged;

    private readonly HashSet<BallState> present = new HashSet<BallState>();

    private bool autoFlushEnabled = true;

    public int Count => present.Count;

    public int BlackCount
    {
        get
        {
            int count = 0;

            foreach (BallState st in present)
            {
                if (st == null)
                    continue;

                if (st.type == BallType.Black)
                    count++;
            }

            return count;
        }
    }

    public void SetAutoFlushEnabled(bool enabled)
    {
        autoFlushEnabled = enabled;
    }

    public int PeekTotalPoints()
    {
        int total = 0;

        foreach (BallState st in present)
        {
            if (st == null)
                continue;

            total += st.points;
        }

        return total;
    }

    public bool ContainsBlack()
    {
        foreach (BallState st in present)
        {
            if (st == null)
                continue;

            if (st.type == BallType.Black)
                return true;
        }

        return false;
    }

    public List<BallState> TakeSnapshotAndClear()
    {
        List<BallState> snapshot = new List<BallState>(present.Count);

        foreach (BallState st in present)
        {
            if (st == null)
                continue;

            snapshot.Add(st);

            if (st.currentSide == side)
            {
                st.inBin = false;
                st.currentSide = Side.None;
            }
        }

        present.Clear();
        OnContentChanged?.Invoke();

        return snapshot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        BallState state = other.GetComponent<BallState>();

        if (state == null || state.collected)
            return;

        if (!present.Add(state))
            return;

        state.inBin = true;
        state.currentSide = side;

        OnBallEnteredBin?.Invoke(state, side);
        OnContentChanged?.Invoke();

        TryAutoFlush();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        BallState state = other.GetComponent<BallState>();

        if (state == null)
            return;

        if (!present.Remove(state))
            return;

        if (state.currentSide == side)
        {
            state.inBin = false;
            state.currentSide = Side.None;
        }

        OnContentChanged?.Invoke();
    }

    private void TryAutoFlush()
    {
        if (!autoFlushEnabled)
            return;

        if (!autoFlushOnThreshold)
            return;

        if (collector == null)
            return;

        int effectiveThreshold = collector.GetEffectiveFlushThresholdFor(this);

        if (present.Count < effectiveThreshold)
            return;

        bool sideBusy =
            side == Side.Left
            ? collector.IsLeftFlushing()
            : collector.IsRightFlushing();

        if (!sideBusy)
            collector.CollectFromBin(side);
    }
}