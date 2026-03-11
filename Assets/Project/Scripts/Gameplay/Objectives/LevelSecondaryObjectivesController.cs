using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur runtime des objectifs secondaires.
/// Role:
/// - Setup du SecondaryObjectivesManager depuis LevelData.
/// - Bind/Unbind au ScoreManager (flush snapshots + void losses).
/// - Forward des combos.
/// - Synchronise la phase courante (pour filtrage PhaseIndex sur pertes, et objectifs par phase).
///
/// IMPORTANT:
/// - Source de verite = ScoreManager.
/// - Phase sync = BallSpawner.OnPhaseChanged.
/// - Avec Option A: BallSpawner.OnPhaseChanged emet un index 0-based.
///   Le manager attend du 1-based => on fait +1 ici.
/// </summary>
public class LevelSecondaryObjectivesController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;

    private SecondaryObjectivesManager secondaryManager = new SecondaryObjectivesManager();

    private bool hasSecondaryObjectives;
    private List<SecondaryObjectiveResult> lastResults;

    private bool managerBound;
    private bool spawnerHooked;

    public SecondaryObjectivesManager Manager => hasSecondaryObjectives ? secondaryManager : null;

    public List<SecondaryObjectiveResult> GetLastResults() => lastResults;

    public void SetResults(List<SecondaryObjectiveResult> results) => lastResults = results;

    private void OnEnable()
    {
        BindSpawnerPhaseHook();
        BindManagerIfPossible();
    }

    private void OnDisable()
    {
        UnbindSpawnerPhaseHook();
        UnbindManager();
    }

    private void OnDestroy()
    {
        UnbindSpawnerPhaseHook();
        UnbindManager();
    }

    /// <summary>
    /// Appele par LevelManager pendant le setup du niveau.
    /// </summary>
    public void SetupFromLevel(LevelData data)
    {
        lastResults = null;

        hasSecondaryObjectives = false;

        UnbindManager();
        secondaryManager = new SecondaryObjectivesManager();

        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        hasSecondaryObjectives = true;

        secondaryManager.Setup(data.SecondaryObjectives);

        // Si deja enabled, on bind immediatement
        BindManagerIfPossible();

        // Phase init "safe": Phase 1
        secondaryManager.SetCurrentPhaseIndex1Based(1);
    }

    private void BindManagerIfPossible()
    {
        if (managerBound)
            return;

        if (!hasSecondaryObjectives)
            return;

        if (scoreManager == null)
        {
            Debug.LogWarning("[LevelSecondaryObjectivesController] scoreManager manquant -> objectifs secondaires inactifs.");
            return;
        }

        secondaryManager.Bind(scoreManager);
        managerBound = true;
    }

    private void UnbindManager()
    {
        if (!managerBound)
            return;

        secondaryManager.Unbind();
        managerBound = false;
    }

    private void BindSpawnerPhaseHook()
    {
        if (spawnerHooked)
            return;

        if (ballSpawner == null)
            return;

        // Idempotent
        ballSpawner.OnPhaseChanged -= HandleSpawnerPhaseChanged;
        ballSpawner.OnPhaseChanged += HandleSpawnerPhaseChanged;

        spawnerHooked = true;
    }

    private void UnbindSpawnerPhaseHook()
    {
        if (!spawnerHooked)
            return;

        if (ballSpawner != null)
            ballSpawner.OnPhaseChanged -= HandleSpawnerPhaseChanged;

        spawnerHooked = false;
    }

    /// <summary>
    /// Option A: phaseIndex0Based fourni par le spawner.
    /// On convertit en 1-based pour le SecondaryObjectivesManager.
    /// </summary>
    private void HandleSpawnerPhaseChanged(int phaseIndex0Based, string _)
    {
        if (!hasSecondaryObjectives)
            return;

        int phaseIndex1Based = Mathf.Max(0, phaseIndex0Based + 1);
        secondaryManager.SetCurrentPhaseIndex1Based(phaseIndex1Based);
    }

    /// <summary>
    /// Appele par LevelManager (ou autre) quand un combo est declenche.
    /// </summary>
    public void NotifyComboTriggered(string comboId)
    {
        if (!hasSecondaryObjectives)
            return;

        if (string.IsNullOrEmpty(comboId))
            return;

        secondaryManager.NotifyComboTriggered(comboId);
    }
}
