using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construit le breakdown final du score de fin de niveau.
/// - RawScore : score brut (gameplay pur)
/// - GoalsBonus : bonus des objectifs (principal + secondaires)
/// - BonusTotal : bonus finaux (ex-combos, etc.)
/// - FinalScore : somme totale
/// 
/// IMPORTANT :
/// - Ne fait aucun affichage UI.
/// - Utilise linesBuilder comme source de vérité pour les bonus calculés.
/// </summary>
public static class EndLevelScoreBuilder
{
    public static EndLevelScoreBreakdown Build(
        EndLevelStats stats,
        MainObjectiveResult mainObj,
        List<SecondaryObjectiveResult> secondaryResults,
        EndLevelLinesBuilderUI linesBuilder)
    {
        // --------------------------------------------------
        // RAW SCORE
        // --------------------------------------------------
        int raw = (stats != null) ? Mathf.Max(0, stats.RawScore) : 0;

        // --------------------------------------------------
        // GOALS BONUS
        // --------------------------------------------------
        int goalsBonus = 0;

        if (linesBuilder != null)
        {
            goalsBonus = Mathf.Max(
                0,
                linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults)
            );
        }

        // --------------------------------------------------
        // FINAL BONUS (ex-combos)
        // --------------------------------------------------
        int bonusTotal = 0;

        if (linesBuilder != null)
        {
            bonusTotal = Mathf.Max(0, linesBuilder.LastBonusPoints);
        }

        // --------------------------------------------------
        // BUILD RESULT
        // --------------------------------------------------
        EndLevelScoreBreakdown breakdown = new EndLevelScoreBreakdown
        {
            RawScore = raw,
            GoalsBonus = goalsBonus,
            BonusTotal = bonusTotal,
            FinalScore = raw + goalsBonus + bonusTotal
        };

        return breakdown;
    }
}