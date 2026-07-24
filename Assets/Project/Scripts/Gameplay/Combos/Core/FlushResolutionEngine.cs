using System;
using UnityEngine;

public class FlushResolutionEngine : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private MainUIController mainUIController;

    public event Action<FlushResolution> OnFlushResolved;

    private void Awake()
    {
        // ComboResolver conserve ses états Timing/Chain dans des champs statiques.
        // Chaque nouvelle scène de niveau doit donc repartir d'un état propre.
        ResetRuntimeState();
    }

    public void OnFlush(BinSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        FlushResolution resolution =
            FlushResolutionBuilder.BuildBase(snapshot);

        ComboResolver.Resolve(snapshot, resolution);

        float comboPointsMultiplier =
            ModuleRuntimeStats.Instance != null
                ? ModuleRuntimeStats.Instance.ComboPointsMultiplier
                : 1f;

        resolution.ApplyComboPointsMultiplier(comboPointsMultiplier);

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

        // Le score de base a déjà été ajouté par ScoreManager.GetSnapshot().
        // Cette couche n'ajoute que les bonus de combos.
        if (resolution.ComboTotal != 0)
            scoreManager.AddPoints(resolution.ComboTotal, "Flush Combo Resolution");
    }

    private void NotifyCombos(FlushResolution resolution)
    {
        if (resolution == null || resolution.ComboEvents == null)
            return;

        foreach (ComboEvent comboEvent in resolution.ComboEvents)
        {
            if (scoreManager != null)
                scoreManager.RegisterCombo(comboEvent);

            if (levelManager != null)
                levelManager.NotifyComboTriggered(comboEvent.DefinitionId);
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
        mainUIController?.ClearRuntimeComboIndicators();
    }
}
