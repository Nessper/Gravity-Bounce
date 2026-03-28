using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Orchestrateur de la phase d'evacuation.
/// - Configure EndSequenceController (duree evac, callbacks UI, progress bar)
/// - Expose OnGameplaySealed (relai depuis EndSequenceController)
///
/// Note :
/// - Aucune logique musique ici.
/// - Peut relayer un callback intermediaire avant fermeture du board.
/// </summary>
public class LevelEvacuationController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private EndSequenceController endSequence;
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private PauseController pauseController;

    [Header("UI")]
    [SerializeField] private EvacTimerUI evacTimerUI;
    [SerializeField] private ProgressBarUI progressBarUI;

    public event Action OnGameplaySealed;

    private void OnEnable()
    {
        if (endSequence != null)
            endSequence.OnGameplaySealed += HandleGameplaySealed;
    }

    private void OnDisable()
    {
        if (endSequence != null)
            endSequence.OnGameplaySealed -= HandleGameplaySealed;
    }

    private void HandleGameplaySealed()
    {
        OnGameplaySealed?.Invoke();
    }

    public void Configure(LevelData data, Func<IEnumerator> onBeforeBoardOutroCb = null)
    {
        if (endSequence == null)
            return;

        float evacDuration = 10f;
        if (data != null && data.Evacuation != null)
            evacDuration = Mathf.Max(0.1f, data.Evacuation.DurationSec);

        endSequence.Configure(
            collector,
            player,
            closeBinController,
            pauseController,
            evacDuration: evacDuration,
            tickInterval: 1f,
            onEvacStartCb: () =>
            {
                if (evacTimerUI != null)
                    evacTimerUI.OnEvacStart();
            },
            onEvacTickCb: (remaining) =>
            {
                if (evacTimerUI != null)
                    evacTimerUI.OnEvacTick(remaining);
            },
            progressBar: progressBarUI,
            onBeforeBoardOutroCb: onBeforeBoardOutroCb
        );
    }

    public void ResetState()
    {
        if (endSequence != null)
            endSequence.ResetState();
    }

    public void AbortEvacuation()
    {
        if (endSequence != null)
            endSequence.AbortSequence();
    }

    public void BeginEvacuationPhase(Action onComplete)
    {
        if (endSequence == null)
        {
            onComplete?.Invoke();
            return;
        }

        endSequence.BeginEvacuationPhase(() =>
        {
            onComplete?.Invoke();
        });
    }
}