public class ColorComboRule : IComboRule
{
    private const int WHITE_STREAK_FALLBACK_THRESHOLD = 5;
    private const int BLUE_RUSH_FALLBACK_THRESHOLD = 4;
    private const int RED_STORM_FALLBACK_THRESHOLD = 3;

    private readonly ComboDefinitionProvider definitionProvider;

    public ColorComboRule(
        ComboDefinitionProvider definitionProvider)
    {
        this.definitionProvider = definitionProvider;
    }

    public void Evaluate(
        BinSnapshot snapshot,
        FlushResolution resolution)
    {
        if (snapshot == null || resolution == null)
            return;

        if (definitionProvider == null)
            return;

        snapshot.parBallId.TryGetValue("white", out int whiteCount);
        snapshot.pointsParBallId.TryGetValue("white", out int whitePoints);

        snapshot.parBallId.TryGetValue("blue", out int blueCount);
        snapshot.pointsParBallId.TryGetValue("blue", out int bluePoints);

        snapshot.parBallId.TryGetValue("red", out int redCount);
        snapshot.pointsParBallId.TryGetValue("red", out int redPoints);

        if (MeetsThreshold(
                ComboIds.WhiteStreak,
                whiteCount,
                WHITE_STREAK_FALLBACK_THRESHOLD))
        {
            AddColorCombo(
                ComboIds.WhiteStreak,
                snapshot,
                resolution,
                whitePoints);
        }

        if (MeetsThreshold(
                ComboIds.BlueRush,
                blueCount,
                BLUE_RUSH_FALLBACK_THRESHOLD))
        {
            AddColorCombo(
                ComboIds.BlueRush,
                snapshot,
                resolution,
                bluePoints);
        }

        if (MeetsThreshold(
                ComboIds.RedStorm,
                redCount,
                RED_STORM_FALLBACK_THRESHOLD))
        {
            AddColorCombo(
                ComboIds.RedStorm,
                snapshot,
                resolution,
                redPoints);
        }
    }

    private bool MeetsThreshold(
        string comboId,
        int count,
        int fallbackThreshold)
    {
        ComboDefinition definition = definitionProvider.Get(comboId);
        int threshold = definition != null && definition.Threshold > 0
            ? definition.Threshold
            : fallbackThreshold;

        return count >= threshold;
    }

    private void AddColorCombo(
        string comboId,
        BinSnapshot snapshot,
        FlushResolution resolution,
        int colorPoints)
    {
        ComboDefinition definition =
            definitionProvider.Get(comboId);

        if (definition == null)
            return;

        int bonus =
            definition.ComputePercentBonus(colorPoints);

        if (bonus <= 0)
            return;

        resolution.AddCombo(
            new ComboEvent(
                comboId,
                definition.Family,
                definition.Intensity,
                bonus,
                snapshot.binSource));
    }
}
