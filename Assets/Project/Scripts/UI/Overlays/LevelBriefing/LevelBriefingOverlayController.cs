using System;
using UnityEngine;
using VoidScrappers.Briefing;

/// <summary>
/// Controle l etat logique et le contenu de l overlay de briefing.
///
/// Responsabilites :
/// - binder les donnees de niveau
/// - rafraichir le bloc Ship Status
/// - binder le bloc Level Info
/// - stocker et executer les callbacks des boutons Menu / Next
/// - rester synchronise avec l etat runtime (equipement / stats modules)
///
/// IMPORTANT :
/// - ce script ne gere plus la visibilite ni les fades
/// - la visibilite est pilotee par MainOverlaysController
/// - les boutons sont cables dans l Inspector Unity
/// </summary>
public class LevelBriefingOverlayController : MonoBehaviour
{
    private const string UiPackName = "ui";
    private const string ScanNoDataKey = "briefing.scan.no_data";

    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Panels")]
    [SerializeField] private RunHubShipStatusController shipStatusController;
    [SerializeField] private LevelBriefingLevelPanelUI levelPanel;

    private Action onMenuCallback;
    private Action onNextCallback;

    private LevelCatalogService.LevelCatalogEntry currentLevelMeta;
    private LevelData currentLevelData;

    private void Awake()
    {
        if (shipStatusController == null)
        {
            Debug.LogError("[LevelBriefingOverlayController] shipStatusController non assigne.");
            enabled = false;
            return;
        }

        if (levelPanel == null)
        {
            Debug.LogError("[LevelBriefingOverlayController] levelPanel non assigne.");
            enabled = false;
            return;
        }
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
    }

    /// <summary>
    /// Prepare et bind le contenu du briefing.
    /// Ne touche pas a la visibilite.
    /// </summary>
    public void Show(
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData data,
        Action onMenu,
        Action onNext)
    {
        currentLevelMeta = levelMeta;
        currentLevelData = data;

        onMenuCallback = onMenu;
        onNextCallback = onNext;

        RebindAll();
    }


    /// <summary>
    /// A binder dans l Inspector sur le bouton Menu.
    /// </summary>
    public void OnMenuClicked()
    {
        onMenuCallback?.Invoke();
    }

    /// <summary>
    /// A binder dans l Inspector sur le bouton Next.
    /// </summary>
    public void OnNextClicked()
    {
        onNextCallback?.Invoke();
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

        return "No scan data available.";
    }
}