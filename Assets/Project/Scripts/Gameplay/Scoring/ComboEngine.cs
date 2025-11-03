using UnityEngine;
using System;
using System.Collections.Generic;

public class ComboEngine : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;

    // New: structured batch event (UI builds text/colors via provider)
    public event Action<(string id, int points)[], string> OnComboBatchIdsTriggered;

    // For testers/recap: per-combo ping
    public event Action<string, int> OnComboIdTriggered;

    // Combo rules
    private const int WHITE_STREAK_THRESHOLD = 5;
    private const float WHITE_STREAK_BONUS_PCT = 0.20f;

    private const int BLUE_RUSH_THRESHOLD = 4;
    private const float BLUE_RUSH_BONUS_PCT = 0.18f;

    private const int RED_STORM_THRESHOLD = 3;
    private const float RED_STORM_BONUS_PCT = 0.25f;

    private const int FAST_FLUSH_BONUS_PCT = 100;
    private const float FAST_FLUSH_WINDOW = 3f; 
    private float _lastFlushTime = -1f;           // sentinelle "pas de dernier flush"

    /// <summary>
    /// Called once per flush with a complete snapshot.
    /// </summary>
    public void OnFlush(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0 || scoreManager == null)
            return;

        int positivePoints = ComputePositivePointsFrom(snapshot);

        // Counts
        snapshot.parType.TryGetValue("White", out int whiteCount);
        snapshot.parType.TryGetValue("Blue", out int blueCount);
        snapshot.parType.TryGetValue("Red", out int redCount);
        snapshot.parType.TryGetValue("Black", out int blackCount);

        // Points per type (positive sums)
        snapshot.pointsParType.TryGetValue("White", out int whitePointsSum);
        snapshot.pointsParType.TryGetValue("Blue", out int bluePointsSum);
        snapshot.pointsParType.TryGetValue("Red", out int redPointsSum);

        var batchIds = new List<(string id, int points)>();

        Debug.Log($"[FastFlushDBG] dt={snapshot.timestamp - _lastFlushTime:0.000} pos={positivePoints} last={_lastFlushTime:0.000} now={snapshot.timestamp:0.000}");

        // ===== COMBO: FAST FLUSH =====
        // Condition: deux flushs a moins de FAST_FLUSH_WINDOW secondes d'intervalle
        float dt = snapshot.timestamp - _lastFlushTime;
        if (_lastFlushTime > 0f && dt >= 0f && dt <= FAST_FLUSH_WINDOW && positivePoints > 0)
        {
            
            int bonus = Mathf.RoundToInt(positivePoints * (FAST_FLUSH_BONUS_PCT / 100f));
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Fast Flush");
                batchIds.Add(("FastFlush", bonus));
                OnComboIdTriggered?.Invoke("FastFlush", bonus);
            }
        }

        // WHITE STREAK: >= 5 whites, bonus on white points only
        if (whiteCount >= WHITE_STREAK_THRESHOLD && whitePointsSum > 0)
        {
            int bonus = Mathf.RoundToInt(whitePointsSum * WHITE_STREAK_BONUS_PCT);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "White Streak");
                batchIds.Add(("WhiteStreak", bonus));
                OnComboIdTriggered?.Invoke("WhiteStreak", bonus);
            }
        }

        // BLUE RUSH: >= 4 blues, bonus on blue points only
        if (blueCount >= BLUE_RUSH_THRESHOLD && bluePointsSum > 0)
        {
            int bonus = Mathf.RoundToInt(bluePointsSum * BLUE_RUSH_BONUS_PCT);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Blue Rush");
                batchIds.Add(("BlueRush", bonus));
                OnComboIdTriggered?.Invoke("BlueRush", bonus);
            }
        }

        // RED STORM: >= 3 reds, bonus on red points only
        if (redCount >= RED_STORM_THRESHOLD && redPointsSum > 0)
        {
            int bonus = Mathf.RoundToInt(redPointsSum * RED_STORM_BONUS_PCT);
            if (bonus > 0)
            {
                scoreManager.AddPoints(bonus, "Red Storm");
                batchIds.Add(("RedStorm", bonus));
                OnComboIdTriggered?.Invoke("RedStorm", bonus);
            }
        }

        // Mise a jour du temps du dernier flush (a faire en fin de OnFlush)
        _lastFlushTime = snapshot.timestamp;

        // Emit structured batch for HUD (binSource kept)
        if (batchIds.Count > 0)
        {
            OnComboBatchIdsTriggered?.Invoke(batchIds.ToArray(), snapshot.binSource);
        }
    }

    // Calcule la somme des points du flush
    // Dans ComboEngine (méthode utilitaire)
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
}
