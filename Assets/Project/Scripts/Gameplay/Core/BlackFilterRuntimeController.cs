using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BlackFilterRuntimeController
/// ------------------------------------------------------------
/// Gère l'état vivant du module A pendant UNE mission.
///
/// Règle :
/// - ModuleRuntimeStats dit combien de charges sont disponibles par mission.
/// - Ce controller réserve une charge sur des billes noires présentes dans les bins.
/// - Si la bille sort avant flush, la charge est rendue.
/// - Si la bille est flush, la charge est consommée.
/// - Le BallType réel ne change pas ici.
/// </summary>
public class BlackFilterRuntimeController : MonoBehaviour
{
    public static BlackFilterRuntimeController Instance { get; private set; }

    [Header("Debug / Lecture seule")]
    [SerializeField] private int maxChargesThisMission;
    [SerializeField] private int consumedCharges;

    private readonly HashSet<BallState> reservedBalls = new HashSet<BallState>();

    public int MaxChargesThisMission => maxChargesThisMission;
    public int ConsumedCharges => consumedCharges;
    public int ReservedCharges => reservedBalls.Count;

    public int FreeCharges
    {
        get
        {
            int free = maxChargesThisMission - consumedCharges - reservedBalls.Count;
            return Mathf.Max(0, free);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.AddListener(ResetForMission);

        ResetForMission();
    }

    private void OnDisable()
    {
        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.RemoveListener(ResetForMission);

        ClearReservations();

        if (Instance == this)
            Instance = null;
    }

    public void ResetForMission()
    {
        ClearReservations();

        consumedCharges = 0;
        maxChargesThisMission = 0;

        if (ModuleRuntimeStats.Instance != null)
            maxChargesThisMission = Mathf.Max(0, ModuleRuntimeStats.Instance.BlackFilterChargesPerMission);
    }

    public bool IsReserved(BallState ball)
    {
        if (ball == null)
            return false;

        return reservedBalls.Contains(ball);
    }

    public bool TryReserve(BallState ball)
    {
        if (ball == null)
            return false;

        if (ball.type != BallType.Black)
            return false;

        if (ball.collected)
            return false;

        if (reservedBalls.Contains(ball))
            return true;

        if (FreeCharges <= 0)
            return false;

        reservedBalls.Add(ball);
        ball.SetModuleVisualPreview(BallType.White);
        return true;
    }

    public void ReleaseReservation(BallState ball)
    {
        if (ball == null)
            return;

        if (reservedBalls.Remove(ball))
            ball.SetModuleVisualPreview(null);
    }

    public bool ConsumeReservation(BallState ball)
    {
        if (ball == null)
            return false;

        if (!reservedBalls.Remove(ball))
            return false;

        ball.SetModuleVisualPreview(null);

        consumedCharges = Mathf.Clamp(
            consumedCharges + 1,
            0,
            maxChargesThisMission
        );

        return true;
    }

    public bool WillFilterOnFlush(BallState ball)
    {
        return IsReserved(ball);
    }

    private void ClearReservations()
    {
        foreach (BallState ball in reservedBalls)
        {
            if (ball != null)
                ball.SetModuleVisualPreview(null);
        }

        reservedBalls.Clear();
    }


}