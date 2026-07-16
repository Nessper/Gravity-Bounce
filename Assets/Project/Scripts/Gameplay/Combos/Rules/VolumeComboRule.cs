public class VolumeComboRule : IComboRule
{
    private const int SUPER_FLUSH_FALLBACK_COUNT = 6;
    private const int ULTRA_FLUSH_FALLBACK_COUNT = 7;
    private const int MONSTER_FLUSH_FALLBACK_COUNT = 8;

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

        int superThreshold = GetThreshold(
            ComboIds.SuperFlush,
            SUPER_FLUSH_FALLBACK_COUNT);

        int ultraThreshold = GetThreshold(
            ComboIds.UltraFlush,
            ULTRA_FLUSH_FALLBACK_COUNT);

        int monsterThreshold = GetThreshold(
            ComboIds.MonsterFlush,
            MONSTER_FLUSH_FALLBACK_COUNT);

        if (count >= monsterThreshold)
        {
            AddVolumeCombo(
                ComboIds.MonsterFlush,
                snapshot,
                resolution,
                positivePoints);
        }
        else if (count == ultraThreshold)
        {
            AddVolumeCombo(
                ComboIds.UltraFlush,
                snapshot,
                resolution,
                positivePoints);
        }
        else if (count == superThreshold)
        {
            AddVolumeCombo(
                ComboIds.SuperFlush,
                snapshot,
                resolution,
                positivePoints);
        }
    }

    private int GetThreshold(
        string comboId,
        int fallbackThreshold)
    {
        ComboDefinition definition = definitionProvider.Get(comboId);

        return definition != null && definition.Threshold > 0
            ? definition.Threshold
            : fallbackThreshold;
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
