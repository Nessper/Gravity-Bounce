using NUnit.Framework;

public class ComboPointsMultiplierTests
{
    [Test]
    public void MultiplierAppliesToEveryOccurrenceAndRecomputesTotal()
    {
        FlushResolution resolution = BuildJResolution();

        resolution.ApplyComboPointsMultiplier(1.22f);

        Assert.That(resolution.ComboEvents, Has.Count.EqualTo(3));
        Assert.That(resolution.ComboEvents[0].Points, Is.EqualTo(183));
        Assert.That(resolution.ComboEvents[1].Points, Is.EqualTo(183));
        Assert.That(resolution.ComboEvents[2].Points, Is.EqualTo(244));
        Assert.That(resolution.ComboTotal, Is.EqualTo(610));
        Assert.That(resolution.AppliedComboMultiplier, Is.EqualTo(1.22f));
        Assert.That(resolution.ComboMultiplierApplied, Is.True);
    }

    [Test]
    public void MultiplierPreservesBasePointsIdsAndOccurrenceKeys()
    {
        FlushResolution resolution = BuildJResolution();

        resolution.ApplyComboPointsMultiplier(1.30f);

        Assert.That(resolution.ComboEvents[0].BasePoints, Is.EqualTo(150));
        Assert.That(resolution.ComboEvents[1].BasePoints, Is.EqualTo(150));
        Assert.That(resolution.ComboEvents[2].BasePoints, Is.EqualTo(200));

        Assert.That(
            resolution.ComboEvents[0].DefinitionId,
            Is.EqualTo(ComboIds.JMix32));
        Assert.That(
            resolution.ComboEvents[1].DefinitionId,
            Is.EqualTo(ComboIds.JMix32));
        Assert.That(
            resolution.ComboEvents[2].DefinitionId,
            Is.EqualTo(ComboIds.JMix221));

        Assert.That(
            resolution.ComboEvents[0].OccurrenceKey,
            Is.EqualTo("J_MIX_32|major:red|minor:blue"));
        Assert.That(
            resolution.ComboEvents[1].OccurrenceKey,
            Is.EqualTo("J_MIX_32|major:red|minor:white"));
        Assert.That(
            resolution.ComboEvents[2].OccurrenceKey,
            Is.EqualTo("J_MIX_221|white|blue|red"));
    }

    [Test]
    public void SecondApplicationDoesNotCompoundOrReplaceFirstMultiplier()
    {
        FlushResolution resolution = BuildJResolution();

        resolution.ApplyComboPointsMultiplier(1.15f);
        int firstTotal = resolution.ComboTotal;

        resolution.ApplyComboPointsMultiplier(1.30f);

        Assert.That(resolution.ComboTotal, Is.EqualTo(firstTotal));
        Assert.That(resolution.ComboEvents[0].Points, Is.EqualTo(172));
        Assert.That(resolution.ComboEvents[1].Points, Is.EqualTo(172));
        Assert.That(resolution.ComboEvents[2].Points, Is.EqualTo(230));
        Assert.That(resolution.AppliedComboMultiplier, Is.EqualTo(1.15f));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(0f)]
    [TestCase(0.5f)]
    public void InvalidOrReducingMultiplierFallsBackToOne(float multiplier)
    {
        FlushResolution resolution = BuildJResolution();

        resolution.ApplyComboPointsMultiplier(multiplier);

        Assert.That(resolution.ComboTotal, Is.EqualTo(500));
        Assert.That(resolution.AppliedComboMultiplier, Is.EqualTo(1f));
    }

    private static FlushResolution BuildJResolution()
    {
        var resolution = new FlushResolution();

        resolution.AddCombo(new ComboEvent(
            ComboIds.JMix32,
            ComboFamily.Module,
            ComboIntensity.Major,
            150,
            "Test",
            occurrenceKey: "J_MIX_32|major:red|minor:blue"));

        resolution.AddCombo(new ComboEvent(
            ComboIds.JMix32,
            ComboFamily.Module,
            ComboIntensity.Major,
            150,
            "Test",
            occurrenceKey: "J_MIX_32|major:red|minor:white"));

        resolution.AddCombo(new ComboEvent(
            ComboIds.JMix221,
            ComboFamily.Module,
            ComboIntensity.Epic,
            200,
            "Test",
            occurrenceKey: "J_MIX_221|white|blue|red"));

        return resolution;
    }
}
