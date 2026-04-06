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
    private const int FAST_FLUSH_BONUS_POINTS = 100;
    private float _lastFlushTime = -1f;

    // Taille du flush
    private const int SUPER_FLUSH_COUNT = 6;
    private const int ULTRA_FLUSH_COUNT = 7;
    private const int MONSTER_FLUSH_COUNT = 8;

    private const float SUPER_FLUSH_BONUS = 0.10f;
    private const float ULTRA_FLUSH_BONUS = 0.20f;
    private const float MONSTER_FLUSH_BONUS = 0.30f;

    // === CHAINS PAR NOMBRE DE BILLES CUMULÉES ===
    private int whiteChainBalls = 0;
    private int blueChainBalls = 0;
    private int redChainBalls = 0;

    private int whiteChainAwardedMult = 0;
    private int blueChainAwardedMult = 0;
    private int redChainAwardedMult = 0;

    [Header("Chain thresholds (balls cumulated)")]
    [SerializeField] private int whiteChainStepBalls = 10;
    [SerializeField] private int blueChainStepBalls = 8;
    [SerializeField] private int redChainStepBalls = 6;

    [Header("Chain tuning")]
    [SerializeField, Tooltip("Bonus par multiplicateur (x1, x2...) appliqué aux points de la couleur du flush courant.")]
    private float chainBonusBase = 0.15f;

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

    /// <summary>
    /// Reset complet de l'état runtime des combos.
    /// À appeler après le tuto, ou au début d'un vrai niveau si besoin.
    /// </summary>
    public void ResetRuntimeState()
    {
        _lastFlushTime = -1f;

        whiteChainBalls = 0;
        blueChainBalls = 0;
        redChainBalls = 0;

        whiteChainAwardedMult = 0;
        blueChainAwardedMult = 0;
        redChainAwardedMult = 0;
    }

    private void HandleBallLost(string color)
    {
        void ResetWhite()
        {
            whiteChainBalls = 0;
            whiteChainAwardedMult = 0;
        }

        void ResetBlue()
        {
            blueChainBalls = 0;
            blueChainAwardedMult = 0;
        }

        void ResetRed()
        {
            redChainBalls = 0;
            redChainAwardedMult = 0;
        }

        switch (color)
        {
            case "White":
                ResetWhite();
                break;

            case "Blue":
                ResetBlue();
                break;

            case "Red":
                ResetRed();
                break;

            default:
                ResetWhite();
                ResetBlue();
                ResetRed();
                break;
        }
    }

    /// <summary>
    /// Appelé une fois par flush avec un snapshot complet.
    /// </summary>
    public void OnFlush(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0 || scoreManager == null)
            return;

        int positivePoints = ComputePositivePointsFrom(snapshot);

        snapshot.parType.TryGetValue("White", out int whiteCount);
        snapshot.parType.TryGetValue("Blue", out int blueCount);
        snapshot.parType.TryGetValue("Red", out int redCount);
        snapshot.parType.TryGetValue("Black", out int blackCount);

        snapshot.pointsParType.TryGetValue("White", out int whitePointsSum);
        snapshot.pointsParType.TryGetValue("Blue", out int bluePointsSum);
        snapshot.pointsParType.TryGetValue("Red", out int redPointsSum);

        var batchIds = new List<(string id, int points)>();

        // ===== FAST FLUSH =====
        float dt = snapshot.timestamp - _lastFlushTime;
        if (!snapshot.isFinalFlush &&
            _lastFlushTime > 0f &&
            dt >= 0f &&
            dt <= FAST_FLUSH_WINDOW &&
            positivePoints > 0)
        {
            int bonus = FAST_FLUSH_BONUS_POINTS;
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

        // ===== CHAINS =====
        if (blackCount > 0)
        {
            whiteChainBalls = 0;
            blueChainBalls = 0;
            redChainBalls = 0;

            whiteChainAwardedMult = 0;
            blueChainAwardedMult = 0;
            redChainAwardedMult = 0;
        }
        else
        {
            if (whiteCount > 0)
            {
                TryTriggerChainBalls(
                    "White",
                    whiteCount,
                    whitePointsSum,
                    ref whiteChainBalls,
                    ref whiteChainAwardedMult,
                    whiteChainStepBalls,
                    batchIds);
            }

            if (blueCount > 0)
            {
                TryTriggerChainBalls(
                    "Blue",
                    blueCount,
                    bluePointsSum,
                    ref blueChainBalls,
                    ref blueChainAwardedMult,
                    blueChainStepBalls,
                    batchIds);
            }

            if (redCount > 0)
            {
                TryTriggerChainBalls(
                    "Red",
                    redCount,
                    redPointsSum,
                    ref redChainBalls,
                    ref redChainAwardedMult,
                    redChainStepBalls,
                    batchIds);
            }
        }

        _lastFlushTime = snapshot.timestamp;

        if (batchIds.Count > 0)
            OnComboBatchIdsTriggered?.Invoke(batchIds.ToArray(), snapshot.binSource);
    }

    private void TryTriggerChainBalls(
        string color,
        int addBalls,
        int colorPointsSum,
        ref int cumBalls,
        ref int awardedMult,
        int stepBalls,
        List<(string id, int points)> batch)
    {
        if (stepBalls <= 0)
            return;

        cumBalls += addBalls;

        int currentMult = cumBalls / stepBalls;
        if (currentMult <= awardedMult)
            return;

        for (int m = awardedMult + 1; m <= currentMult; m++)
        {
            float pct = chainBonusBase * m;
            int bonus = Mathf.RoundToInt(colorPointsSum * pct);
            if (bonus <= 0)
                continue;

            string label = $"{color} Flush Chain" + (m > 1 ? $" x{m}" : "");
            string id = (color + "FlushChain" + (m > 1 ? $"x{m}" : "")).Replace(" ", "");

            scoreManager.AddPoints(bonus, label);
            batch.Add((id, bonus));
            NotifyCombo(id, bonus);
        }

        awardedMult = currentMult;
    }

    private static int ComputePositivePointsFrom(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.pointsParType == null)
            return 0;

        snapshot.pointsParType.TryGetValue("White", out int w);
        snapshot.pointsParType.TryGetValue("Blue", out int b);
        snapshot.pointsParType.TryGetValue("Red", out int r);

        int p = 0;
        if (w > 0) p += w;
        if (b > 0) p += b;
        if (r > 0) p += r;

        return p;
    }

    private void NotifyCombo(string id, int bonus)
    {
        if (scoreManager != null)
            scoreManager.RegisterComboId(id);

        OnComboIdTriggered?.Invoke(id, bonus);

        if (levelManager != null)
            levelManager.NotifyComboTriggered(id);
    }
}