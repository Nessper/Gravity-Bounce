using System;
using System.Collections.Generic;
using UnityEngine;

public class BinTrigger : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private Side side;

    [Header("Wiring")]
    [SerializeField] private BinCollector collector;

    [Header("Ball Definitions")]
    [SerializeField] private BallDefinition whiteDefinition;
    [SerializeField] private BallDefinition blueDefinition;
    [SerializeField] private BallDefinition redDefinition;
    [SerializeField] private BallDefinition blackDefinition;

    [Header("Rules")]
    [SerializeField] public int flushThreshold = 5;
    [SerializeField] private bool autoFlushOnThreshold = true;

    public event Action<BallState, Side> OnBallEnteredBin;
    public event Action OnContentChanged;

    private readonly HashSet<BallState> present =
        new HashSet<BallState>();

    private readonly List<BallState> presentOrdered =
        new List<BallState>();

    private bool autoFlushEnabled = true;

    public int Count => present.Count;

    public int BlackCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < presentOrdered.Count; i++)
            {
                BallState st = presentOrdered[i];

                if (st == null)
                    continue;

                if (st.IsVisualDanger)
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

        for (int i = 0; i < presentOrdered.Count; i++)
        {
            BallState st = presentOrdered[i];

            if (st == null)
                continue;

            total += st.points;
        }

        return total;
    }

    public bool ContainsBlack()
    {
        for (int i = 0; i < presentOrdered.Count; i++)
        {
            BallState st = presentOrdered[i];

            if (st == null)
                continue;

            if (st.IsVisualDanger)
                return true;
        }

        return false;
    }

    public List<BallState> TakeSnapshotAndClear()
    {
        List<BallState> snapshot =
            new List<BallState>(presentOrdered.Count);

        for (int i = 0; i < presentOrdered.Count; i++)
        {
            BallState st = presentOrdered[i];

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
        presentOrdered.Clear();

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

        presentOrdered.Add(state);

        state.inBin = true;
        state.currentSide = side;

        OnBallEnteredBin?.Invoke(state, side);

        RefreshModuleVisualPreviews();
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

        presentOrdered.Remove(state);

        state.ClearModuleVisualPreview();

        if (BlackFilterRuntimeController.Instance != null)
            BlackFilterRuntimeController.Instance.ReleaseReservation(state);

        if (state.currentSide == side)
        {
            state.inBin = false;
            state.currentSide = Side.None;
        }

        RefreshModuleVisualPreviews();
        OnContentChanged?.Invoke();
    }

    private void RefreshModuleVisualPreviews()
    {
        if (BlackFilterRuntimeController.Instance != null)
        {
            for (int i = 0; i < presentOrdered.Count; i++)
            {
                BallState st = presentOrdered[i];

                if (st == null)
                    continue;

                if (!IsBall(st, blackDefinition))
                    continue;

                BlackFilterRuntimeController.Instance.TryReserve(st);
            }
        }

        int whiteToRedLeft = 0;
        int whiteToBlueLeft = 0;

        if (ModuleRuntimeStats.Instance != null)
        {
            whiteToRedLeft =
                Mathf.Max(0, ModuleRuntimeStats.Instance.FlushWhiteToRedCount);

            whiteToBlueLeft =
                Mathf.Max(0, ModuleRuntimeStats.Instance.FlushWhiteToBlueCount);
        }

        for (int i = 0; i < presentOrdered.Count; i++)
        {
            BallState st = presentOrdered[i];

            if (st == null)
                continue;

            BallDefinition previewBaseDefinition =
                st.Definition;

            BallDefinition previewDefinition = null;

            if (BlackFilterRuntimeController.Instance != null &&
                BlackFilterRuntimeController.Instance.IsReserved(st))
            {
                previewBaseDefinition = whiteDefinition;
                previewDefinition = whiteDefinition;
            }

            if (IsDefinition(previewBaseDefinition, whiteDefinition) &&
                whiteToRedLeft > 0)
            {
                previewDefinition = redDefinition;
                whiteToRedLeft--;
            }
            else if (IsDefinition(previewBaseDefinition, whiteDefinition) &&
                     whiteToBlueLeft > 0)
            {
                previewDefinition = blueDefinition;
                whiteToBlueLeft--;
            }

            st.SetModuleVisualPreview(previewDefinition);
        }
    }

    private void TryAutoFlush()
    {
        if (!autoFlushEnabled)
            return;

        if (!autoFlushOnThreshold)
            return;

        if (collector == null)
            return;

        int effectiveThreshold =
            collector.GetEffectiveFlushThresholdFor(this);

        if (present.Count < effectiveThreshold)
            return;

        bool sideBusy =
            side == Side.Left
                ? collector.IsLeftFlushing()
                : collector.IsRightFlushing();

        if (!sideBusy)
            collector.CollectFromBin(side);
    }

    private bool IsBall(BallState st, BallDefinition def)
    {
        if (st == null || def == null)
            return false;

        return string.Equals(
            st.BallId,
            def.Id,
            StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDefinition(
        BallDefinition a,
        BallDefinition b)
    {
        if (a == null || b == null)
            return false;

        return string.Equals(
            a.Id,
            b.Id,
            StringComparison.OrdinalIgnoreCase);
    }
}
