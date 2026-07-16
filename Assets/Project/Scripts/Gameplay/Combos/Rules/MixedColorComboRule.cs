using System.Collections.Generic;

public readonly struct MixedColorComboMatch
{
    public string DefinitionId { get; }
    public string OccurrenceKey { get; }
    public ComboColorRole[] ColorRoles { get; }

    public MixedColorComboMatch(
        string definitionId,
        string occurrenceKey,
        ComboColorRole[] colorRoles)
    {
        DefinitionId = definitionId;
        OccurrenceKey = occurrenceKey;
        ColorRoles = colorRoles;
    }
}

public static class MixedColorComboMatcher
{
    private static readonly string[] PositiveBallIds =
    {
        "white",
        "blue",
        "red"
    };

    public static List<MixedColorComboMatch> FindMatches(
        int activeTier,
        int whiteCount,
        int blueCount,
        int redCount)
    {
        var matches = new List<MixedColorComboMatch>();

        if (activeTier <= 0)
            return matches;

        int[] counts =
        {
            whiteCount,
            blueCount,
            redCount
        };

        var occurrenceKeys = new HashSet<string>();

        AddOrientedPairMatches(
            matches,
            occurrenceKeys,
            counts,
            ComboIds.JMix41,
            majorRequired: 4,
            minorRequired: 1);

        if (activeTier >= 2)
        {
            AddOrientedPairMatches(
                matches,
                occurrenceKeys,
                counts,
                ComboIds.JMix32,
                majorRequired: 3,
                minorRequired: 2);
        }

        if (activeTier >= 3)
        {
            AddTricolorMatch(
                matches,
                occurrenceKeys,
                counts);
        }

        return matches;
    }

    private static void AddOrientedPairMatches(
        List<MixedColorComboMatch> matches,
        HashSet<string> occurrenceKeys,
        int[] counts,
        string definitionId,
        int majorRequired,
        int minorRequired)
    {
        for (int majorIndex = 0;
             majorIndex < PositiveBallIds.Length;
             majorIndex++)
        {
            if (counts[majorIndex] < majorRequired)
                continue;

            for (int minorIndex = 0;
                 minorIndex < PositiveBallIds.Length;
                 minorIndex++)
            {
                if (minorIndex == majorIndex ||
                    counts[minorIndex] < minorRequired)
                {
                    continue;
                }

                string majorBallId = PositiveBallIds[majorIndex];
                string minorBallId = PositiveBallIds[minorIndex];
                string occurrenceKey =
                    definitionId +
                    "|major:" + majorBallId +
                    "|minor:" + minorBallId;

                if (!occurrenceKeys.Add(occurrenceKey))
                    continue;

                matches.Add(
                    new MixedColorComboMatch(
                        definitionId,
                        occurrenceKey,
                        new[]
                        {
                            new ComboColorRole(
                                ComboOccurrenceRole.Major,
                                majorBallId,
                                counts[majorIndex]),
                            new ComboColorRole(
                                ComboOccurrenceRole.Minor,
                                minorBallId,
                                counts[minorIndex])
                        }));
            }
        }
    }

    private static void AddTricolorMatch(
        List<MixedColorComboMatch> matches,
        HashSet<string> occurrenceKeys,
        int[] counts)
    {
        int colorsWithAtLeastTwo = 0;

        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] <= 0)
                return;

            if (counts[i] >= 2)
                colorsWithAtLeastTwo++;
        }

        if (colorsWithAtLeastTwo < 2)
            return;

        string occurrenceKey =
            ComboIds.JMix221 + "|white|blue|red";

        if (!occurrenceKeys.Add(occurrenceKey))
            return;

        matches.Add(
            new MixedColorComboMatch(
                ComboIds.JMix221,
                occurrenceKey,
                new[]
                {
                    new ComboColorRole(
                        ComboOccurrenceRole.Participant,
                        "white",
                        counts[0]),
                    new ComboColorRole(
                        ComboOccurrenceRole.Participant,
                        "blue",
                        counts[1]),
                    new ComboColorRole(
                        ComboOccurrenceRole.Participant,
                        "red",
                        counts[2])
                }));
    }
}

public class MixedColorComboRule : IComboRule
{
    private readonly ComboDefinitionProvider definitionProvider;
    private readonly int activeTier;

    public MixedColorComboRule(
        ComboDefinitionProvider definitionProvider,
        int activeTier)
    {
        this.definitionProvider = definitionProvider;
        this.activeTier = activeTier;
    }

    public void Evaluate(
        BinSnapshot snapshot,
        FlushResolution resolution)
    {
        if (snapshot == null ||
            resolution == null ||
            definitionProvider == null ||
            activeTier <= 0)
        {
            return;
        }

        snapshot.parBallId.TryGetValue("white", out int whiteCount);
        snapshot.parBallId.TryGetValue("blue", out int blueCount);
        snapshot.parBallId.TryGetValue("red", out int redCount);

        List<MixedColorComboMatch> matches =
            MixedColorComboMatcher.FindMatches(
                activeTier,
                whiteCount,
                blueCount,
                redCount);

        for (int i = 0; i < matches.Count; i++)
        {
            MixedColorComboMatch match = matches[i];
            ComboDefinition definition =
                definitionProvider.Get(match.DefinitionId);

            if (definition == null)
                continue;

            int bonus = definition.ComputeFlatBonus();

            if (bonus <= 0)
                continue;

            resolution.AddCombo(
                new ComboEvent(
                    match.DefinitionId,
                    definition.Family,
                    definition.Intensity,
                    bonus,
                    snapshot.binSource,
                    occurrenceKey: match.OccurrenceKey,
                    colorRoles: match.ColorRoles));
        }
    }
}
