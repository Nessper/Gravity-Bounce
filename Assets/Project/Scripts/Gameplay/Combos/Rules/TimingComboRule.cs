public class TimingComboRule : IComboRule
{
    private readonly TimingRuntimeState state;
    private readonly ComboDefinitionProvider definitionProvider;

    public TimingComboRule(
        TimingRuntimeState state,
        ComboDefinitionProvider definitionProvider)
    {
        this.state = state;
        this.definitionProvider = definitionProvider;
    }

    public void Evaluate(
        BinSnapshot snapshot,
        FlushResolution resolution)
    {
        if (snapshot == null || resolution == null)
            return;

        if (state == null || definitionProvider == null)
            return;

        ComboDefinition definition =
            definitionProvider.Get(ComboIds.FastFlush);

        if (definition == null)
            return;

        float currentTime = snapshot.timestamp;

        state.Update(currentTime);

        bool triggered =
            state.IsWindowActive &&
            state.WindowRemaining > 0f &&
            resolution.BaseTotal > 0;

        if (triggered)
        {
            int bonus = definition.ComputeFlatBonus();

            resolution.AddCombo(
                new ComboEvent(
                    ComboIds.FastFlush,
                    definition.Family,
                    definition.Intensity,
                    bonus,
                    snapshot.binSource));

            state.Consume(currentTime);
        }

        if (definition.TimingWindowSec > 0f)
        {
            state.StartWindow(
                definition.TimingWindowSec,
                currentTime);
        }
    }

    public void Reset()
    {
        state?.Reset();
    }
}