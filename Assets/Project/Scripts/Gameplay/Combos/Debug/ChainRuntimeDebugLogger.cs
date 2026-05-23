using UnityEngine;

public class ChainRuntimeDebugLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlushResolutionEngine flushResolutionEngine;

    [Header("Options")]
    [SerializeField] private bool logProgress = true;
    [SerializeField] private bool logLevelReached = true;
    [SerializeField] private bool logReset = true;

    private ChainRuntimeState state;

    private void Awake()
    {
        if (flushResolutionEngine == null)
            flushResolutionEngine = Object.FindFirstObjectByType<FlushResolutionEngine>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (flushResolutionEngine == null)
            return;

        state = flushResolutionEngine.GetChainState();

        if (state == null)
            return;

        state.OnChainProgressChanged += HandleProgressChanged;
        state.OnChainLevelReached += HandleLevelReached;
        state.OnChainsReset += HandleChainsReset;
    }

    private void Unsubscribe()
    {
        if (state == null)
            return;

        state.OnChainProgressChanged -= HandleProgressChanged;
        state.OnChainLevelReached -= HandleLevelReached;
        state.OnChainsReset -= HandleChainsReset;

        state = null;
    }

    private void HandleProgressChanged(ChainProgress progress)
    {
        if (!logProgress || progress == null)
            return;

        Debug.Log(
            $"[ChainState] {progress.Color} " +
            $"{progress.ProgressInCurrentLevel}/{progress.StepBalls} " +
            $"Level={progress.CurrentLevel} " +
            $"Awarded={progress.AwardedLevel} " +
            $"TotalBalls={progress.CurrentBalls}");
    }

    private void HandleLevelReached(
        ChainProgress progress,
        int level)
    {
        if (!logLevelReached || progress == null)
            return;

        Debug.Log(
            $"[ChainState] {progress.Color} CHAIN LEVEL {level} reached");
    }

    private void HandleChainsReset()
    {
        if (!logReset)
            return;

        Debug.Log("[ChainState] All chains reset.");
    }
}