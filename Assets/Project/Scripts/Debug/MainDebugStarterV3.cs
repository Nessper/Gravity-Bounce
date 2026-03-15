// Chemin recommandé (projet Unity) : Scripts/Debug/MainDebugStarterV3.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainDebugStarterV3
/// ------------------------------------------------------------
/// Injecte un état de run de debug dans la sauvegarde, puis charge
/// directement la scène demandée (RunHub / Main / Credits).
///
/// Objectif : tester rapidement une situation sans repasser par le flow complet.
///
/// Règles de fonctionnement :
/// - Actif uniquement si le PlayerPref de debug est activé.
/// - Une seule injection par session Play pour éviter les doubles initialisations.
/// - Chaque lancement debug repart d'une nouvelle run (ResetRunState).
/// - Le HullMax n'est jamais persisté ici : il reste dérivé du ship plus tard.
/// - Les modules debug sont stockés comme vrais moduleId, puis injectés dans l'équipement de run.
///
/// Note Inspector :
/// - debugWorldId, debugNodeIndex, debugShipId et debugEquippedModuleIds sont cachés.
/// - Ils restent sérialisés pour stocker la config choisie via le Custom Editor.
/// </summary>
[DefaultExecutionOrder(-500)]
public class MainDebugStarterV3 : MonoBehaviour
{
    private static bool s_hasAppliedThisPlaySession;

    private const string DebugPlayerPrefKey = "VS_DEBUG_MAIN";

    private const string RunHubSceneName = "RunHub";
    private const string MainSceneName = "Main";
    private const string CreditsSceneName = "CreditsScene";

    // IMPORTANT :
    // Doit rester aligné avec RunSessionState.equipmentSlotCount.
    private const int RunEquipmentSlotCount = 6;

    // Nombre de slots de pré-équipement debug dans l'inspector.
    private const int DebugModuleLoadoutSlotCount = 3;

    public enum StartDestination
    {
        RunHub,
        Main,
        Credits
    }

    [Header("Start")]
    [SerializeField] private StartDestination startDestination = StartDestination.RunHub;

    [Header("Run target")]
    [HideInInspector]
    [SerializeField] private string debugWorldId = "W1";

    [HideInInspector]
    [SerializeField] private int debugNodeIndex = 0;

    [Header("Ship")]
    [HideInInspector]
    [SerializeField] private string debugShipId = "CORE_SCOUT";

    [Header("Run state overrides")]
    [Tooltip("0 = utilise le hull max du vaisseau. Sinon simule un hull courant déjà endommagé.")]
    [SerializeField] private int debugCurrentHull = 0;

    [SerializeField] private int debugContractLives = 3;
    [SerializeField] private int debugMoney = 0;
    [SerializeField] private int debugRunScore = 0;

    [Header("Debug modules")]
    [SerializeField] private bool debugTreatAllModulesAsOwned = true;

    [HideInInspector]
    [SerializeField] private string[] debugEquippedModuleIds = new string[DebugModuleLoadoutSlotCount];

    private void OnValidate()
    {
        EnsureDebugModuleArrayInitialized();
    }

    private void Awake()
    {
        EnsureDebugModuleArrayInitialized();

        if (!IsDebugActive())
            return;

        if (s_hasAppliedThisPlaySession)
            return;

        s_hasAppliedThisPlaySession = true;

        // Flag global runtime pour l'équipement debug des modules.
        RunSessionState.DebugTreatAllModulesAsOwnedGlobal = debugTreatAllModulesAsOwned;

        Debug.Log("[MainDebugStarterV3] Debug injection active. Destination=" + startDestination);

        TryEnsureShipCatalogLoaded();
        ModuleCatalogService.EnsureLoaded();

        if (!SetupRunStateInSave())
        {
            Debug.LogWarning("[MainDebugStarterV3] Injection aborted (SaveManager missing). Destination scene will NOT be loaded.");
            return;
        }

        LoadDestinationScene();
    }

    private void EnsureDebugModuleArrayInitialized()
    {
        if (debugEquippedModuleIds == null || debugEquippedModuleIds.Length != DebugModuleLoadoutSlotCount)
        {
            string[] resized = new string[DebugModuleLoadoutSlotCount];

            if (debugEquippedModuleIds != null)
            {
                int copyCount = Mathf.Min(debugEquippedModuleIds.Length, resized.Length);
                Array.Copy(debugEquippedModuleIds, resized, copyCount);
            }

            debugEquippedModuleIds = resized;
        }
    }

