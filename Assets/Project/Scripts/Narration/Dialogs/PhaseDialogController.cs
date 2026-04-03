using UnityEngine;

/// <summary>
/// Gere les dialogues contextuels pendant le gameplay :
/// - changements de phase
/// - debut d evacuation
///
/// Regle UX :
/// - ces dialogues se jouent automatiquement
/// - ils ne doivent pas bloquer le gameplay
/// - ils ne demandent pas de clic joueur
///
/// Les sequences sont resolues a partir du levelId courant :
/// - phase : W1_L2_phase0, W1_L2_phase1, etc.
/// - evacuation : W1_L2_evac
/// </summary>
public class PhaseDialogController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private EndSequenceController endSequence;
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Level Identity")]
    [Tooltip("Identifiant de niveau (ex: 'W1-L2') injecte par LevelManager.")]
    [SerializeField] private string levelId = "W1-L1";

    private bool hasIdentity;

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

        hasIdentity = !string.IsNullOrEmpty(levelId);
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnPhaseChanged -= HandlePhaseChanged;

        if (endSequence != null)
            endSequence.OnEvacuationStarted -= HandleEvacuationStarted;
    }

    /// <summary>
    /// Injecte l'identifiant du niveau courant.
    /// </summary>
    public void SetLevelId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        levelId = id;
        hasIdentity = true;
    }

    /// <summary>
    /// Appelé lors d'un changement de phase par le spawner.
    /// </summary>
    private void HandlePhaseChanged(int phaseIndex, string phaseName)
    {
        PlayPhaseDialog(phaseIndex);
    }

    /// <summary>
    /// Appelé au debut de la phase d'evacuation.
    /// </summary>
    private void HandleEvacuationStarted()
    {
        PlayEvacDialog();
    }

    /// <summary>
    /// Joue automatiquement le dialogue associe a une phase.
    /// </summary>
    private void PlayPhaseDialog(int phaseIndex)
    {
        if (!hasIdentity)
            return;

        if (dialogSequenceRunner == null)
            return;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            return;

        if (!dialogManager.IsReady)
            return;

        string seqId = BuildPhaseSequenceId(phaseIndex);
        if (string.IsNullOrEmpty(seqId))
            return;

        DialogSequence seq = dialogManager.GetSequenceById(seqId);
        if (seq == null)
            return;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            return;

        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Auto,
            onComplete: null
        );
    }

    /// <summary>
    /// Joue automatiquement le dialogue associe a l'evacuation.
    /// </summary>
    private void PlayEvacDialog()
    {
        if (!hasIdentity)
            return;

        if (dialogSequenceRunner == null)
            return;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            return;

        if (!dialogManager.IsReady)
            return;

        string seqId = BuildEvacSequenceId();
        if (string.IsNullOrEmpty(seqId))
            return;

        DialogSequence seq = dialogManager.GetSequenceById(seqId);
        if (seq == null)
            return;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            return;

        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Auto,
            onComplete: null
        );
    }

    /// <summary>
    /// Construit l'identifiant de sequence pour une phase.
    /// Exemple : W1-L2 -> W1_L2_phase0
    /// </summary>
    private string BuildPhaseSequenceId(int phaseIndex)
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        string normalized = levelId.Replace("-", "_");
        return normalized + "_phase" + phaseIndex;
    }

    /// <summary>
    /// Construit l'identifiant de sequence pour l'evacuation.
    /// Exemple : W1-L2 -> W1_L2_evac
    /// </summary>
    private string BuildEvacSequenceId()
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        string normalized = levelId.Replace("-", "_");
        return normalized + "_evac";
    }
}