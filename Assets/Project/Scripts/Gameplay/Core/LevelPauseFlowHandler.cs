using UnityEngine;
using VoidScrappers.Briefing;

/// <summary>
/// Gère le contenu et les actions de l'overlay Pause pendant un niveau.
/// 
/// Responsabilités :
/// - assembler les données affichées (world/title, data, phases, tier, score, contracts, credits)
/// - gérer les actions Menu / Retry / Resume (appelées par les boutons via l'Inspector)
/// 
/// IMPORTANT :
/// - Ne touche pas Time.timeScale (c'est PauseController).
/// - Ne montre pas l'overlay (c'est PauseController).
/// </summary>
public class LevelPauseFlowHandler : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PauseController pauseController;
    [SerializeField] private PauseOverlayUI pauseOverlayUI;
    [SerializeField] private RunSessionState runSession;

    /// <summary>
    /// Appelé par LevelManager quand la pause s'ouvre (via PauseController.OnPauseOpening).
    /// Rend le contenu de l'overlay (mais ne show pas).
    /// </summary>
    public void ShowPause(
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData levelData,
        PhasePlanInfo[] phasePlans)
    {
        if (pauseController == null || pauseOverlayUI == null)
        {
            Debug.LogWarning("[LevelPauseFlowHandler] PauseController ou PauseOverlayUI manquant.");
            return;
        }

        if (levelData == null || phasePlans == null)
        {
            Debug.LogWarning("[LevelPauseFlowHandler] Données de niveau manquantes.");
            return;
        }

        // World / Title depuis meta
        string worldName = "";
        string title = "";
        if (levelMeta != null)
        {
            worldName = WorldCatalogService.GetWorldDisplayName(levelMeta.worldId);
            title = levelMeta.title;
        }

        // Tier effectif (SCAN)
        BriefingTier tier = BriefingTier.T0;
        if (runSession != null)
            tier = runSession.GetEffectiveBriefingTier();

        // Run stats
        int runScore = 0;
        int credits = 0;

        if (SaveManager.Instance != null)
        {
            runScore = SaveManager.Instance.GetCurrentRunScore();
            credits = SaveManager.Instance.GetMoney();
        }

        int contractsLeft = (runSession != null) ? runSession.ContractLives : 0;

        // Render (ne show pas)
        pauseOverlayUI.RenderAll(
            levelData,
            phasePlans,
            worldName,
            title,
            tier,
            runScore,
            contractsLeft,
            credits
        );
    }

    // -----------------------------
    // Boutons (câblés Inspector)
    // -----------------------------

    public void OnResumePressed()
    {
        pauseController?.Resume();
    }

    public void OnMenuPressed()
    {
        AudioManager.Instance?.StopDialogTypingLoop();
        AudioManager.Instance?.StopAll();

        pauseController?.Resume();

        if (SaveManager.Instance != null)
            SaveManager.Instance.ApplyAbortPenaltyNow(1);

        BootRoot.GameFlow.GoToTitle();
    }

    public void OnRetryPressed()
    {
        // Revenir à un état propre AVANT de reload (sinon timescale=0 reste)
        pauseController?.Resume();

        if (SaveManager.Instance == null)
            return;

        // Pénalité hull (1 par design)
        SaveManager.Instance.ApplyAbortPenaltyNow(1);

        int hull = SaveManager.Instance.GetRemainingHullInRun();

        if (hull > 0)
        {
            // Relance Main
            BootRoot.GameFlow.RetryLevel();
        }
        else
        {
            // Run morte -> clean + retour menu
            SaveManager.Instance.MarkGameOverInRun();
            BootRoot.GameFlow.GoToTitle();
        }
    }

}
