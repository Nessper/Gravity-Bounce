// Chemin recommandé : Scripts/Systems/Pause/LevelPauseFlowHandler.cs

using UnityEngine;
using VoidScrappers.Briefing;

/// <summary>
/// Gère le contenu et les actions de l'overlay Pause pendant un niveau.
///
/// Responsabilités :
/// - Assembler les données affichées (world/title, data, phases, tier, score, contracts, credits).
/// - Gérer les actions Menu / Retry / Resume (appelées par les boutons via l'Inspector).
///
/// Important :
/// - Ne touche pas Time.timeScale (c'est le rôle de PauseController).
/// - Ne montre pas l'overlay (c'est aussi le rôle de PauseController).
/// - Le tier de briefing effectif vient désormais de ModuleRuntimeStats.
/// </summary>
public class LevelPauseFlowHandler : MonoBehaviour
{
    [Header("Dépendances")]
    [SerializeField] private PauseController pauseController;
    [SerializeField] private PauseOverlayUI pauseOverlayUI;
    [SerializeField] private RunSessionState runSession;

    /// <summary>
    /// Appelé par LevelManager quand la pause s'ouvre.
    /// Rend le contenu de l'overlay, sans l'afficher.
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

        // World / Title depuis les métadonnées
        string worldName = "";
        string title = "";

        if (levelMeta != null)
        {
            worldName = WorldCatalogService.GetWorldDisplayName(levelMeta.worldId);
            title = levelMeta.title;
        }

        // Tier effectif (SCAN) via ModuleRuntimeStats
        BriefingTier tier = BriefingTier.T0;
        if (ModuleRuntimeStats.Instance != null)
            tier = ModuleRuntimeStats.Instance.GetEffectiveBriefingTier();

        // Stats de run
        int runScore = 0;
        int credits = 0;

        if (SaveManager.Instance != null)
        {
            runScore = SaveManager.Instance.GetCurrentRunScore();
            credits = SaveManager.Instance.GetMoney();
        }

        int contractsLeft = (runSession != null) ? runSession.ContractLives : 0;

        // Rend le contenu (sans show)
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

    // ------------------------------------------------------------
    // Boutons (câblés dans l'Inspector)
    // ------------------------------------------------------------

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
        // Revenir à un état propre avant reload
        // (sinon le Time.timeScale = 0 peut rester actif)
        pauseController?.Resume();

        if (SaveManager.Instance == null)
            return;

        // Pénalité Hull (1 par design)
        SaveManager.Instance.ApplyAbortPenaltyNow(1);

        int hull = SaveManager.Instance.GetRemainingHullInRun();

        if (hull > 0)
        {
            // Relance la scène Main
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