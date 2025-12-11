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
        // === FAST / CLUTCH FINISHER (timing de validation de l objectif) ===
        // ============================================================
        {
            // Temps auquel l objectif principal a ete atteint (en secondes depuis le debut du niveau).
            // -1 signifie "jamais atteint".
            float goalTime = score.MainGoalReachedTimeSec;

            // On ne fait rien si l objectif n a jamais ete atteint
            // ou si la duree totale est invalide.
            if (goalTime >= 0f && ctx.timeElapsedSec > 0)
            {
                // Marge entre la validation et la fin du timer.
                // Exemple: timer 60s, objectif atteint a 45s -> margin = 15.
                float margin = ctx.timeElapsedSec - goalTime;

                // FastFinisher : objectif atteint avec une marge confortable.
                if (margin >= config.fastFinisherMarginSec)
                {
                    results.Add(new FinalComboResult(
                        "FastFinisher",
                        config.fastFinisherPoints
                    ));
                }
                // ClutchFinisher : objectif atteint dans les dernieres secondes.
                else if (margin >= 0f && margin <= config.clutchFinisherMarginSec)
                {
                    results.Add(new FinalComboResult(
                        "ClutchFinisher",
                        config.clutchFinisherPoints
                    ));
                }
            }
        }

        return results;
    }

}
