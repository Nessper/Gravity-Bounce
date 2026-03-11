using System.Collections.Generic;
using UnityEngine;

public static class EndLevelScoreBuilder
{
    public static EndLevelScoreBreakdown Build(
        EndLevelStats stats,
        MainObjectiveResult mainObj,
        List<SecondaryObjectiveResult> secondaryResults,
        EndLevelLinesBuilderUI linesBuilder)
    {
        int raw = (stats != null) ? Mathf.Max(0, stats.RawScore) : 0;

        int goalsBonus = 0;
        if (linesBuilder != null)
            goalsBonus = Mathf.Max(0, linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults));

        int combosBonus = 0;
        if (linesBuilder != null)
            combosBonus = Mathf.Max(0, linesBuilder.LastComboPoints);

        EndLevelScoreBreakdown b = new EndLevelScoreBreakdown
        {
            RawScore = raw,
            GoalsBonus = goalsBonus,
            CombosBonus = combosBonus,
            FinalScore = raw + goalsBonus + combosBonus
        };

        return b;
    }
}
