public class ChainComboRule : IComboRule
{
    private readonly ChainRuntimeState state;
    private readonly ComboDefinitionProvider definitionProvider;

    public ChainComboRule(
        ChainRuntimeState state,
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

        snapshot.parBallId.TryGetValue("black", out int blackCount);

        if (blackCount > 0)
        {
            state.ResetAll();
            return;
        }

        ProcessChain(
            ChainColor.White,
            ComboIds.WhiteChain,
            snapshot,
            resolution,
            "white");

        ProcessChain(
            ChainColor.Blue,
            ComboIds.BlueChain,
            snapshot,
            resolution,
            "blue");

        ProcessChain(
            ChainColor.Red,
            ComboIds.RedChain,
            snapshot,
            resolution,
            "red");
    }

    private void ProcessChain(
        ChainColor color,
        string comboId,
        BinSnapshot snapshot,
        FlushResolution resolution,
        string ballId)
    {
        ComboDefinition definition =
            definitionProvider.Get(comboId);

        if (definition == null)
            return;

        snapshot.parBallId.TryGetValue(ballId, out int count);
        snapshot.pointsParBallId.TryGetValue(ballId, out int pointsSum);

        if (count <= 0)
            return;

        ChainProgress progress =
            state.Get(color);

        int previousAwardedLevel =
            progress.AwardedLevel;

        state.AddProgress(color, count);

        int currentLevel =
            progress.CurrentLevel;

        if (definition.MaxLevel > 0 && currentLevel > definition.MaxLevel)
            currentLevel = definition.MaxLevel;

        if (currentLevel <= previousAwardedLevel)
            return;

        for (int level = previousAwardedLevel + 1;
             level <= currentLevel;
             level++)
        {
            int bonus =
                definition.ComputeScaledPercentBonus(
                    pointsSum,
                    level);

            if (bonus <= 0)
                continue;

            resolution.AddCombo(
                new ComboEvent(
                    comboId,
                    definition.Family,
                    definition.Intensity,
                    bonus,
                    snapshot.binSource,
                    level));

            state.MarkAwarded(color, level);
        }
    }

    public void Reset()
    {
        state?.ResetAll();
    }
}