using UnityEngine;

/// <summary>
/// Outil de debug pour lancer directement la scene Main.
/// - Ne s active que si BootRoot.GameFlow est null (scene Main lancee seule),
///   sauf si ignoreGameFlowPresence est true.
/// - Desactive RunSessionBootstrapper pour ne pas retourner au Title.
/// - Force une run minimale DANS LA SAVE (source de verite):
///   SaveManager.runState.worldId + currentNodeIndex + currentShipId + ressources.
/// - Laisse RunSessionState.LoadFromSave() reconstruire le RunPlan depuis WorldCatalog.
/// - Applique les skips via controllers (pas via LevelManager).
/// </summary>
[DefaultExecutionOrder(-500)]
public class MainDebugStarter : MonoBehaviour
{
    [Header("Debug mode")]
    [SerializeField] private bool debugEnabled = true;

    [Header("Compat GameFlow")]
    [SerializeField] private bool ignoreGameFlowPresence = true;

    [Header("Debug sur device")]
    [SerializeField] private bool allowDebugInPlayer = false;

    [SerializeField] private MainDebugSkipsApplier debugSkipsApplier;

    private const string PlayerPrefKey_DebugMain = "VS_DEBUG_MAIN";

    [Header("Vaisseau")]
    [SerializeField] private string debugShipId = "CORE_SCOUT";
    [SerializeField] private int defaultContractLives = 3;

    [Header("Niveau (levelId)")]
    [SerializeField] private string debugLevelId = "W1-L1";

    [Header("Skip sequences")]
    [SerializeField] private bool debugSkipBriefing = true;
    [SerializeField] private bool debugSkipIntro = true;

    [Header("Refs")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private RunSessionBootstrapper runSessionBootstrapper;

    [Header("Dialogs debug")]
    [SerializeField] private bool enableDialogsInDebug = true;
    [SerializeField] private DialogManager dialogManagerPrefab;

    private void Awake()
    {
        if (!IsDebugActive())
            return;

        if (BootRoot.GameFlow != null && !ignoreGameFlowPresence)
            return;

        Debug.Log("[MainDebugStarter] Debug mode actif pour la scene Main (BootRoot.GameFlow="
                  + (BootRoot.GameFlow != null ? "present" : "null") + ").");

        // 1) Catalog ship dispo (pour hullValue)
        TryEnsureShipCatalogLoaded();

  

        // 3) Setup vaisseau dans RunConfig (si present)
        var runConfig = RunConfig.Instance;
        if (runConfig != null && !string.IsNullOrEmpty(debugShipId))
        {
            runConfig.SetSelectedShip(debugShipId);
            Debug.Log("[MainDebugStarter] RunConfig ship id = " + debugShipId);
        }

        // 4) Debug run: ECRITURE DANS LA SAVE (source de verite)
        SetupRunStateInSave();

        // 5) Charger le runtime depuis la save (rebuild plan via WorldCatalog)
        if (runSessionState != null)
        {
            bool ok = runSessionState.LoadFromSave();
            Debug.Log("[MainDebugStarter] RunSessionState.LoadFromSave => " + (ok ? "OK" : "FAIL"));
        }
        else
        {
            Debug.LogWarning("[MainDebugStarter] RunSessionState non assigne (runtime non charge).");
        }

        // 6) Dialogs debug
        EnsureDialogManagerForDebug();

        // 7) Skips (briefing/intro)
        if (debugSkipsApplier != null)
        {
            debugSkipsApplier.ApplySkips(debugSkipBriefing, debugSkipIntro);
        }
        else
        {
            Debug.LogWarning("[MainDebugStarter] debugSkipsApplier manquant. Les skips briefing/intro ne seront pas appliques.");
        }
    }

    /// <summary>
    /// Ecrit une run minimale dans SaveManager.runState.
    /// Convention: worldId + currentNodeIndex = source de verite.
    /// </summary>
    private void SetupRunStateInSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogWarning("[MainDebugStarter] SaveManager absent: impossible d init la run debug.");
            return;
        }

        // Hull: derive du ship si possible
        int hullValue = ResolveDebugHull();

        // WorldId: derive de debugLevelId si possible ("W1-Lx" => "W1"), sinon fallback "W1"
        string resolvedWorldId = ResolveWorldIdFromLevelId(debugLevelId);

        // NodeIndex: on tente de trouver l index du levelId dans WorldCatalog
        int resolvedNodeIndex = ResolveNodeIndexFromWorld(resolvedWorldId, debugLevelId);

