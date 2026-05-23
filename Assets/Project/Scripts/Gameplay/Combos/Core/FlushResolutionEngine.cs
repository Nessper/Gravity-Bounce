using System;
using UnityEngine;

public class FlushResolutionEngine : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private MainUIController mainUIController;

    public event Action<FlushResolution> OnFlushResolved;

    public void OnFlush(BinSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        FlushResolution resolution =
            FlushResolutionBuilder.BuildBase(snapshot);

        ComboResolver.Resolve(snapshot, resolution);

        ApplyScore(resolution);

        NotifyCombos(resolution);

        OnFlushResolved?.Invoke(resolution);

        if (mainUIController != null)
            mainUIController.PlayFlushResolution(resolution);
    }

    private void ApplyScore(FlushResolution resolution)
    {
        if (scoreManager == null || resolution == null)
            return;

        if (resolution.FinalTotal != 0)
            scoreManager.AddPoints(resolution.FinalTotal, "Flush Resolution");
    }

    private void NotifyCombos(FlushResolution resolution)
    {
        if (resolution == null || resolution.ComboEvents == null)
            return;

        foreach (ComboEvent comboEvent in resolution.ComboEvents)
        {
            if (scoreManager != null)
                scoreManager.RegisterComboId(comboEvent.Id);

            if (levelManager != null)
                levelManager.NotifyComboTriggered(comboEvent.Id);
        }
    }

    public TimingRuntimeState GetTimingState()
    {
        return ComboResolver.GetTimingState();
    }

    public ChainRuntimeState GetChainState()
    {
        return ComboResolver.GetChainState();
    }

    public void ResetRuntimeState()
    {
        ComboResolver.ResetRuntimeState();
    }
}