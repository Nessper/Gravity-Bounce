public class ColorComboRule : IComboRule
{
    private const int WHITE_STREAK_THRESHOLD = 5;
    private const int BLUE_RUSH_THRESHOLD = 4;
    private const int RED_STORM_THRESHOLD = 3;

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

        if (whiteCount >= WHITE_STREAK_THRESHOLD)
        {
            AddColorCombo(
                ComboIds.WhiteStreak,
                snapshot,
                resolution,
                whitePoints);
        }

        if (blueCount >= BLUE_RUSH_THRESHOLD)
        {
            AddColorCombo(
                ComboIds.BlueRush,
                snapshot,
                resolution,
                bluePoints);
        }

        if (redCount >= RED_STORM_THRESHOLD)
        {
            AddColorCombo(
                ComboIds.RedStorm,
                snapshot,
                resolution,
                redPoints);
        }
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