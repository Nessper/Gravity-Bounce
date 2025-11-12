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
        // Prenons TOUT ce qui est présent, même si déjà `collected`
        var snapshot = new List<BallState>(present.Count);

        // Copie robuste + purge
        foreach (var st in present)
        {
            if (st == null) continue;
            snapshot.Add(st);

            // Reset des flags de présence côté bin
            if (st.currentSide == side)
            {
                st.inBin = false;
                st.currentSide = Side.None;
            }
        }

        // On vide complètement le set — important car OnTriggerExit ne sera pas appelé
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
