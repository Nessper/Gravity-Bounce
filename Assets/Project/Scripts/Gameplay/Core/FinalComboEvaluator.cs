using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Résultat d'un combo final appliqué à la fin du niveau.
/// </summary>
public struct FinalComboResult
{
    /// <summary>
    /// Identifiant du combo (ex: "PerfectRun", "CombosCollector").
    /// Servira à mapper vers un texte lisible ou un style côté UI.
    /// </summary>
    public string id;

    /// <summary>
    /// Valeur en points de ce combo final.
    /// </summary>
    public int points;

    public FinalComboResult(string id, int points)
    {
        this.id = id;
        this.points = points;
    }
}

/// <summary>
/// Contexte global du niveau pour évaluer certains combos finaux.
/// </summary>
public struct FinalComboContext
{
    /// <summary>
    /// Temps écoulé sur le niveau (en secondes).
    /// </summary>
    public int timeElapsedSec;

    /// <summary>
    /// Nombre total de billes activées / utilisées sur le niveau.
    /// </summary>
    public int totalBilles;
}

/// <summary>
/// Évalue les combos "cachés" de fin de niveau à partir des stats globales.
/// </summary>
public static class FinalComboEvaluator
{

    // Seuils potentiels pour de futurs combos (pas encore utilisés à ce stade).
    private const int AlternationThreshold = 4;
    private const int RedCollectorThreshold = 10;

    // Valeurs en points pour différents combos (la plupart ne sont pas encore utilisés).
    private const int PtsNoBlackStreak = 150;
    private const int PtsAlternation = 200;
    private const int PtsRedCollector = 300;
    private const int PtsLongRun = 250;
    private const int PtsMassCollector = 350;

    /// <summary>
    /// Calcule la liste des combos finaux déclenchés pour ce niveau.
    /// Pour le moment, seuls deux combos sont effectivement utilisés :
    /// PerfectRun et CombosCollector.
    /// </summary>
    /// <param name="score">ScoreManager contenant les stats globales.</param>
    /// <param name="ctx">Contexte global du niveau (temps, total billes).</param>
    /// <returns>Liste des combos finaux valides.</returns>
    public static List<FinalComboResult> Evaluate(ScoreManager score, FinalComboContext ctx)
    {
        var results = new List<FinalComboResult>();
        if (score == null) return results;

        // Totaux de billes collectées par type (White, Blue, Red, Black, etc.).
        var totals = score.GetTotalsByTypeSnapshot();

        // Pertes par type de bille.
        var losses = score.GetLossesByTypeSnapshot();

        // Historique des flushs (liste des BinSnapshot).
        var histo = score.GetHistoriqueSnapshot();

        // === PERFECT RUN (aucune perte ET aucune bille noire collectée) ===
        {
            bool noLoss = score.TotalPertes == 0;

            // S'il n'y a pas d'entrée "Black" ou que le total de noires collectées vaut 0,
            // alors on considère qu'aucune bille noire n'a été collectée.
            bool noBlackCollected = !totals.TryGetValue("Black", out var blacks) || blacks == 0;

            if (noLoss && noBlackCollected)
            {
                // Combo final "PerfectRun" avec une grosse valeur en points.
                results.Add(new FinalComboResult("PerfectRun", 500));
            }
        }

        // === COMBOS COLLECTOR (au moins 10 flushs déclenchés sur le niveau) ===
        {
            int flushCount = histo != null ? histo.Count : 0;

            if (flushCount >= 10)
            {
                // Récompense un joueur qui a déclenché beaucoup de flushs pendant le niveau.
                results.Add(new FinalComboResult("CombosCollector", 400));
            }
        }

        // === WHITE MASTER (au moins 5 billes blanches collectées) ===
        {
            if (totals.TryGetValue("White", out int whites) && whites >= 5)
            {
                results.Add(new FinalComboResult("WhiteMaster", 123));
            }
        }


        // Les autres constantes (Alternation, RedCollector, LongRun, MassCollector)
        // seront utilisées plus tard quand on ajoutera de nouveaux combos finaux.

        return results;
    }

    /// <summary>
    /// Calcule la meilleure série d'alternance Gauche/Droite dans l'historique des flushs.
    /// Non utilisée pour le moment, mais prête pour un futur combo d'alternance.
    /// </summary>
    private static int CountBestAlternation(List<BinSnapshot> histo)
    {
        if (histo == null || histo.Count == 0) return 0;

        int run = 1;
        int best = 1;
        var last = histo[0].binSide;

        for (int i = 1; i < histo.Count; i++)
        {
            var s = histo[i].binSide;

            // Si on change de côté, on augmente la run, sinon on repart à 1.
            run = (s != last) ? run + 1 : 1;

            if (run > best)
                best = run;

            last = s;
        }

        return best;
    }
}
