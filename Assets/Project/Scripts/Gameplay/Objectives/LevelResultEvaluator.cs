using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service d'evaluation de fin de niveau.
/// - Construit les EndLevelStats a partir du ScoreManager.
/// - Evalue l'objectif principal.
/// - Evalue les objectifs secondaires.
/// - Evalue les combos finaux.
/// 
/// LevelManager l'utilise pour obtenir un bloc de donnees complet,
/// puis emet l'event OnEndComputed.
/// </summary>
public static class LevelResultEvaluator
{
    public struct Result
    {
        public EndLevelStats Stats;
        public MainObjectiveResult MainObjective;
        public List<SecondaryObjectiveResult> SecondaryObjectives;
    }

    /// <summary>
    /// Calcule le resultat complet de fin de niveau.
    /// </summary>
    public static Result Evaluate(
        ScoreManager scoreManager,
        LevelData levelData,
        SecondaryObjectivesManager secondaryObjectivesManager,
        int elapsedTimeSec)
    {
        Result result = new Result
        {
            Stats = null,
            MainObjective = default,
            SecondaryObjectives = null
        };

        if (scoreManager == null || levelData == null)
        {
            Debug.LogWarning("[LevelResultEvaluator] ScoreManager ou LevelData manquants.");
            return result;
        }

        int spawnedPlan = scoreManager.TotalBillesPrevues;
        int spawnedReal = scoreManager.GetRealSpawned();
        int spawnedForEval = spawnedReal > 0 ? spawnedReal : spawnedPlan;

        if (spawnedForEval <= 0)
        {
            Debug.LogWarning("[LevelResultEvaluator] Aucune bille, evaluation ignoree.");
            return result;
        }

        // ------------------------------------------------------------------
        // Objectif principal
        // ------------------------------------------------------------------

        // On se cale sur la logique du ScoreManager :
        // - seuil = ObjectiveThreshold (ThresholdCount configuré au début du niveau)
        // - progression = TotalNonBlackBilles (billes NON NOIRES seulement)
        int required = Mathf.Max(0, scoreManager.ObjectiveThreshold);
        int collectedNonBlack = scoreManager.TotalNonBlackBilles;

        // On recalcule le success avec EXACTEMENT la même règle que CheckGoalReached().
        bool success = collectedNonBlack >= required;

        var mainObj = new MainObjectiveResult
        {
            Text = levelData.MainObjective?.Text ?? string.Empty,
            ThresholdPct = 0,
            Required = required,
            Collected = collectedNonBlack,
            Achieved = success,
            BonusApplied = (success && levelData.MainObjective != null)
                ? levelData.MainObjective.Bonus
                : 0
        };


        // ------------------------------------------------------------------
        // Stats de fin de niveau
        // ------------------------------------------------------------------
        var stats = scoreManager.BuildEndLevelStats(elapsedTimeSec);

        // ------------------------------------------------------------------
        // Objectifs secondaires
        // ------------------------------------------------------------------
        List<SecondaryObjectiveResult> secondaryResults = null;

        if (levelData.SecondaryObjectives != null &&
            levelData.SecondaryObjectives.Length > 0 &&
            secondaryObjectivesManager != null)
        {
            secondaryResults = secondaryObjectivesManager.BuildResults();

            int totalReward = secondaryObjectivesManager.GetTotalRewardScore();
            if (totalReward > 0)
            {
                Debug.Log("[LevelResultEvaluator] Secondary objectives reward total = " + totalReward);
            }

            // IMPORTANT : on ne touche pas encore au score final ici.
            // L'utilisation de AwardedScore sera geree plus tard dans la ceremonie.
        }

        // ------------------------------------------------------------------
        // Combos finaux (PerfectRun, WhiteMaster, etc.)
        // ------------------------------------------------------------------
        if (stats.Combos == null)
            stats.Combos = new List<EndLevelStats.ComboCalc>();
        else
            stats.Combos.Clear();

        var finalCtx = new FinalComboContext
        {
            timeElapsedSec = stats.TimeElapsedSec,
            totalBilles = stats.BallsCollected + stats.BallsLost
        };

        var finalCombos = FinalComboEvaluator.Evaluate(scoreManager, finalCtx);

        if (finalCombos != null && finalCombos.Count > 0)
        {
            for (int i = 0; i < finalCombos.Count; i++)
            {
                var fc = finalCombos[i];

                var comboLine = new EndLevelStats.ComboCalc
                {
                    Label = fc.id,   // id technique (PerfectRun, WhiteMaster...)
                    Base = fc.points,
                    Mult = 1f,
                    Total = fc.points
                };

                stats.Combos.Add(comboLine);
            }
        }

        result.Stats = stats;
        result.MainObjective = mainObj;
        result.SecondaryObjectives = secondaryResults;

        return result;
    }
}