    private void LoadDestinationScene()
    {
        string target =
            startDestination == StartDestination.RunHub ? RunHubSceneName :
            startDestination == StartDestination.Main ? MainSceneName :
            CreditsSceneName;

        if (string.IsNullOrWhiteSpace(target))
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

    private bool SetupRunStateInSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        SaveManager.Instance.ResetRunState();

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return false;

        string resolvedWorldId = string.IsNullOrWhiteSpace(debugWorldId) ? "W1" : debugWorldId.Trim();
        string resolvedShipId = string.IsNullOrWhiteSpace(debugShipId) ? "CORE_SCOUT" : debugShipId.Trim();

        run.runId = Guid.NewGuid().ToString("N");
        run.hasOngoingRun = true;
        run.worldId = resolvedWorldId;
        run.currentNodeIndex = Mathf.Max(0, debugNodeIndex);
        run.currentShipId = resolvedShipId;

        int shipHullMax = ResolveShipHullMax(resolvedShipId);
        int currentHull = ResolveCurrentHull(shipHullMax);

        SaveManager.Instance.SetRemainingHullInRun(currentHull);
        SaveManager.Instance.SetRemainingContractLives(Mathf.Max(0, debugContractLives));
        SaveManager.Instance.SetCurrentRunScore(Mathf.Max(0, debugRunScore));
        SaveManager.Instance.SetMoney(Mathf.Max(0, debugMoney));

        InjectDebugModulesIntoSave(run);

        SaveManager.Instance.Save();

        Debug.Log(
            "[MainDebugStarterV3] Run injected" +
            " | World=" + run.worldId +
            " | Node=" + run.currentNodeIndex +
            " | Ship=" + run.currentShipId +
            " | Hull=" + currentHull +
            " | ContractLives=" + Mathf.Max(0, debugContractLives) +
            " | Money=" + Mathf.Max(0, debugMoney) +
            " | RunScore=" + Mathf.Max(0, debugRunScore));

        return true;
    }

    private void InjectDebugModulesIntoSave(RunStateData run)
    {
        EnsureDebugModuleArrayInitialized();

        if (run == null || SaveManager.Instance == null)
            return;

        SaveManager.Instance.EnsureEquipmentArrays(RunEquipmentSlotCount);

        if (run.equippedModuleIds == null || run.equippedModuleIds.Length != RunEquipmentSlotCount)
            return;

        for (int i = 0; i < run.equippedModuleIds.Length; i++)
            run.equippedModuleIds[i] = null;

        bool catalogLoaded = ModuleCatalogService.EnsureLoaded();
        int equippedCount = 0;
        HashSet<string> seenFamilies = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < debugEquippedModuleIds.Length; i++)
        {
            string moduleId = string.IsNullOrWhiteSpace(debugEquippedModuleIds[i])
                ? null
                : debugEquippedModuleIds[i].Trim();

            if (string.IsNullOrEmpty(moduleId))
                continue;

            if (!catalogLoaded)
            {
                Debug.LogWarning("[MainDebugStarterV3] Debug module injection skipped: ModuleCatalog not loaded.");
                break;
            }

            ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
            if (def == null)
            {
                Debug.LogWarning("[MainDebugStarterV3] Debug module ignored (unknown moduleId): " + moduleId);
                continue;
            }

            if (!string.IsNullOrEmpty(def.familyId) && !seenFamilies.Add(def.familyId))
            {
                Debug.LogWarning("[MainDebugStarterV3] Debug module ignored (duplicate family): " + moduleId);
                continue;
            }

            run.equippedModuleIds[equippedCount] = moduleId;
            equippedCount++;
        }

        run.unlockedModuleSlotsInRun = Mathf.Max(0, equippedCount);
    }

    private int ResolveShipHullMax(string shipId)
    {
        const int fallbackHullMax = 3;

        if (ShipCatalogService.Catalog == null ||
            ShipCatalogService.Catalog.ships == null ||
            ShipCatalogService.Catalog.ships.Count == 0)
        {
            return fallbackHullMax;
        }

        var ship = ShipCatalogService.Catalog.ships.Find(s => s.id == shipId);
        if (ship == null)
            return fallbackHullMax;

        return Mathf.Max(1, ship.maxHull);
    }

    private int ResolveCurrentHull(int shipHullMax)
    {
        if (debugCurrentHull > 0)
            return Mathf.Max(1, debugCurrentHull);

        return Mathf.Max(1, shipHullMax);
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

    private bool IsDebugActive()
    {
        return PlayerPrefs.GetInt(DebugPlayerPrefKey, 0) == 1;
    }
}