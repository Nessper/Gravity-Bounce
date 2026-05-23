public class VolumeComboRule : IComboRule
{
    private const int SUPER_FLUSH_COUNT = 6;
    private const int ULTRA_FLUSH_COUNT = 7;
    private const int MONSTER_FLUSH_COUNT = 8;

    private readonly ComboDefinitionProvider definitionProvider;

    public VolumeComboRule(
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

        int positivePoints =
            ComputePositivePoints(snapshot);

        int count =
            snapshot.nombreDeBilles;

        if (count >= MONSTER_FLUSH_COUNT)
        {
            AddVolumeCombo(
                ComboIds.MonsterFlush,
                snapshot,
                resolution,
                positivePoints);
        }
        else if (count == ULTRA_FLUSH_COUNT)
        {
            AddVolumeCombo(
                ComboIds.UltraFlush,
                snapshot,
                resolution,
                positivePoints);
        }
        else if (count == SUPER_FLUSH_COUNT)
        {
            AddVolumeCombo(
                ComboIds.SuperFlush,
                snapshot,
                resolution,
                positivePoints);
        }
    }

    private void AddVolumeCombo(
        string comboId,
        BinSnapshot snapshot,
        FlushResolution resolution,
        int positivePoints)
    {
        ComboDefinition definition =
            definitionProvider.Get(comboId);

        if (definition == null)
            return;

        int bonus =
            definition.ComputePercentBonus(positivePoints);

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

    private int ComputePositivePoints(
        BinSnapshot snapshot)
    {
        int total = 0;

        snapshot.pointsParType.TryGetValue("White", out int w);
        snapshot.pointsParType.TryGetValue("Blue", out int b);
        snapshot.pointsParType.TryGetValue("Red", out int r);

        if (w > 0) total += w;
        if (b > 0) total += b;
        if (r > 0) total += r;

        return total;
    }
}