        RunStateData run = SaveManager.Instance.GetRunState();
        run.hasOngoingRun = true;

        run.worldId = string.IsNullOrEmpty(resolvedWorldId) ? "W1" : resolvedWorldId;
        run.currentNodeIndex = Mathf.Max(0, resolvedNodeIndex);

        // Ship gelé pour la run
        run.currentShipId = string.IsNullOrEmpty(debugShipId) ? "CORE_SCOUT" : debugShipId;

        // Ressources de run (persistées)
        SaveManager.Instance.SetRemainingHullInRun(hullValue);
        SaveManager.Instance.SetRemainingContractLives(Mathf.Max(0, defaultContractLives));
        SaveManager.Instance.SetCurrentRunScore(0);

        // Money: on ne force pas ici (meta), mais tu peux si tu veux
        // SaveManager.Instance.SetMoney(...);

        SaveManager.Instance.Save();

        Debug.Log("[MainDebugStarter] RunState DEBUG ecrit dans la save: worldId=" + run.worldId
                  + " nodeIndex=" + run.currentNodeIndex
                  + " shipId=" + run.currentShipId
                  + " hull=" + hullValue
                  + " contractLives=" + defaultContractLives
                  + " levelId=" + debugLevelId);
    }

    private int ResolveDebugHull()
    {
        int hullValue = 3;

        if (ShipCatalogService.Catalog != null &&
            ShipCatalogService.Catalog.ships != null &&
            ShipCatalogService.Catalog.ships.Count > 0)
        {
            string shipId = string.IsNullOrEmpty(debugShipId) ? "CORE_SCOUT" : debugShipId;
            var ship = ShipCatalogService.Catalog.ships.Find(s => s.id == shipId);
            if (ship != null)
            {
                hullValue = Mathf.Max(1, ship.maxHull);
            }
            else
            {
                Debug.LogWarning("[MainDebugStarter] Vaisseau introuvable dans le catalog pour id=" + shipId + ", hull par defaut = " + hullValue);
            }
        }
        else
        {
            Debug.LogWarning("[MainDebugStarter] ShipCatalog non disponible, hull par defaut = " + hullValue);
        }

        return hullValue;
    }

    private string ResolveWorldIdFromLevelId(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return "W1";

        // Convention: "W1-L3" => "W1"
        int dash = levelId.IndexOf("-");
        if (dash > 0)
            return levelId.Substring(0, dash);

        // Fallback
        return "W1";
    }

    private int ResolveNodeIndexFromWorld(string worldId, string levelId)
    {
        if (string.IsNullOrEmpty(worldId) || string.IsNullOrEmpty(levelId))
            return 0;

        WorldCatalogService.WorldEntry world;
        if (!WorldCatalogService.TryGetWorld(worldId, out world) || world.levelIds == null || world.levelIds.Length == 0)
        {
            Debug.LogWarning("[MainDebugStarter] WorldCatalog introuvable/vide pour worldId=" + worldId + ". nodeIndex=0");
            return 0;
        }

        for (int i = 0; i < world.levelIds.Length; i++)
        {
            if (world.levelIds[i] == levelId)
                return i;
        }

        Debug.LogWarning("[MainDebugStarter] levelId=" + levelId + " non trouve dans worldId=" + worldId + ". nodeIndex=0");
        return 0;
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
            Debug.LogWarning("[MainDebugStarter] ShipCatalog introuvable dans Resources/Ships/ShipCatalog.");
            return;
        }

        try
        {
            var catalog = JsonUtility.FromJson<ShipCatalog>(jsonAsset.text);

            if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
            {
                Debug.LogWarning("[MainDebugStarter] ShipCatalog charge mais vide ou invalide.");
                return;
            }

            ShipCatalogService.Catalog = catalog;
            Debug.Log("[MainDebugStarter] ShipCatalog charge en debug (" + catalog.ships.Count + " vaisseaux).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[MainDebugStarter] Exception lors du chargement du ShipCatalog: " + ex.Message);
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
        {
            Debug.LogWarning("[MainDebugStarter] Dialog debug actif mais aucun prefab de DialogManager n est assigne.");
            return;
        }

        var dm = Instantiate(dialogManagerPrefab);
        dm.name = "[Debug] DialogManager";
    }

    private bool IsDebugActive()
    {
#if UNITY_EDITOR
        return debugEnabled;
#else
        if (!allowDebugInPlayer)
            return false;

        return PlayerPrefs.GetInt(PlayerPrefKey_DebugMain, 0) == 1;
#endif
    }
}
