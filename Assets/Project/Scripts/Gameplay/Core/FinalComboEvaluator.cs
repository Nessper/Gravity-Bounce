using System.Collections.Generic;

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
    private const int AlternationThreshold = 4;
    private const int RedCollectorThreshold = 10;

    private const int PtsNoBlackStreak = 150;
    private const int PtsAlternation = 200;
    private const int PtsRedCollector = 300;
    private const int PtsLongRun = 250;
    private const int PtsMassCollector = 350;

    public static List<FinalComboResult> Evaluate(ScoreManager score, FinalComboContext ctx)
    {
        var results = new List<FinalComboResult>();
        if (score == null) return results;

        var totals = score.GetTotalsByTypeSnapshot();
        var losses = score.GetLossesByTypeSnapshot();
        var histo = score.GetHistoriqueSnapshot();

        // === PERFECT RUN (aucune perte ET aucune noire collectée) ===
        {
            bool noLoss = score.TotalPertes == 0;
            bool noBlackCollected = !totals.TryGetValue("Black", out var blacks) || blacks == 0;

            if (noLoss && noBlackCollected)
                results.Add(new FinalComboResult("PerfectRun", 500));
        }
        // === COMBOS COLLECTOR (au moins 10 flushs déclenchés) ===
        {
            int flushCount = histo != null ? histo.Count : 0;
            if (flushCount >= 10)
                results.Add(new FinalComboResult("CombosCollector", 400));
        }



        return results;
    }

    private static int CountBestAlternation(List<BinSnapshot> histo)
    {
        if (histo == null || histo.Count == 0) return 0;

        int run = 1, best = 1;
        var last = histo[0].binSide;

        for (int i = 1; i < histo.Count; i++)
        {
            var s = histo[i].binSide;
            run = (s != last) ? run + 1 : 1;
            if (run > best) best = run;
            last = s;
        }
        return best;
    }
}
