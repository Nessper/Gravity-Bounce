using UnityEngine;

public class RuntimeComboOverlayController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FlushResolutionEngine flushResolutionEngine;

    [Header("UI")]
    [SerializeField] private RuntimeComboOverlayUI overlayUI;

    private ComboDefinitionProvider definitionProvider;

    private ChainRuntimeState chainState;
    private TimingRuntimeState timingState;

    public RectTransform TimingSourceRoot
    {
        get
        {
            if (overlayUI == null)
                return null;

            return overlayUI.TimingSourceRoot;
        }
    }

    private void Awake()
    {
        if (overlayUI != null)
            overlayUI.HideAll();
    }

    private void Update()
    {
        if (timingState == null)
            return;

        if (!timingState.IsWindowActive)
            return;

        timingState.Update(Time.time);
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void Bind()
    {
        Unbind();

        if (flushResolutionEngine == null)
        {
            Debug.LogWarning("[RuntimeComboOverlayController] FlushResolutionEngine non assigne.");
            return;
        }

        definitionProvider = ComboDefinitionProvider.Instance;

        chainState = flushResolutionEngine.GetChainState();
        timingState = flushResolutionEngine.GetTimingState();

        if (chainState != null)
        {
            chainState.OnChainProgressChanged += HandleChainProgressChanged;
            chainState.OnChainsReset += HandleChainsReset;
        }

        if (timingState != null)
        {
            timingState.OnTimingWindowStarted += HandleTimingWindowStarted;
            timingState.OnTimingWindowUpdated += HandleTimingWindowUpdated;
            timingState.OnTimingWindowConsumed += HandleTimingWindowConsumed;
            timingState.OnTimingWindowExpired += HandleTimingWindowExpired;
            timingState.OnTimingWindowReset += HandleTimingWindowReset;
        }

        RefreshAll();
    }

    public void Unbind()
    {
        if (chainState != null)
        {
            chainState.OnChainProgressChanged -= HandleChainProgressChanged;
            chainState.OnChainsReset -= HandleChainsReset;
        }

        if (timingState != null)
        {
            timingState.OnTimingWindowStarted -= HandleTimingWindowStarted;
            timingState.OnTimingWindowUpdated -= HandleTimingWindowUpdated;
            timingState.OnTimingWindowConsumed -= HandleTimingWindowConsumed;
            timingState.OnTimingWindowExpired -= HandleTimingWindowExpired;
            timingState.OnTimingWindowReset -= HandleTimingWindowReset;
        }

        chainState = null;
        timingState = null;
    }

    public void RefreshAll()
    {
        RefreshChains();
        RefreshTiming();
    }

    private void RefreshChains()
    {
        if (overlayUI == null || chainState == null)
            return;

        RefreshChain(
            ComboIds.WhiteChain,
            chainState.White.CurrentBalls,
            chainState.White.StepBalls,
            chainState.White.AwardedLevel
        );

        RefreshChain(
            ComboIds.BlueChain,
            chainState.Blue.CurrentBalls,
            chainState.Blue.StepBalls,
            chainState.Blue.AwardedLevel
        );

        RefreshChain(
            ComboIds.RedChain,
            chainState.Red.CurrentBalls,
            chainState.Red.StepBalls,
            chainState.Red.AwardedLevel
        );
    }

    private void RefreshChain(
        string comboId,
        int currentBalls,
        int stepBalls,
        int awardedLevel)
    {
        ComboDefinition definition = GetDefinition(comboId);

        string displayName = comboId;
        Color uiColor = Color.white;

        if (definition != null)
        {
            displayName = ComboTextResolver.ResolveDisplayName(
                definition,
                comboId);
            uiColor = definition.UiColor;
        }

        overlayUI.SetChain(
            comboId,
            displayName,
            uiColor,
            currentBalls,
            stepBalls,
            awardedLevel
        );
    }

    private void RefreshTiming()
    {
        if (overlayUI == null || timingState == null)
            return;

        if (!timingState.IsWindowActive)
        {
            overlayUI.HideTiming();
            return;
        }

        ComboDefinition definition = GetDefinition(ComboIds.FastFlush);

        string displayName = ComboIds.FastFlush;
        Color uiColor = Color.white;

        if (definition != null)
        {
            displayName = ComboTextResolver.ResolveDisplayName(
                definition,
                ComboIds.FastFlush);
            uiColor = definition.UiColor;
        }

        overlayUI.SetTiming(
            ComboIds.FastFlush,
            displayName,
            uiColor,
            timingState.WindowRemaining,
            timingState.WindowDuration
        );
    }

    private ComboDefinition GetDefinition(string comboId)
    {
        if (definitionProvider == null)
            definitionProvider = ComboDefinitionProvider.Instance;

        if (definitionProvider == null)
            return null;

        return definitionProvider.Get(comboId);
    }

    private void HandleChainProgressChanged(ChainProgress progress)
    {
        RefreshChains();
    }

    private void HandleChainsReset()
    {
        RefreshChains();

        if (overlayUI != null)
            overlayUI.PulseChainReset();
    }

    private void HandleTimingWindowStarted(float duration)
    {
        RefreshTiming();

        if (overlayUI != null)
            overlayUI.PulseTimingStart();
    }

    private void HandleTimingWindowUpdated(float remaining)
    {
        RefreshTiming();
    }

    private void HandleTimingWindowConsumed()
    {
        if (overlayUI != null)
            overlayUI.PulseTimingSuccess();
    }

    private void HandleTimingWindowExpired()
    {
        if (overlayUI != null)
            overlayUI.PulseTimingExpired();

        RefreshTiming();
    }

    private void HandleTimingWindowReset()
    {
        RefreshTiming();
    }
}
