using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Debug injector: writes a run into the save then loads RunHub/Main/Credits.
/// Goal: fast testing without going through the full flow.
///
/// Safety:
/// - Only active when debug flag is enabled (PlayerPrefs key).
/// - DEBUG RULE: each play session = NEW GAME (ResetRunState).
/// - Does not persist HullMax (derived later by RunSessionState).
///
/// Credits:
/// - Can start directly on CreditsScene for quick UI tests.
/// </summary>
[DefaultExecutionOrder(-500)]
public class MainDebugStarterV3 : MonoBehaviour
{
    private static bool s_hasAppliedThisPlaySession;

    public enum StartDestination
    {
        RunHub,
        Main,
        Credits
    }

    [Header("Debug mode")]
    [SerializeField] private bool debugEnabled = true;
    [SerializeField] private bool allowDebugInPlayer = false;
    [SerializeField] private string playerPrefKeyDebug = "VS_DEBUG_MAIN";

    [Header("Start")]
    [SerializeField] private StartDestination startDestination = StartDestination.RunHub;
    [SerializeField] private string runHubSceneName = "RunHub";
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string creditsSceneName = "CreditsScene";

    [Header("Run identity")]
    [SerializeField] private string debugWorldId = "W1";
    [SerializeField] private int debugNodeIndex = 0;

    [Header("Ship")]
    [SerializeField] private string debugShipId = "CORE_SCOUT";

    [Header("Run state overrides")]
    [Tooltip("0 = default ship max hull. Otherwise simulates current hull after damage.")]
    [SerializeField] private int debugCurrentHull = 0;

    [SerializeField] private int debugContractLives = 3;
    [SerializeField] private int debugMoney = 0;
    [SerializeField] private int debugRunScore = 0;

    [Header("Optional")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private MainDebugSkipsApplier debugSkipsApplier;
    [SerializeField] private bool debugSkipBriefing = true;
    [SerializeField] private bool debugSkipIntro = true;

    [Header("Dialogs debug")]
    [SerializeField] private bool enableDialogsInDebug = true;
    [SerializeField] private DialogManager dialogManagerPrefab;

    private void Awake()
    {
        if (!IsDebugActive())
            return;

        if (s_hasAppliedThisPlaySession)
            return;

        s_hasAppliedThisPlaySession = true;

        Debug.Log("[MainDebugStarterV3] Debug injection active. Destination=" + startDestination);

        TryEnsureShipCatalogLoaded();
        ApplyRunConfigShip();

        if (!SetupRunStateInSave())
        {
            Debug.LogWarning("[MainDebugStarterV3] Injection aborted (SaveManager missing). Destination scene will NOT be loaded.");
            return;
        }

        if (runSessionState != null)
        {
            bool ok = runSessionState.LoadFromSave();
            Debug.Log("[MainDebugStarterV3] RunSessionState.LoadFromSave => " + (ok ? "OK" : "FAIL"));
        }

        EnsureDialogManagerForDebug();

        if (debugSkipsApplier != null)
            debugSkipsApplier.ApplySkips(debugSkipBriefing, debugSkipIntro);

        LoadDestinationScene();
    }

    /// <summary>
    /// Loads the requested destination scene.
    /// </summary>
    private void LoadDestinationScene()
    {
        string target =
            (startDestination == StartDestination.RunHub) ? runHubSceneName :
            (startDestination == StartDestination.Main) ? mainSceneName :
            creditsSceneName;

        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[MainDebugStarterV3] Destination scene name is empty. No scene will be loaded.");
            return;
        }

        if (SceneManager.GetActiveScene().name == target)
        {
            Debug.Log("[MainDebugStarterV3] Already in destination scene: " + target);
            return;
        }

        Debug.Log("[MainDebugStarterV3] Loading scene: " + target);
        SceneManager.LoadScene(target, LoadSceneMode.Single);
    }

