using System.Collections.Generic;

public static class ComboResolver
{
    // =========================================================
    // RUNTIME STATES
    // =========================================================

    private static readonly ChainRuntimeState ChainState =
        new ChainRuntimeState();

    private static readonly TimingRuntimeState TimingState =
        new TimingRuntimeState();

    // =========================================================
    // CACHED PROVIDER
    // =========================================================

    private static ComboDefinitionProvider definitionProvider;

    private static ComboDefinitionProvider GetProvider()
    {
        if (definitionProvider == null)
            definitionProvider = ComboDefinitionProvider.Instance;

        return definitionProvider;
    }

    // =========================================================
    // RUNTIME RULES
    // =========================================================

    private static ChainComboRule ChainRule =>
    new ChainComboRule(
        ChainState,
        GetProvider());

    private static TimingComboRule TimingRule =>
        new TimingComboRule(
            TimingState,
            GetProvider());

    // =========================================================
    // ACCESSORS
    // =========================================================

    public static ChainRuntimeState GetChainState()
    {
        return ChainState;
    }

    public static TimingRuntimeState GetTimingState()
    {
        return TimingState;
    }

    // =========================================================
    // RESOLVE
    // =========================================================

    public static void Resolve(
        BinSnapshot snapshot,
        FlushResolution resolution)
    {
        if (snapshot == null || resolution == null)
            return;

        List<IComboRule> rules = GetRules();

        for (int i = 0; i < rules.Count; i++)
        {
            rules[i]?.Evaluate(snapshot, resolution);
        }
    }

    private static List<IComboRule> GetRules()
    {
        return new List<IComboRule>
    {
        new ColorComboRule(GetProvider()),
        new VolumeComboRule(GetProvider()),
        TimingRule,
        ChainRule
    };
    }

    // =========================================================
    // RESET
    // =========================================================

    public static void ResetRuntimeState()
    {
        TimingState.Reset();
        ChainState.ResetAll();
    }
}