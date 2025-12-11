using UnityEngine;
using System;
using System.Collections.Generic;

public class ComboEngine : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField, Tooltip("LevelManager (pour notifier les objectifs secondaires ComboCount).")]
    private LevelManager levelManager;

    // === EVENTS (HUD / toasts) ===
    public event Action<(string id, int points)[], string> OnComboBatchIdsTriggered;
    public event Action<string, int> OnComboIdTriggered;

    // === COMBOS INTRA-FLUSH (existants) ===
    private const int WHITE_STREAK_THRESHOLD = 5;
    private const float WHITE_STREAK_BONUS_PCT = 0.20f;

    private const int BLUE_RUSH_THRESHOLD = 4;
    private const float BLUE_RUSH_BONUS_PCT = 0.18f;

    private const int RED_STORM_THRESHOLD = 3;
    private const float RED_STORM_BONUS_PCT = 0.25f;

    // Fast Flush (tempo)
    private const float FAST_FLUSH_WINDOW = 3f;
    private const int FAST_FLUSH_BONUS_POINTS = 100; // BONUS FIXE
    private float _lastFlushTime = -1f;

    // Taille du flush
    private const int SUPER_FLUSH_COUNT = 6;
    private const int ULTRA_FLUSH_COUNT = 7;
    private const int MONSTER_FLUSH_COUNT = 8;

    private const float SUPER_FLUSH_BONUS = 0.10f;
    private const float ULTRA_FLUSH_BONUS = 0.20f;
    private const float MONSTER_FLUSH_BONUS = 0.30f;

    // === CHAINS PAR NOMBRE DE BILLES CUMULÉES ===
    // Cumuls depuis le dernier reset (perte de cette couleur / flush avec noir)
    private int whiteChainBalls = 0;
    private int blueChainBalls = 0;
    private int redChainBalls = 0;

    // Dernier multiplicateur déjà attribué (x1 à step, x2 à 2*step, …)
    private int whiteChainAwardedMult = 0;
    private int blueChainAwardedMult = 0;
    private int redChainAwardedMult = 0;

    // PALIERS MODULABLES PAR COULEUR (exposés Inspector)
    [Header("Chain thresholds (balls cumulated)")]
    [SerializeField] private int whiteChainStepBalls = 10; // x1=10, x2=20, x3=30...
    [SerializeField] private int blueChainStepBalls = 8;  // x1=8,  x2=16, x3=24...
    [SerializeField] private int redChainStepBalls = 6;  // x1=6,  x2=12, x3=18...

    // % de bonus appliqué aux points de la couleur du flush courant (multiplié par xN)
    [SerializeField, Tooltip("Bonus par multiplicateur (x1, x2...) appliqué aux points de la couleur du flush courant.")]
    private float chainBonusBase = 0.15f; // 15% * multiplicateur

    // === LIFECYCLE ===
    private void OnEnable()
    {
        if (scoreManager != null)
            scoreManager.OnBallLost += HandleBallLost;
    }

    private void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.OnBallLost -= HandleBallLost;
    }

    // Reset ciblé sur perte (par couleur / défaut = reset total)
    private void HandleBallLost(string color)
    {
        void ResetWhite() { whiteChainBalls = 0; whiteChainAwardedMult = 0; }
        void ResetBlue() { blueChainBalls = 0; blueChainAwardedMult = 0; }
        void ResetRed() { redChainBalls = 0; redChainAwardedMult = 0; }

        switch (color)
        {
            case "White": ResetWhite(); break;
            case "Blue": ResetBlue(); break;
            case "Red": ResetRed(); break;
            default: ResetWhite(); ResetBlue(); ResetRed(); break; // Black/Unknown -> reset global
        }
    }

    /// <summary>Appelé une fois par flush avec un snapshot complet.</summary>
    public void OnFlush(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0 || scoreManager == null)
            return;

        int positivePoints = ComputePositivePointsFrom(snapshot);

        // Comptages
        snapshot.parType.TryGetValue("White", out int whiteCount);
        snapshot.parType.TryGetValue("Blue", out int blueCount);
        snapshot.parType.TryGetValue("Red", out int redCount);
        snapshot.parType.TryGetValue("Black", out int blackCount);

        // Points par type (positifs)
        snapshot.pointsParType.TryGetValue("White", out int whitePointsSum);
        snapshot.pointsParType.TryGetValue("Blue", out int bluePointsSum);
        snapshot.pointsParType.TryGetValue("Red", out int redPointsSum);

        var batchIds = new List<(string id, int points)>();

        // ===== FAST FLUSH (tempo, bonus fixe) =====
        // Désactivé pour les flushs finaux/forcés (fin de niveau)
        float dt = snapshot.timestamp - _lastFlushTime;
        if (!snapshot.isFinalFlush &&
            _lastFlushTime > 0f &&
            dt >= 0f &&
            dt <= FAST_FLUSH_WINDOW &&
            positivePoints > 0)
        {
            int bonus = FAST_FLUSH_BONUS_POINTS; // +100 fixe
            scoreManager.AddPoints(bonus, "Fast Flush");
            batchIds.Add(("FastFlush", bonus));
            NotifyCombo("FastFlush", bonus);
        }


        // ===== COMBOS INTRA-FLUSH COULEURS =====
        if (whiteCount >= WHITE_STREAK_THRESHOLD && whitePointsSum > 0)
        {
            int bonus = Mathf.RoundToInt(whitePointsSum * WHITE_STREAK_BONUS_PCT);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "White Streak");
                batchIds.Add(("WhiteStreak", bonus));
                NotifyCombo("WhiteStreak", bonus);
            }
        }

        if (blueCount >= BLUE_RUSH_THRESHOLD && bluePointsSum > 0)
        {
            int bonus = Mathf.RoundToInt(bluePointsSum * BLUE_RUSH_BONUS_PCT);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Blue Rush");
                batchIds.Add(("BlueRush", bonus));
                NotifyCombo("BlueRush", bonus);
            }
        }

        if (redCount >= RED_STORM_THRESHOLD && redPointsSum > 0)
        {
            int bonus = Mathf.RoundToInt(redPointsSum * RED_STORM_BONUS_PCT);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Red Storm");
                batchIds.Add(("RedStorm", bonus));
                NotifyCombo("RedStorm", bonus);
            }
        }

        // ===== FLUSH SIZE =====
        if (snapshot.nombreDeBilles >= MONSTER_FLUSH_COUNT)
        {
            int bonus = Mathf.RoundToInt(positivePoints * MONSTER_FLUSH_BONUS);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Monster Flush");
                batchIds.Add(("MonsterFlush", bonus));
                NotifyCombo("MonsterFlush", bonus);
            }
        }
        else if (snapshot.nombreDeBilles == ULTRA_FLUSH_COUNT)
        {
            int bonus = Mathf.RoundToInt(positivePoints * ULTRA_FLUSH_BONUS);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Ultra Flush");
                batchIds.Add(("UltraFlush", bonus));
                NotifyCombo("UltraFlush", bonus);
            }
        }
        else if (snapshot.nombreDeBilles == SUPER_FLUSH_COUNT)
        {
            int bonus = Mathf.RoundToInt(positivePoints * SUPER_FLUSH_BONUS);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Super Flush");
                batchIds.Add(("SuperFlush", bonus));
                NotifyCombo("SuperFlush", bonus);
            }
        }

        // ===== CHAINS PAR BILLES CUMULÉES =====
        if (blackCount > 0)
        {
            // flush noir -> reset total des chains
            whiteChainBalls = blueChainBalls = redChainBalls = 0;
            whiteChainAwardedMult = blueChainAwardedMult = redChainAwardedMult = 0;
        }
        else
        {
            if (whiteCount > 0)
                TryTriggerChainBalls("White", whiteCount, whitePointsSum, ref whiteChainBalls, ref whiteChainAwardedMult, whiteChainStepBalls, batchIds);

            if (blueCount > 0)
                TryTriggerChainBalls("Blue", blueCount, bluePointsSum, ref blueChainBalls, ref blueChainAwardedMult, blueChainStepBalls, batchIds);

            if (redCount > 0)
                TryTriggerChainBalls("Red", redCount, redPointsSum, ref redChainBalls, ref redChainAwardedMult, redChainStepBalls, batchIds);
        }

        // Fin du flush
        _lastFlushTime = snapshot.timestamp;

        if (batchIds.Count > 0)
            OnComboBatchIdsTriggered?.Invoke(batchIds.ToArray(), snapshot.binSource);
    }

    /// <summary>
    /// Ajoute les billes de la couleur au cumul, déclenche aux paliers (stepBalls, 2*stepBalls, …) avec label "Color Flush Chain xN".
    /// Bonus = points de la couleur du flush courant * (chainBonusBase * multiplicateur).
    /// </summary>
    private void TryTriggerChainBalls(
        string color,
        int addBalls,
        int colorPointsSum,
        ref int cumBalls,
        ref int awardedMult,
        int stepBalls,
        List<(string id, int points)> batch)
    {
        if (stepBalls <= 0) return;

        cumBalls += addBalls;

        int currentMult = cumBalls / stepBalls;            // x1 au 1er palier, x2 au 2e, etc.
        if (currentMult <= awardedMult) return;            // pas de nouveau palier franchi

        // Peut franchir plusieurs paliers d’un coup (ex: +12 avec step=6 -> x1 puis x2)
        for (int m = awardedMult + 1; m <= currentMult; m++)
        {
            float pct = chainBonusBase * m;
            int bonus = Mathf.RoundToInt(colorPointsSum * pct);
            if (bonus <= 0) continue;

            string label = $"{color} Flush Chain" + (m > 1 ? $" x{m}" : "");
            string id = (color + "FlushChain" + (m > 1 ? $"x{m}" : "")).Replace(" ", "");

            scoreManager.AddPoints(bonus, label);
            batch.Add((id, bonus));
            NotifyCombo(id, bonus);
        }

        awardedMult = currentMult;
    }

    // Points positifs du flush (couleurs non noires)
    private static int ComputePositivePointsFrom(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.pointsParType == null) return 0;
        snapshot.pointsParType.TryGetValue("White", out int w);
        snapshot.pointsParType.TryGetValue("Blue", out int b);
        snapshot.pointsParType.TryGetValue("Red", out int r);
        int p = 0;
        if (w > 0) p += w;
        if (b > 0) p += b;
        if (r > 0) p += r;
        return p;
    }

    /// <summary>
    /// Notifie les listeners d'un combo individuel (HUD, objectifs secondaires).
    /// </summary>
    private void NotifyCombo(string id, int bonus)
    {
        // Historique global des combos (pour les combos finaux)
        if (scoreManager != null)
            scoreManager.RegisterComboId(id);

        // HUD / toasts
        OnComboIdTriggered?.Invoke(id, bonus);

        // Objectifs secondaires ComboCount
        if (levelManager != null)
            levelManager.NotifyComboTriggered(id);
    }


}
