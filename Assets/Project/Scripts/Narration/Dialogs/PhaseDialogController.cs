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
/// Les sequences sont resolues a partir du levelId courant via LocalizationManager.
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

    private LocalizationManager Loc => LocalizationManager.Instance;

    private void OnEnable()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<BallSpawner>();

        if (spawner != null)
            spawner.OnPhaseChanged += HandlePhaseChanged;

        if (endSequence == null)
            endSequence = FindFirstObjectByType<EndSequenceController>();

        if (endSequence != null)
            endSequence.OnEvacuationStarted += HandleEvacuationStarted;

        hasIdentity = !string.IsNullOrWhiteSpace(levelId);
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnPhaseChanged -= HandlePhaseChanged;

        if (endSequence != null)
            endSequence.OnEvacuationStarted -= HandleEvacuationStarted;
    }

    /// <summary>
    /// Injecte l identifiant du niveau courant.
    /// </summary>
    public void SetLevelId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        levelId = id;
        hasIdentity = true;
    }

    /// <summary>
    /// Appele lors d un changement de phase par le spawner.
    /// </summary>
    private void HandlePhaseChanged(int phaseIndex, string phaseName)
    {
        PlaySequence(Loc != null ? Loc.GetPhaseSequence(levelId, phaseIndex) : null);
    }

    /// <summary>
    /// Appele au debut de la phase d evacuation.
    /// </summary>
    private void HandleEvacuationStarted()
    {
        PlaySequence(Loc != null ? Loc.GetEvacSequence(levelId) : null);
    }

    /// <summary>
    /// Joue automatiquement une sequence de dialogue gameplay.
    /// </summary>
    private void PlaySequence(DialogSequence sequence)
    {
        if (!CanPlayDialogs())
            return;

        if (sequence == null)
            return;

        DialogLine[] lines = Loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            return;

        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Auto,
            onComplete: null
        );
    }

    /// <summary>
    /// Valide les prerequis communs avant lecture d un dialogue gameplay.
    /// </summary>
    private bool CanPlayDialogs()
    {
        if (!hasIdentity)
            return false;

        if (dialogSequenceRunner == null)
            return false;

        if (Loc == null)
        {
            Debug.LogError("[PhaseDialogController] LocalizationManager.Instance est null.");
            return false;
        }

        if (!Loc.IsReady)
            return false;

        return true;
    }
}