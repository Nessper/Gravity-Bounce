using System.Collections.Generic;
using System.Linq;

public struct FinalComboResult
{
    public string id;
    public int points;

    public FinalComboResult(string id, int points)
    {
        this.id = id;
        this.points = points;
    }
}

public struct FinalComboContext
{
    public int timeElapsedSec;
    public int totalBilles;
}

public static class FinalComboEvaluator
{
    public static List<FinalComboResult> Evaluate(
        ScoreManager score,
        FinalComboContext ctx,
        FinalComboConfig config)
    {
        var results = new List<FinalComboResult>();
        if (score == null)
            return results;

        // Lecture des valeurs depuis la config (fallbacks si null)
        int ptsNoBlack = config != null ? config.ptsNoBlackCollected : 400;
        int ptsPerfect = config != null ? config.ptsPerfectRun : 700;
        int ptsCombosCollector = config != null ? config.ptsCombosCollector : 200;
        int ptsMaxChainBonus = config != null ? config.ptsMaxChainBonus : 150;
        int ptsComboDiversity = config != null ? config.ptsComboDiversity : 250;
        int ptsColorTrinity = config != null ? config.ptsColorTrinity : 200;
        int ptsChainDuo = config != null ? config.ptsChainDuo : 400;
        float fastMargin = config != null ? config.fastFinisherMarginSec : 10f;
        int fastPoints = config != null ? config.fastFinisherPoints : 250;
        float clutchMargin = config != null ? config.clutchFinisherMarginSec : 3f;
        int clutchPoints = config != null ? config.clutchFinisherPoints : 100;
        int ptsJustInTime = config != null ? config.ptsJustInTime : 80;


        var totals = score.GetTotalsByTypeSnapshot();
        var losses = score.GetLossesByTypeSnapshot();
        var histo = score.GetHistoriqueSnapshot();
        var combosTriggered = score.GetCombosTriggeredSnapshot();

        // ============================================================
        // NO BLACK COLLECTED
        // ============================================================
        {
            int total = score.TotalBilles;
            int nonNoires = score.TotalNonBlackBilles;
            int noiresCollectees = total - nonNoires;

            if (total > 0 && noiresCollectees <= 0)
                results.Add(new FinalComboResult("NoBlackCollected", ptsNoBlack));
        }

        // ============================================================
        // PERFECT RUN
        // ============================================================
        {
            int total = score.TotalBilles;
            int nonNoires = score.TotalNonBlackBilles;
            int noiresCollectees = total - nonNoires;

            bool noBlackCollected = (total > 0 && noiresCollectees <= 0);
            int prevuesNonNoires = score.TotalBillesPrevues;
            bool allNonBlackCollected =
                (prevuesNonNoires > 0 && nonNoires >= prevuesNonNoires);

            if (noBlackCollected && allNonBlackCollected)
                results.Add(new FinalComboResult("PerfectRun", ptsPerfect));
        }

        // ============================================================
        // COMBOS COLLECTOR (>= 10 flushs)
        // ============================================================
        {
            int flushCount = histo != null ? histo.Count : 0;
            if (flushCount >= 10)
                results.Add(new FinalComboResult("CombosCollector", ptsCombosCollector));
        }

        // ============================================================
        // MAX CHAIN BONUS (si un FlushChain couleur a ete declenche)
        // ============================================================
        {
            bool hasColorChain = false;

            if (combosTriggered != null)
            {
                foreach (var id in combosTriggered)
                {
                    if (id.StartsWith("WhiteFlushChain") ||
                        id.StartsWith("BlueFlushChain") ||
                        id.StartsWith("RedFlushChain"))
                    {
                        hasColorChain = true;
                        break;
                    }
                }
            }

            if (hasColorChain)
                results.Add(new FinalComboResult("MaxChainBonus", ptsMaxChainBonus));
        }

        // ============================================================
        // COMBO DIVERSITY (plusieurs types de combos differents)
        // ============================================================
        {
            int distinctCombos = combosTriggered != null ? combosTriggered.Count : 0;

            // Par exemple: au moins 5 combos differents
            if (distinctCombos >= 5)
            {
                results.Add(new FinalComboResult("ComboDiversity", ptsComboDiversity));
            }
        }

        // ============================================================
        // COLOR TRINITY (WhiteStreak + BlueRush + RedStorm tous declenches)
        // ============================================================
        {
            bool hasWhite = combosTriggered != null && combosTriggered.Contains("WhiteStreak");
            bool hasBlue = combosTriggered != null && combosTriggered.Contains("BlueRush");
            bool hasRed = combosTriggered != null && combosTriggered.Contains("RedStorm");

            if (hasWhite && hasBlue && hasRed)
            {
                results.Add(new FinalComboResult("ColorTrinity", ptsColorTrinity));
            }
        }

        // ============================================================
        // CHAIN DUO (au moins deux couleurs avec FlushChain)
        // ============================================================
        {
            bool whiteChain = false;
            bool blueChain = false;
            bool redChain = false;

            if (combosTriggered != null)
            {
                foreach (var id in combosTriggered)
                {
                    if (id.StartsWith("WhiteFlushChain"))
                        whiteChain = true;
                    else if (id.StartsWith("BlueFlushChain"))
                        blueChain = true;
                    else if (id.StartsWith("RedFlushChain"))
                        redChain = true;
                }
            }

            int chainColors =
                (whiteChain ? 1 : 0) +
                (blueChain ? 1 : 0) +
                (redChain ? 1 : 0);

            if (chainColors >= 2)
            {
                results.Add(new FinalComboResult("ChainDuo", ptsChainDuo));
            }
        }

        // ============================================================
        // === FAST / CLUTCH FINISHER ===
        // ============================================================
        {
            // IMPORTANT : si l'objectif est atteint pendant le flush final,
            // on ne doit pas valider Fast/Clutch.
            if (!score.GoalReachedInFinalFlush)
            {
                float goalTime = score.MainGoalReachedTimeSec;

                if (goalTime >= 0f && ctx.timeElapsedSec > 0)
                {
                    float margin = ctx.timeElapsedSec - goalTime;

                    if (margin >= fastMargin)
                        results.Add(new FinalComboResult("FastFinisher", fastPoints));
                    else if (margin >= 0f && margin <= clutchMargin)
                        results.Add(new FinalComboResult("ClutchFinisher", clutchPoints));
                }
            }
        }


        // ============================================================
        // JUST IN TIME (objectif atteint pendant le flush final)
        // ============================================================
        {
            if (score.GoalReachedInFinalFlush)
                results.Add(new FinalComboResult("JustInTime", ptsJustInTime));
        }


        return results;
    }

}
