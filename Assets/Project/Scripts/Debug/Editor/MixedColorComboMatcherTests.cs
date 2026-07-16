using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class MixedColorComboMatcherTests
{
    [TestCase(0, 4, 1, 0, 0)]
    [TestCase(0, 3, 2, 0, 0)]
    [TestCase(0, 2, 2, 1, 0)]
    [TestCase(1, 4, 1, 0, 1)]
    [TestCase(1, 3, 2, 0, 0)]
    [TestCase(2, 4, 1, 0, 1)]
    [TestCase(2, 3, 2, 0, 1)]
    [TestCase(2, 2, 2, 1, 0)]
    [TestCase(3, 2, 2, 1, 1)]
    [TestCase(3, 2, 1, 2, 1)]
    [TestCase(3, 1, 2, 2, 1)]
    public void TierMatrixReturnsExpectedOccurrenceCount(
        int tier,
        int white,
        int blue,
        int red,
        int expectedCount)
    {
        Assert.That(
            MixedColorComboMatcher.FindMatches(
                tier,
                white,
                blue,
                red),
            Has.Count.EqualTo(expectedCount));
    }

    [Test]
    public void ComplexFlushReturnsTwoJ32AndOneTricolorOccurrence()
    {
        var matches = MixedColorComboMatcher.FindMatches(3, 2, 2, 3);

        Assert.That(matches.Select(match => match.OccurrenceKey), Is.EquivalentTo(new[]
        {
            "J_MIX_32|major:red|minor:blue",
            "J_MIX_32|major:red|minor:white",
            "J_MIX_221|white|blue|red"
        }));

        Assert.That(
            matches.Count(match => match.DefinitionId == ComboIds.JMix32),
            Is.EqualTo(2));

        Assert.That(
            matches.Select(match => match.DefinitionId).Distinct().Count(),
            Is.EqualTo(2));

        MixedColorComboMatch tricolor = matches.Single(
            match => match.DefinitionId == ComboIds.JMix221);

        Assert.That(
            tricolor.ColorRoles.Select(
                role => role.BallId + ":" + role.Count),
            Is.EqualTo(new[] { "white:2", "blue:2", "red:3" }));
    }

    [Test]
    public void EqualLargeColorsKeepBothOrientedMatchesWithoutDuplicates()
    {
        var matches = MixedColorComboMatcher.FindMatches(2, 4, 4, 0);

        Assert.That(matches.Select(match => match.OccurrenceKey), Is.EquivalentTo(new[]
        {
            "J_MIX_41|major:white|minor:blue",
            "J_MIX_41|major:blue|minor:white",
            "J_MIX_32|major:white|minor:blue",
            "J_MIX_32|major:blue|minor:white"
        }));

        Assert.That(
            matches.Select(match => match.OccurrenceKey).Distinct().Count(),
            Is.EqualTo(matches.Count));
    }

    [Test]
    public void ScoreManagerStoresOccurrencesButDiversityUsesDefinitionIds()
    {
        GameObject root = new GameObject("ScoreManager_J_Test");

        try
        {
            ScoreManager scoreManager = root.AddComponent<ScoreManager>();

            scoreManager.RegisterCombo(BuildEvent(
                ComboIds.JMix32,
                "J_MIX_32|major:red|minor:blue"));

            scoreManager.RegisterCombo(BuildEvent(
                ComboIds.JMix32,
                "J_MIX_32|major:red|minor:white"));

            scoreManager.RegisterCombo(BuildEvent(
                ComboIds.JMix221,
                "J_MIX_221|white|blue|red"));

            Assert.That(
                scoreManager.GetComboOccurrencesSnapshot(),
                Has.Count.EqualTo(3));

            Assert.That(
                scoreManager.GetCombosTriggeredSnapshot(),
                Has.Count.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void SecondaryObjectivesCountOccurrencesByIdAndAny()
    {
        var manager = new SecondaryObjectivesManager();

        manager.Setup(new[]
        {
            BuildComboObjective("specific", ComboIds.JMix32, 2),
            BuildComboObjective("any", "Any", 3)
        });

        manager.NotifyComboTriggered(ComboIds.JMix32);
        manager.NotifyComboTriggered(ComboIds.JMix32);
        manager.NotifyComboTriggered(ComboIds.JMix221);

        var results = manager.BuildResults();

        Assert.That(results[0].Current, Is.EqualTo(2));
        Assert.That(results[0].Achieved, Is.True);
        Assert.That(results[1].Current, Is.EqualTo(3));
        Assert.That(results[1].Achieved, Is.True);
    }

    private static ComboEvent BuildEvent(
        string definitionId,
        string occurrenceKey)
    {
        return new ComboEvent(
            definitionId,
            ComboFamily.Module,
            ComboIntensity.Major,
            100,
            "Test",
            occurrenceKey: occurrenceKey);
    }

    private static SecondaryObjectiveData BuildComboObjective(
        string id,
        string targetId,
        int threshold)
    {
        return new SecondaryObjectiveData
        {
            Id = id,
            Type = "ComboCount",
            TargetId = targetId,
            Threshold = threshold,
            UiText = id
        };
    }
}
