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
    private readonly List<BallState> presentOrdered = new List<BallState>();

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

            if (st.type == BallType.Black)
                return true;
        }

        return false;
    }

    public List<BallState> TakeSnapshotAndClear()
    {
        List<BallState> snapshot = new List<BallState>(presentOrdered.Count);

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
        OnContentChanged?.Invoke();

        RefreshModuleVisualPreviews();

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
        state.SetModuleVisualPreview(null);

        if (BlackFilterRuntimeController.Instance != null)
            BlackFilterRuntimeController.Instance.ReleaseReservation(state);

        if (state.currentSide == side)
        {
            state.inBin = false;
            state.currentSide = Side.None;
        }

        OnContentChanged?.Invoke();

        RefreshModuleVisualPreviews();
    }

    /// <summary>
    /// Recalcule les previews visuels des modules A + B pour le contenu actuel du bin.
    ///
    /// IMPORTANT :
    /// - On repart de zéro à chaque changement de contenu.
    /// - A réserve les noires via BlackFilterRuntimeController.
    /// - B simule les upgrades par flush.
    /// - Le BallType réel des billes ne change jamais ici.
    /// </summary>
    private void RefreshModuleVisualPreviews()
    {
        // ------------------------------------------------------------
        // 1) Reset visuel des billes présentes
        // ------------------------------------------------------------
        for (int i = 0; i < presentOrdered.Count; i++)
        {
            BallState st = presentOrdered[i];
            if (st == null)
                continue;

            st.SetModuleVisualPreview(null);
        }

        // ------------------------------------------------------------
        // 2) Famille A : réserve les noires filtrables
        // ------------------------------------------------------------
        if (BlackFilterRuntimeController.Instance != null)
        {
            for (int i = 0; i < presentOrdered.Count; i++)
            {
                BallState st = presentOrdered[i];
                if (st == null)
                    continue;

                if (st.type != BallType.Black)
                    continue;

                if (!BlackFilterRuntimeController.Instance.IsReserved(st))
                    BlackFilterRuntimeController.Instance.TryReserve(st);
            }
        }

        // ------------------------------------------------------------
        // 3) Famille B : preview White -> Red puis White -> Blue
        // ------------------------------------------------------------
        int whiteToRedLeft = 0;
        int whiteToBlueLeft = 0;

        if (ModuleRuntimeStats.Instance != null)
        {
            whiteToRedLeft = Mathf.Max(0, ModuleRuntimeStats.Instance.FlushWhiteToRedCount);
            whiteToBlueLeft = Mathf.Max(0, ModuleRuntimeStats.Instance.FlushWhiteToBlueCount);
        }

        if (whiteToRedLeft <= 0 && whiteToBlueLeft <= 0)
            return;

        for (int i = 0; i < presentOrdered.Count; i++)
        {
            BallState st = presentOrdered[i];
            if (st == null)
                continue;

            BallType previewBaseType = st.type;

            // Une noire réservée par A est considérée comme White pour B.
            if (BlackFilterRuntimeController.Instance != null &&
                BlackFilterRuntimeController.Instance.IsReserved(st))
            {
                previewBaseType = BallType.White;
            }

            if (previewBaseType != BallType.White)
                continue;

            if (whiteToRedLeft > 0)
            {
                st.SetModuleVisualPreview(BallType.Red);
                whiteToRedLeft--;
            }
            else if (whiteToBlueLeft > 0)
            {
                st.SetModuleVisualPreview(BallType.Blue);
                whiteToBlueLeft--;
            }
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