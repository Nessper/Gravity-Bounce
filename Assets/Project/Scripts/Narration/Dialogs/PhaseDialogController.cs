using UnityEngine;

public class PhaseDialogController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private EndSequenceController endSequence;     
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Identification niveau")]
    [SerializeField] private int worldIndex = 1;
    [SerializeField] private int levelIndex = 1;

    private void OnEnable()
    {
        if (spawner == null)
            spawner = UnityEngine.Object.FindFirstObjectByType<BallSpawner>();

        if (spawner != null)
            spawner.OnPhaseChanged += HandlePhaseChanged;

        if (endSequence == null)
            endSequence = UnityEngine.Object.FindFirstObjectByType<EndSequenceController>();

        if (endSequence != null)
            endSequence.OnEvacuationStarted += HandleEvacuationStarted;
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnPhaseChanged -= HandlePhaseChanged;

        if (endSequence != null)
            endSequence.OnEvacuationStarted -= HandleEvacuationStarted;
    }

    private void HandlePhaseChanged(int phaseIndex, string phaseName)
    {
     
        PlayPhaseDialog(phaseIndex);
    }

    private void HandleEvacuationStarted()
    {

        PlayEvacDialog();
    }

    private void PlayPhaseDialog(int phaseIndex)
    {
        if (dialogSequenceRunner == null)
            return;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            return;

        DialogSequence seq = dialogManager.GetPhaseSequence(worldIndex, levelIndex, phaseIndex);
        if (seq == null)
            return;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            return;

        dialogSequenceRunner.Play(lines, onComplete: null);
    }

    private void PlayEvacDialog()
    {
        if (dialogSequenceRunner == null)
            return;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            return;

        DialogSequence seq = dialogManager.GetEvacSequence(worldIndex, levelIndex);
        if (seq == null)
            return;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            return;

        dialogSequenceRunner.Play(lines, onComplete: null);
    }


    public void SetWorldAndLevel(int world, int level)
    {
        worldIndex = world;
        levelIndex = level;
    }
}
