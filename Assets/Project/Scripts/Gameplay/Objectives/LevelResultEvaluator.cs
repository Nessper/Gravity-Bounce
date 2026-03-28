// Chemin recommande (projet Unity) : Scripts/Gameplay/EndLevel/LevelResultEvaluator.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service d'evaluation de fin de niveau.
/// 
/// Responsabilites :
/// - Construire les EndLevelStats a partir du ScoreManager.
/// - Evaluer l'objectif principal.
/// - Evaluer les objectifs secondaires.
/// - Preparer les lignes de bonus de fin de niveau pour la ceremonie.
///
/// IMPORTANT :
/// - La ceremonie consomme EndLevelStats comme source de verite.
/// - Les bonus de fin affiches dans la section "bonus" sont prepares ici
///   dans stats.BonusLines.
/// - On y injecte actuellement :
///   1) les final combos
///   2) les lignes de score venant des modules
///
/// NOTE :
/// - Les effets Hull / HullMax des modules H et C sont geres ailleurs,
///   avant la ceremonie.
/// - Ici, on ne gere que les lignes de score destinees a l'affichage
///   et au calcul du total de la section bonus.
/// </summary>
public static class LevelResultEvaluator
{
    /// <summary>
    /// Bloc de resultat complet retourne au flow de fin de niveau.
    /// </summary>
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
        int elapsedTimeSec,
        FinalComboConfig comboConfig)
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
        // OBJECTIF PRINCIPAL
        // ------------------------------------------------------------------
        // On se cale sur la logique du ScoreManager :
        // - seuil = ObjectiveThreshold
        // - progression = TotalNonBlackBilles
        int required = Mathf.Max(0, scoreManager.ObjectiveThreshold);
        int collectedNonBlack = scoreManager.TotalNonBlackBilles;

        // Meme regle que CheckGoalReached().
        bool success = collectedNonBlack >= required;

        MainObjectiveResult mainObj = new MainObjectiveResult
        {
            Text = levelData.MainObjective != null ? levelData.MainObjective.Text : string.Empty,
            ThresholdPct = 0,
            Required = required,
            Collected = collectedNonBlack,
            Achieved = success,
            BonusApplied = (success && levelData.MainObjective != null)
                ? levelData.MainObjective.Bonus
                : 0
        };

        // ------------------------------------------------------------------
        // STATS DE FIN DE NIVEAU
        // ------------------------------------------------------------------
        EndLevelStats stats = scoreManager.BuildEndLevelStats(elapsedTimeSec);

        // ------------------------------------------------------------------
        // OBJECTIFS SECONDAIRES
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

            // IMPORTANT :
            // On ne touche pas ici au score final de la ceremonie.
            // Les AwardedScore des objectifs secondaires seront utilises
            // plus tard par l'UI de fin de niveau.
        }

        // ------------------------------------------------------------------
        // BONUS DE FIN DE NIVEAU - SOURCE DE VERITE CEREMONIE
        // ------------------------------------------------------------------
        // On reconstruit ici la liste complete des lignes de bonus affichees
        // dans la section bonus de la ceremonie.
        if (stats.BonusLines == null)
            stats.BonusLines = new List<EndLevelStats.EndLevelBonusLine>();
        else
            stats.BonusLines.Clear();

        // ------------------------------------------------------------------
        // FINAL COMBOS
        // ------------------------------------------------------------------
        // Les final combos existants restent evalues ici, puis convertis
        // en lignes de bonus de ceremonie.
        FinalComboContext finalCtx = new FinalComboContext
        {
            timeElapsedSec = stats.TimeElapsedSec,
            totalBilles = stats.BallsCollected + stats.BallsLost
        };

        List<FinalComboResult> finalCombos = FinalComboEvaluator.Evaluate(
            scoreManager,
            finalCtx,
            comboConfig);

        if (finalCombos != null && finalCombos.Count > 0)
        {
            for (int i = 0; i < finalCombos.Count; i++)
            {
                FinalComboResult fc = finalCombos[i];

                EndLevelStats.EndLevelBonusLine bonusLine = new EndLevelStats.EndLevelBonusLine
                {
                    // Pour les final combos, on conserve l'id technique
                    // comme label source. Le style provider UI s'occupe ensuite
                    // de le transformer en texte joli si besoin.
                    Label = fc.id,
                    Base = fc.points,
                    Mult = 1f,
                    Total = fc.points
                };

                stats.BonusLines.Add(bonusLine);
            }
        }

        // ------------------------------------------------------------------
        // BONUS MODULES (SCORE LINES UNIQUEMENT)
        // ------------------------------------------------------------------
        // Les modules peuvent injecter ici des lignes de score pour la ceremonie.
        // Exemple actuel :
        // - Core Growth T1/T2/T3 : delta de score de fin de niveau.
        List<EndLevelBonusEntry> moduleBonusEntries = ModuleEndLevelBonusProvider.Evaluate();

        if (moduleBonusEntries != null && moduleBonusEntries.Count > 0)
        {
            for (int i = 0; i < moduleBonusEntries.Count; i++)
            {
                EndLevelBonusEntry entry = moduleBonusEntries[i];

                EndLevelStats.EndLevelBonusLine bonusLine = new EndLevelStats.EndLevelBonusLine
                {
                    // Pour les modules, le provider retourne deja un label affichable.
                    Label = entry.label,
                    Base = entry.points,
                    Mult = 1f,
                    Total = entry.points
                };

                stats.BonusLines.Add(bonusLine);
            }
        }

        // ------------------------------------------------------------------
        // RESULTAT FINAL
        // ------------------------------------------------------------------
        result.Stats = stats;
        result.MainObjective = mainObj;
        result.SecondaryObjectives = secondaryResults;

        return result;
    }
}