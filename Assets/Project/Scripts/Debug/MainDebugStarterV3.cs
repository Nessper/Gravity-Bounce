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
/// Règle importante :
/// - la création de la run passe d abord par NewRunInitializer
/// - puis seulement les overrides debug sont appliqués
///
/// Objectif : garder UNE source de vérité pour l init d une nouvelle run.
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

    // Nombre de slots de pré-équipement debug dans l inspector.
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
    [Tooltip("0 = utilise le hull max du vaisseau. Sinon simule un hull courant deja endommage.")]
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

        RunSessionState.DebugTreatAllModulesAsOwnedGlobal = debugTreatAllModulesAsOwned;

        Debug.Log("[MainDebugStarterV3] Debug injection active. Destination=" + startDestination);

        TryEnsureShipCatalogLoaded();
        ModuleCatalogService.EnsureLoaded();

        if (!SetupRunStateInSave())
        {
            Debug.LogWarning("[MainDebugStarterV3] Injection aborted (SaveManager missing or init failed). Destination scene will NOT be loaded.");
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

        GameSaveData save = SaveManager.Instance.Current;

        string resolvedWorldId = string.IsNullOrWhiteSpace(debugWorldId) ? "W1" : debugWorldId.Trim();
        string resolvedShipId = string.IsNullOrWhiteSpace(debugShipId) ? "CORE_SCOUT" : debugShipId.Trim();

        ShipDefinition ship = ResolveShipDefinition(resolvedShipId);
        if (ship == null)
        {
            Debug.LogError("[MainDebugStarterV3] Ship introuvable: " + resolvedShipId);
            return false;
        }

        // Base commune : toute nouvelle run passe par le meme init que le vrai jeu.
        bool ok = NewRunInitializer.Initialize(save, ship);
        if (!ok)
            return false;

        RunStateData run = save.runState;
        if (run == null)
            return false;

        // ------------------------------------------------------------
        // OVERRIDES DEBUG
        // ------------------------------------------------------------
        run.worldId = resolvedWorldId;
        run.currentNodeIndex = Mathf.Max(0, debugNodeIndex);

        int shipHullMax = ResolveShipHullMax(ship);
        int currentHull = ResolveCurrentHull(shipHullMax);

        SaveManager.Instance.SetRemainingHullInRun(currentHull);
        SaveManager.Instance.SetRemainingContractLives(Mathf.Max(0, debugContractLives));
        SaveManager.Instance.SetCurrentRunScore(Mathf.Max(0, debugRunScore));
        SaveManager.Instance.SetMoney(Mathf.Max(0, debugMoney));

        // Si un loadout debug est renseigne, il remplace l equipement de depart du ship.
        ApplyDebugEquipmentOverride(run);

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

        Debug.Log("[MainDebugStarterV3] Equipped=" + string.Join(", ", run.equippedModuleIds ?? Array.Empty<string>()));
        Debug.Log("[MainDebugStarterV3] Owned=" + string.Join(", ", run.ownedModuleIdsInRun ?? new List<string>()));

        return true;
    }

    private void ApplyDebugEquipmentOverride(RunStateData run)
    {
        EnsureDebugModuleArrayInitialized();

        if (run == null)
            return;

        if (!HasAnyDebugModuleOverride())
            return;

        if (!ModuleCatalogService.EnsureLoaded())
        {
            Debug.LogWarning("[MainDebugStarterV3] Debug module override skipped: ModuleCatalog not loaded.");
            return;
        }

        if (run.equippedModuleIds == null || run.equippedModuleIds.Length != RunEquipmentSlotCount)
            run.equippedModuleIds = new string[RunEquipmentSlotCount];

        for (int i = 0; i < run.equippedModuleIds.Length; i++)
            run.equippedModuleIds[i] = null;

        HashSet<string> seenFamilies = new HashSet<string>(StringComparer.Ordinal);
        int equippedCount = 0;

        for (int i = 0; i < debugEquippedModuleIds.Length; i++)
        {
            string moduleId = string.IsNullOrWhiteSpace(debugEquippedModuleIds[i])
                ? null
                : debugEquippedModuleIds[i].Trim();

            if (string.IsNullOrEmpty(moduleId))
                continue;

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

            if (equippedCount >= run.equippedModuleIds.Length)
                break;

            run.equippedModuleIds[equippedCount] = moduleId;
            equippedCount++;
        }

        run.unlockedModuleSlotsInRun = Mathf.Max(run.unlockedModuleSlotsInRun, equippedCount);

        // Owned recalculé a partir du nouvel equipement debug.
        run.ownedModuleIdsInRun = BuildOwnedModulesFromEquipped(run.equippedModuleIds);
    }

    private bool HasAnyDebugModuleOverride()
    {
        EnsureDebugModuleArrayInitialized();

        for (int i = 0; i < debugEquippedModuleIds.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(debugEquippedModuleIds[i]))
                return true;
        }

        return false;
    }

    private List<string> BuildOwnedModulesFromEquipped(string[] equippedModuleIds)
    {
        HashSet<string> ownedIds = new HashSet<string>(StringComparer.Ordinal);

        if (equippedModuleIds == null || equippedModuleIds.Length == 0)
            return new List<string>();

        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string moduleId = string.IsNullOrWhiteSpace(equippedModuleIds[i])
                ? null
                : equippedModuleIds[i].Trim();

            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition equippedDef = ModuleCatalogService.GetById(moduleId);
            if (equippedDef == null)
                continue;

            ownedIds.Add(equippedDef.id);

            if (string.IsNullOrWhiteSpace(equippedDef.familyId))
                continue;

            List<ModuleDefinition> familyModules = ModuleCatalogService.GetModulesByFamily(equippedDef.familyId);
            for (int j = 0; j < familyModules.Count; j++)
            {
                ModuleDefinition familyDef = familyModules[j];
                if (familyDef == null)
                    continue;

                if (familyDef.tier <= equippedDef.tier)
                    ownedIds.Add(familyDef.id);
            }
        }

        return new List<string>(ownedIds);
    }

    private ShipDefinition ResolveShipDefinition(string shipId)
    {
        if (ShipCatalogService.Catalog == null ||
            ShipCatalogService.Catalog.ships == null ||
            ShipCatalogService.Catalog.ships.Count == 0)
        {
            return null;
        }

        return ShipCatalogService.Catalog.ships.Find(s => s != null && s.id == shipId);
    }

    private int ResolveShipHullMax(ShipDefinition ship)
    {
        if (ship == null)
            return 3;

        return Mathf.Max(1, ship.baseHull);
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
            ShipCatalog catalog = JsonUtility.FromJson<ShipCatalog>(jsonAsset.text);
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