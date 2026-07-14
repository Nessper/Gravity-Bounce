using System.Collections.Generic;
using UnityEngine;

public class BlackFilterRuntimeController : MonoBehaviour
{
    public static BlackFilterRuntimeController Instance { get; private set; }

    [Header("Ball Definitions")]
    [SerializeField] private BallDefinition blackDefinition;
    [SerializeField] private BallDefinition whiteDefinition;

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
            maxChargesThisMission = Mathf.Max(
                0,
                ModuleRuntimeStats.Instance.BlackFilterChargesPerMission);
    }

    public bool IsReserved(BallState ball)
    {
        return ball != null && reservedBalls.Contains(ball);
    }

    public bool TryReserve(BallState ball)
    {
        if (ball == null)
            return false;

        if (!IsBall(ball, blackDefinition))
            return false;

        if (ball.collected)
            return false;

        if (reservedBalls.Contains(ball))
        {
            ball.SetModuleVisualPreview(whiteDefinition);
            return true;
        }

        if (FreeCharges <= 0)
            return false;

        reservedBalls.Add(ball);
        ball.SetModuleVisualPreview(whiteDefinition);

        return true;
    }

    public void ReleaseReservation(BallState ball)
    {
        if (ball == null)
            return;

        if (reservedBalls.Remove(ball))
            ball.ClearModuleVisualPreview();
    }

    public bool ConsumeReservation(BallState ball)
    {
        if (ball == null)
            return false;

        if (!reservedBalls.Remove(ball))
            return false;

        consumedCharges = Mathf.Clamp(
            consumedCharges + 1,
            0,
            maxChargesThisMission);

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
                ball.ClearModuleVisualPreview();
        }

        reservedBalls.Clear();
    }

    private bool IsBall(BallState ball, BallDefinition definition)
    {
        if (ball == null || definition == null)
            return false;

        return string.Equals(
            ball.BallId,
            definition.Id,
            System.StringComparison.OrdinalIgnoreCase);
    }
}