    /// <summary>
    /// Writes the debug run into the save. Returns false if SaveManager is unavailable.
    /// </summary>
    private bool SetupRunStateInSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        // DEBUG RULE: each play = NEW GAME
        SaveManager.Instance.ResetRunState();

        RunStateData run = SaveManager.Instance.GetRunState();

        run.runId = Guid.NewGuid().ToString("N");
        run.hasOngoingRun = true;

        run.worldId = string.IsNullOrEmpty(debugWorldId) ? "W1" : debugWorldId;
        run.currentNodeIndex = Mathf.Max(0, debugNodeIndex);
        run.currentShipId = string.IsNullOrEmpty(debugShipId) ? "CORE_SCOUT" : debugShipId;

        int shipHullMax = ResolveShipHullMax();
        int currentHull = ResolveCurrentHull(shipHullMax);

        SaveManager.Instance.SetRemainingHullInRun(currentHull);
        SaveManager.Instance.SetRemainingContractLives(Mathf.Max(0, debugContractLives));
        SaveManager.Instance.SetCurrentRunScore(Mathf.Max(0, debugRunScore));
        SaveManager.Instance.SetMoney(Mathf.Max(0, debugMoney));

        SaveManager.Instance.Save();
        return true;
    }

    private int ResolveShipHullMax()
    {
        int fallback = 3;

        if (ShipCatalogService.Catalog == null ||
            ShipCatalogService.Catalog.ships == null ||
            ShipCatalogService.Catalog.ships.Count == 0)
        {
            return fallback;
        }

        string shipId = string.IsNullOrEmpty(debugShipId) ? "CORE_SCOUT" : debugShipId;
        var ship = ShipCatalogService.Catalog.ships.Find(s => s.id == shipId);
        if (ship == null)
            return fallback;

        return Mathf.Max(1, ship.maxHull);
    }

    private int ResolveCurrentHull(int shipHullMax)
    {
        if (debugCurrentHull > 0)
            return Mathf.Max(1, debugCurrentHull);

        return Mathf.Max(1, shipHullMax);
    }

    private void ApplyRunConfigShip()
    {
        var runConfig = RunConfig.Instance;
        if (runConfig == null)
            return;

        if (string.IsNullOrEmpty(debugShipId))
            return;

        runConfig.SetSelectedShip(debugShipId);
    }

    private void TryEnsureShipCatalogLoaded()
    {
        if (ShipCatalogService.Catalog != null &&
            ShipCatalogService.Catalog.ships != null &&
            ShipCatalogService.Catalog.ships.Count > 0)
        {
            return;
        }

        TextAsset jsonAsset = Resources.Load<TextAsset>("Ships/ShipCatalog");
        if (jsonAsset == null)
        {
            Debug.LogWarning("[MainDebugStarterV3] ShipCatalog missing at Resources/Ships/ShipCatalog.");
            return;
        }

        try
        {
            var catalog = JsonUtility.FromJson<ShipCatalog>(jsonAsset.text);
            if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
            {
                Debug.LogWarning("[MainDebugStarterV3] ShipCatalog loaded but empty/invalid.");
                return;
            }

            ShipCatalogService.Catalog = catalog;
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainDebugStarterV3] ShipCatalog load exception: " + ex.Message);
        }
    }

    private void EnsureDialogManagerForDebug()
    {
        if (!enableDialogsInDebug)
            return;

        var existing = FindFirstObjectByType<DialogManager>();
        if (existing != null)
            return;

        if (dialogManagerPrefab == null)
            return;

        var dm = Instantiate(dialogManagerPrefab);
        dm.name = "[Debug] DialogManager";
    }

    private bool IsDebugActive()
    {
#if UNITY_EDITOR
        if (!debugEnabled) return false;
        return PlayerPrefs.GetInt(playerPrefKeyDebug, 0) == 1;
#else
        if (!debugEnabled) return false;
        if (!allowDebugInPlayer) return false;
        return PlayerPrefs.GetInt(playerPrefKeyDebug, 0) == 1;
#endif
    }
}