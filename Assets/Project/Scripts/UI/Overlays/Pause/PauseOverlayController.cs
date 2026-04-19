using System;
using UnityEngine;
using VoidScrappers.Briefing;

/// <summary>
/// Controleur complet de l overlay de pause.
///
/// Responsabilites :
/// - toggle pause / resume
/// - freeze / unfreeze le jeu
/// - pause / reprise audio
/// - unlock / relock curseur
/// - masquer / reafficher les controles mobile
/// - binder le contenu mission (level + ship)
/// - gerer les boutons Resume / Retry / Menu
///
/// IMPORTANT :
/// - ce controller ne gere pas la visibilite de l overlay
/// - la visibilite est pilotee par MainOverlaysController
/// - ce controller emet seulement des evenements OnPauseOpened / OnPauseClosed
/// </summary>
public class PauseOverlayController : MonoBehaviour
{
    private const string UiPackName = "ui";
    private const string ScanNoDataKey = "briefing.scan.no_data";

    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Panels")]
    [SerializeField] private RunHubShipStatusController shipStatusController;
    [SerializeField] private LevelBriefingLevelPanelUI levelPanel;

    [Header("Inputs")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Mobile Controls (optionnel)")]
    [SerializeField] private GameObject mobileControlsRoot;

    private Action onRetryCallback;
    private Action onMenuCallback;

    private LevelCatalogService.LevelCatalogEntry currentLevelMeta;
    private LevelData currentLevelData;

    private bool isPaused;
    private bool allowPause = true;
    private bool relockCursorNextLateUpdate;

    public bool IsPaused => isPaused;

    public event Action OnPauseOpened;
    public event Action OnPauseClosed;

    private void Awake()
    {
        if (shipStatusController == null)
        {
            Debug.LogError("[PauseOverlayController] shipStatusController non assigne.");
            enabled = false;
            return;
        }

        if (levelPanel == null)
        {
            Debug.LogError("[PauseOverlayController] levelPanel non assigne.");
            enabled = false;
            return;
        }

        isPaused = false;
        allowPause = true;
        relockCursorNextLateUpdate = false;

        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        if (runSessionState != null)
            runSessionState.OnEquipmentChanged.AddListener(HandleEquipmentChanged);

        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.AddListener(HandleModuleStatsRebuilt);
    }

    private void OnDisable()
    {
        if (runSessionState != null)
            runSessionState.OnEquipmentChanged.RemoveListener(HandleEquipmentChanged);

        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.RemoveListener(HandleModuleStatsRebuilt);

        relockCursorNextLateUpdate = false;
    }

    private void Update()
    {
        if (!allowPause)
            return;

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            TogglePause();
    }

    private void LateUpdate()
    {
        if (!relockCursorNextLateUpdate)
            return;

        relockCursorNextLateUpdate = false;
        CursorController.Lock();
    }

    /// <summary>
    /// Configure le contenu mission et les callbacks d actions.
    /// A appeler avant utilisation.
    /// </summary>
    public void Configure(
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData levelData,
        Action onRetry,
        Action onMenu)
    {
        currentLevelMeta = levelMeta;
        currentLevelData = levelData;
        onRetryCallback = onRetry;
        onMenuCallback = onMenu;

        RebindAll();
    }

    public void EnablePause(bool enabled)
    {
        allowPause = enabled;

        if (!enabled && isPaused)
            Resume();
    }

    public void ForceResume()
    {
        if (isPaused)
            Resume();
    }

    public void TogglePause()
    {
        if (!allowPause)
            return;

        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (isPaused)
            return;

        isPaused = true;
        relockCursorNextLateUpdate = false;

        CursorController.Unlock();

        RebindAll();

        Time.timeScale = 0f;
        AudioManager.Instance?.SetPaused(true);

        if (Application.isMobilePlatform && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(false);

        OnPauseOpened?.Invoke();
    }

    public void Resume()
    {
        if (!isPaused)
            return;

        isPaused = false;

        Time.timeScale = 1f;
        AudioManager.Instance?.SetPaused(false);

        if (Application.isMobilePlatform && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(true);

        relockCursorNextLateUpdate = true;

        OnPauseClosed?.Invoke();
    }

    /// <summary>
    /// A binder dans l Inspector sur le bouton Resume.
    /// </summary>
    public void OnResumeClicked()
    {
        Resume();
    }

    /// <summary>
    /// A binder dans l Inspector sur le bouton Retry.
    /// </summary>
    public void OnRetryClicked()
    {
        ExitPauseStateImmediate();
        onRetryCallback?.Invoke();
    }

    /// <summary>
    /// A binder dans l Inspector sur le bouton Menu.
    /// </summary>
    public void OnMenuClicked()
    {
        ExitPauseStateImmediate();
        onMenuCallback?.Invoke();
    }

    private void HandleEquipmentChanged()
    {
        RebindAll();
    }

    private void HandleModuleStatsRebuilt()
    {
        RebindAll();
    }

    private void RebindAll()
    {
        if (currentLevelData == null)
            return;

        shipStatusController.RefreshUI();

        string scanText = ResolveScanText(currentLevelData);
        levelPanel.Bind(currentLevelMeta, currentLevelData, scanText);
    }

    private string ResolveScanText(LevelData data)
    {
        BriefingTier tier = ResolveBriefingTier();

        if (tier == BriefingTier.T0)
            return GetT0ScanText();

        if (data == null || data.ScanText == null)
            return "scan unavailable";

        switch (tier)
        {
            case BriefingTier.T1:
                if (!string.IsNullOrWhiteSpace(data.ScanText.T1))
                    return data.ScanText.T1;
                break;

            case BriefingTier.T2:
                if (!string.IsNullOrWhiteSpace(data.ScanText.T2))
                    return data.ScanText.T2;

                if (!string.IsNullOrWhiteSpace(data.ScanText.T1))
                    return data.ScanText.T1;
                break;

            case BriefingTier.T3:
            default:
                if (!string.IsNullOrWhiteSpace(data.ScanText.T3))
                    return data.ScanText.T3;

                if (!string.IsNullOrWhiteSpace(data.ScanText.T2))
                    return data.ScanText.T2;

                if (!string.IsNullOrWhiteSpace(data.ScanText.T1))
                    return data.ScanText.T1;
                break;
        }

        return "scan unavailable";
    }

    private BriefingTier ResolveBriefingTier()
    {
        if (ModuleRuntimeStats.Instance == null)
            return BriefingTier.T0;

        return ModuleRuntimeStats.Instance.GetEffectiveBriefingTier();
    }

    private string GetT0ScanText()
    {
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            return LocalizationManager.Instance.GetTextOrKey(
                UiPackName,
                ScanNoDataKey
            );
        }

        return "AUCUNE DONNEE DE SCAN";
    }

    private void ExitPauseStateImmediate()
    {
        if (!isPaused)
            return;

        isPaused = false;
        relockCursorNextLateUpdate = false;

        Time.timeScale = 1f;
        AudioManager.Instance?.SetPaused(false);

        if (Application.isMobilePlatform && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(true);

        OnPauseClosed?.Invoke();
    }
}