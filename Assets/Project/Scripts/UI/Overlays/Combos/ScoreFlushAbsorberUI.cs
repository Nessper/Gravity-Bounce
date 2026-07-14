using System.Collections;
using UnityEngine;

/// <summary>
/// Owns the global cadence of score packet arrivals at the HUD.
/// It never totals packets and never mutates the gameplay score.
/// </summary>
public class ScoreFlushAbsorberUI : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private GameplayScoreImpactUI gameplayScore;
    [SerializeField] private CanvasGroup arrivalFlash;

    [Header("Cadence")]
    [SerializeField] private float intervalBetweenImpacts = 0.075f;

    [Header("Arrival Flash")]
    [SerializeField] private float arrivalFlashDuration = 0.08f;

    private int reservedArrivals;
    private float nextImpactTime;
    private Coroutine flashRoutine;

    public int ReservedArrivalCount => reservedArrivals;

    private void Awake()
    {
        HideFlash();
        nextImpactTime = Time.time;
    }

    private void OnDisable()
    {
        ResetQueue(syncHud: true);
    }

    public float ReserveArrival(float earliestArrivalTime)
    {
        float scheduledTime = Mathf.Max(
            earliestArrivalTime,
            nextImpactTime
        );

        reservedArrivals++;
        nextImpactTime = scheduledTime +
            Mathf.Max(0f, intervalBetweenImpacts);

        return scheduledTime;
    }

    public void CommitArrival(
        int points,
        Color color,
        bool isCombo)
    {
        reservedArrivals = Mathf.Max(0, reservedArrivals - 1);

        if (reservedArrivals == 0)
            nextImpactTime = Time.time;

        PlayFlash();

        gameplayScore?.PunctuateImpact(
            points,
            color,
            isCombo,
            reservedArrivals
        );
    }

    public void CancelArrival()
    {
        reservedArrivals = Mathf.Max(0, reservedArrivals - 1);

        if (reservedArrivals == 0)
            nextImpactTime = Time.time;
    }

    public void ResetQueue(bool syncHud)
    {
        reservedArrivals = 0;
        nextImpactTime = Time.time;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        HideFlash();

        if (syncHud)
            gameplayScore?.ForceResync();
    }

    private void PlayFlash()
    {
        if (arrivalFlash == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        arrivalFlash.alpha = 1f;

        float elapsed = 0f;

        while (elapsed < arrivalFlashDuration)
        {
            elapsed += Time.deltaTime;

            float t = arrivalFlashDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / arrivalFlashDuration);

            arrivalFlash.alpha = 1f - t;
            yield return null;
        }

        HideFlash();
        flashRoutine = null;
    }

    private void HideFlash()
    {
        if (arrivalFlash != null)
            arrivalFlash.alpha = 0f;
    }
}
