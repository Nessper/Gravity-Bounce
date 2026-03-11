using UnityEngine;

public static class EndLevelOutcomeBuilder
{
    public static EndLevelOutcome Build(LevelData levelData, bool isVictory, int finalScore)
    {
        int bronze = 0;
        int silver = 0;
        int gold = 0;

        ExtractThresholds(levelData, out bronze, out silver, out gold);

        EndMedal medal = ComputeBestMedal(finalScore, bronze, silver, gold);

        EndLevelOutcome outcome = new EndLevelOutcome
        {
            IsVictory = isVictory,
            FinalScore = Mathf.Max(0, finalScore),
            BronzeThreshold = bronze,
            SilverThreshold = silver,
            GoldThreshold = gold,
            BestMedal = medal
        };

        return outcome;
    }

    private static void ExtractThresholds(LevelData levelData, out int bronze, out int silver, out int gold)
    {
        bronze = 0;
        silver = 0;
        gold = 0;

        if (levelData == null || levelData.ScoreGoals == null)
            return;

        for (int i = 0; i < levelData.ScoreGoals.Length; i++)
        {
            ScoreGoalsData g = levelData.ScoreGoals[i];
            if (g == null)
                continue;

            string t = g.Type;
            if (string.IsNullOrEmpty(t))
                continue;

            int pts = Mathf.Max(0, g.Points);

            if (StringEqualsIgnoreCase(t, "Bronze"))
                bronze = pts;
            else if (StringEqualsIgnoreCase(t, "Silver"))
                silver = pts;
            else if (StringEqualsIgnoreCase(t, "Gold"))
                gold = pts;
        }
    }

    private static EndMedal ComputeBestMedal(int score, int bronze, int silver, int gold)
    {
        if (gold > 0 && score >= gold)
            return EndMedal.Gold;

        if (silver > 0 && score >= silver)
            return EndMedal.Silver;

        if (bronze > 0 && score >= bronze)
            return EndMedal.Bronze;

        return EndMedal.None;
    }

    private static bool StringEqualsIgnoreCase(string a, string b)
    {
        return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }
}
