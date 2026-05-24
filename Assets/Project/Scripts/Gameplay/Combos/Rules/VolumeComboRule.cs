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

        if (snapshot.pointsParBallId == null)
            return total;

        foreach (var kv in snapshot.pointsParBallId)
        {
            if (kv.Value > 0)
                total += kv.Value;
        }

        return total;
    }
}