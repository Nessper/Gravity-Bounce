using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VoidScrappers.Briefing;

[CreateAssetMenu(fileName = "RunSessionState", menuName = "Game/Run Session State")]
public class RunSessionState : ScriptableObject
{
    // ------------------------------------------------------------
    // NODES / PLAN (source de verite: SaveManager.runState worldId + currentNodeIndex)
    // ------------------------------------------------------------

    [Header("Nodes / Plan (runtime)")]
    [SerializeField] private RunPlan currentRunPlan;

    [SerializeField] private string worldId = "W1";
    [SerializeField] private int currentNodeIndex = 0;

    public UnityEvent OnNodeChanged = new UnityEvent();

    public string WorldId => worldId;
    public int CurrentNodeIndex => currentNodeIndex;
    public RunPlan CurrentRunPlan => currentRunPlan;

    public int NodeCount
    {
        get
        {
            if (currentRunPlan == null || !currentRunPlan.HasNodes)
                return 0;
            return currentRunPlan.NodeCount;
        }
    }

    public bool HasPlayableNode
    {
        get
        {
            if (currentRunPlan == null || !currentRunPlan.HasNodes)
                return false;
            return currentRunPlan.IsPlayableIndex;
        }
    }

    public bool IsRunCompleted
    {
        get
        {
            if (currentRunPlan == null || !currentRunPlan.HasNodes)
                return false;
            return currentRunPlan.IsCompleted;
        }
    }

    public RunNode CurrentPlayableNode
    {
        get
        {
            if (currentRunPlan == null || !currentRunPlan.HasNodes)
                return null;

            if (currentNodeIndex < 0 || currentNodeIndex >= currentRunPlan.nodes.Count)
                return null;

            return currentRunPlan.nodes[currentNodeIndex];
        }
    }

    // ------------------------------------------------------------
    // SHIP (source de verite: SaveManager.runState.currentShipId)
    // ------------------------------------------------------------

    [Header("Ship")]
    [SerializeField] private string shipId = "CORE_SCOUT";
    public UnityEvent<string> OnShipChanged = new UnityEvent<string>();
    public string ShipId => shipId;

    // ------------------------------------------------------------
    // RESSOURCES RUN (runtime mirror de la save)
    // ------------------------------------------------------------

    [Header("Hull")]
    [SerializeField] private int hull;
    public UnityEvent<int> OnHullChanged = new UnityEvent<int>();
    public int Hull => hull;

    [SerializeField] private int hullMax;
    public UnityEvent<int> OnHullMaxChanged = new UnityEvent<int>();
    public int HullMax => hullMax;

    [Header("Contract Strikes")]
    [SerializeField] private int contractLives;
    public UnityEvent<int> OnContractLivesChanged = new UnityEvent<int>();
    public int ContractLives => contractLives;

    [Header("Run Score")]
    [SerializeField] private int runScore;
    public UnityEvent<int> OnRunScoreChanged = new UnityEvent<int>();
    public int RunScore => runScore;

    [Header("Money")]
    [SerializeField] private int money;
    public UnityEvent<int> OnMoneyChanged = new UnityEvent<int>();
    public int Money => money;

    // ------------------------------------------------------------
    // EQUIPMENT
    // ------------------------------------------------------------

    [Header("Equipment")]
    [SerializeField] private int equipmentSlotCount = 6;

    [SerializeField] private string[] equippedModuleIds;

    public UnityEvent OnEquipmentChanged = new UnityEvent();

    public int EquipmentSlotCount => Mathf.Max(0, equipmentSlotCount);


    /// <summary>
    /// Flag global de debug : si vrai, tous les modules sont considérés comme owned
    /// pour les règles d'équipement runtime.
    /// 
    /// Important :
    /// - Runtime only
    /// - Non persisté en save
    /// - Piloté par MainDebugStarterV3
    /// </summary>
    public static bool DebugTreatAllModulesAsOwnedGlobal = false;

    // ------------------------------------------------------------
    // DIAGNOSTIC (TUNING)
    // ------------------------------------------------------------

    public enum EquipFailReason
    {
        None = 0,
        InvalidModule = 1,
        SlotLocked = 2,
        NotOwned = 3,
        MissingPrerequisite = 4
    }

    // ------------------------------------------------------------
    // FLOW API
    // ------------------------------------------------------------

    public bool LoadFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogWarning("[RunSessionState] SaveManager absent. LoadFromSave impossible.");
            return false;
        }

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
        {
            Debug.LogWarning("[RunSessionState] runState null. LoadFromSave impossible.");
            return false;
        }

        worldId = string.IsNullOrEmpty(run.worldId) ? "W1" : run.worldId;
        currentNodeIndex = Mathf.Max(0, run.currentNodeIndex);
        Debug.Log($"[RunSessionState] LoadFromSave worldId={worldId} currentNodeIndex={currentNodeIndex} hasOngoingRun={run.hasOngoingRun} runId={run.runId}");

        shipId = string.IsNullOrEmpty(run.currentShipId) ? "CORE_SCOUT" : run.currentShipId;

        hull = Mathf.Max(0, run.remainingHullInRun);
        contractLives = Mathf.Max(0, run.remainingContractLives);
        runScore = Mathf.Max(0, run.currentRunScore);
        money = Mathf.Max(0, SaveManager.Instance.GetMoney());

        EnsureUnlockedSlotsInRunInitialized();
        PullEquipmentFromSave();

        // Défensif: purge modules invalides / non possédés / tiers non respectés / doublons famille
        SanitizeEquippedModulesRuntime();

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        if (!EnsurePlanLoaded())
            return false;

        RaiseAllChangedEvents();
        return true;
    }

    public bool StartNewRun(string newWorldId, string initialShipId, int initialHull, int initialContractLives, int initialMoney, int initialRunScore)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogWarning("[RunSessionState] SaveManager absent. StartNewRun impossible.");
            return false;
        }

        worldId = string.IsNullOrEmpty(newWorldId) ? "W1" : newWorldId;
        currentNodeIndex = 0;

        shipId = string.IsNullOrEmpty(initialShipId) ? "CORE_SCOUT" : initialShipId;

        contractLives = Mathf.Max(0, initialContractLives);
        money = Mathf.Max(0, initialMoney);
        runScore = Mathf.Max(0, initialRunScore);

        ResetUnlockedSlotsInRunToShipBase();
        ResetEquipmentEmpty();
        PushEquipmentToSave();

        hull = Mathf.Max(0, initialHull);

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        hull = Mathf.Clamp(hull, 0, hullMax);

        SaveManager.Instance.SetRemainingContractLives(contractLives);
        SaveManager.Instance.SetCurrentRunScore(runScore);
        SaveManager.Instance.SetMoney(money);

        PersistProgressAndShip();

        if (!EnsurePlanLoaded())
            return false;

        RaiseAllChangedEvents();
        return true;
    }

    // ------------------------------------------------------------
    // PLAN / NODES API
    // ------------------------------------------------------------

    public bool EnsurePlanLoaded()
    {
        if (currentRunPlan != null &&
            currentRunPlan.HasNodes &&
            string.Equals(currentRunPlan.worldId, worldId, StringComparison.Ordinal))
        {
            int countCached = currentRunPlan.NodeCount;
            int clampedCached = Mathf.Clamp(currentNodeIndex, 0, Mathf.Max(0, countCached - 1));
            currentNodeIndex = clampedCached;
            currentRunPlan.currentIndex = clampedCached;
            return true;
        }

        RunPlan plan = RunPlanBuilder.BuildFromWorld(worldId);
        if (plan == null || !plan.HasNodes)
        {
            Debug.LogError("[RunSessionState] RunPlan invalide pour worldId=" + worldId);
            currentRunPlan = null;
            return false;
        }

        currentRunPlan = plan;

        int count = currentRunPlan.NodeCount;
        int clamped = Mathf.Clamp(currentNodeIndex, 0, Mathf.Max(0, count - 1));
        currentNodeIndex = clamped;
        currentRunPlan.currentIndex = clamped;

        return true;
    }

    public bool CommitVictoryAndAdvanceNode()
    {
        if (currentRunPlan == null || !currentRunPlan.HasNodes)
        {
            Debug.LogWarning("[RunSessionState] CommitVictory: RunPlan null/empty.");
            return false;
        }

        bool advanced = RunNavigator.TryAdvance(currentRunPlan);
        if (!advanced)
        {
            Debug.LogWarning("[RunSessionState] CommitVictory: TryAdvance a echoue (deja au-dela de la fin?).");
            return false;
        }

        currentNodeIndex = currentRunPlan.currentIndex;
        PersistProgressAndShip();

        OnNodeChanged.Invoke();
        return true;
    }

    // ------------------------------------------------------------
    // PERSIST MINIMAL (progression + ship)
    // ------------------------------------------------------------

    private void PersistProgressAndShip()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        run.worldId = worldId;
        run.currentNodeIndex = Mathf.Max(0, currentNodeIndex);
        run.currentShipId = shipId;

        SaveManager.Instance.Save();
    }

    private void RaiseAllChangedEvents()
    {
        OnNodeChanged.Invoke();
        OnShipChanged.Invoke(shipId);

        RaiseHullEvents();

        OnContractLivesChanged.Invoke(contractLives);
        OnRunScoreChanged.Invoke(runScore);
        OnMoneyChanged.Invoke(money);
        OnEquipmentChanged.Invoke();
    }

    private void RaiseHullEvents()
    {
        OnHullMaxChanged.Invoke(hullMax);
        OnHullChanged.Invoke(hull);
    }

    // ------------------------------------------------------------
    // SHIP API
    // ------------------------------------------------------------

    public void SetShipId(string newShipId, bool persistToSave)
    {
        string resolved = string.IsNullOrEmpty(newShipId) ? "CORE_SCOUT" : newShipId;

        if (shipId == resolved)
            return;

        shipId = resolved;

        if (persistToSave)
            PersistProgressAndShip();

        EnsureUnlockedSlotsInRunInitialized();

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        OnShipChanged.Invoke(shipId);
        OnEquipmentChanged.Invoke();
        RaiseHullEvents();
    }

    // ------------------------------------------------------------
    // RESSOURCES API
    // ------------------------------------------------------------

    public void RemoveHull(int amount)
    {
        int loss = Mathf.Max(1, amount);
        int prev = hull;
        hull = Mathf.Max(0, hull - loss);

        if (hull == prev)
            return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetRemainingHullInRun(hull);

        OnHullChanged.Invoke(hull);
    }

    public void RepairHull(int amount)
    {
        int add = Mathf.Max(1, amount);
        int prev = hull;

        hull = Mathf.Clamp(hull + add, 0, hullMax);

        if (hull == prev)
            return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetRemainingHullInRun(hull);

        OnHullChanged.Invoke(hull);
    }

    public void SetRunScore(int value)
    {
        int v = Mathf.Max(0, value);
        if (v == runScore)
            return;

        runScore = v;
        if (SaveManager.Instance != null)
            SaveManager.Instance.SetCurrentRunScore(runScore);

        OnRunScoreChanged.Invoke(runScore);
    }

    public void AddMoney(int amount)
    {
        int add = Mathf.Max(0, amount);
        if (add <= 0)
            return;

        money += add;
        if (SaveManager.Instance != null)
            SaveManager.Instance.SetMoney(money);

        OnMoneyChanged.Invoke(money);
    }

    public bool TrySpendMoney(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (cost <= 0)
            return true;

        if (money < cost)
            return false;

        money -= cost;

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetMoney(money);

        OnMoneyChanged.Invoke(money);
        return true;
    }

    public void LoseContractLife(int amount = 1)
    {
        int loss = Mathf.Max(1, amount);
        int prev = contractLives;
        contractLives = Mathf.Max(0, contractLives - loss);

        if (contractLives == prev)
            return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetRemainingContractLives(contractLives);

        OnContractLivesChanged.Invoke(contractLives);
    }

    // ------------------------------------------------------------
    // HULL MAX DERIVE (Ship + Modules)
    // ------------------------------------------------------------

    private void RecomputeDerivedHullMax(bool applyDeltaToCurrentHull)
    {
        int oldMax = Mathf.Max(1, hullMax);

        int shipBaseMax = GetShipBaseMaxHull();

        int newMax = Mathf.Max(1, shipBaseMax);

        hullMax = newMax;

        if (applyDeltaToCurrentHull)
        {
            int delta = newMax - oldMax;
            if (delta > 0)
                hull += delta;
        }

        hull = Mathf.Clamp(hull, 0, hullMax);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetRemainingHullInRun(hull);
    }


    private int GetShipBaseMaxHull()
    {
        ShipDefinition def = ShipCatalogService.GetById(shipId);
        if (def == null)
            return 3;

        return Mathf.Max(1, def.maxHull);
    }


    // ------------------------------------------------------------
    // SCAN -> Briefing Tier dérivé des modules équipés
    // ------------------------------------------------------------

    public BriefingTier GetEffectiveBriefingTier()
    {
        BriefingTier tier = BriefingTier.T0;

        EnsureEquipmentInitialized();

        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string id = equippedModuleIds[i];
            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(id);
            if (mod == null)
                continue;

            if (!string.Equals(mod.familyId, "SCAN", StringComparison.Ordinal))
                continue;

            int t = Mathf.Clamp(mod.scanTierSet, 0, 3);

            if (t >= 3) return BriefingTier.T3;
            if (t == 2) tier = BriefingTier.T2;
            else if (t == 1 && tier == BriefingTier.T0) tier = BriefingTier.T1;
        }

        return tier;
    }

    // ------------------------------------------------------------
    // EQUIPMENT API
    // ------------------------------------------------------------

    private void EnsureEquipmentInitialized()
    {
        int count = Mathf.Max(0, equipmentSlotCount);

        if (equippedModuleIds == null || equippedModuleIds.Length != count)
            equippedModuleIds = new string[count];
    }

    private void ResetEquipmentEmpty()
    {
        EnsureEquipmentInitialized();

        for (int i = 0; i < equippedModuleIds.Length; i++)
            equippedModuleIds[i] = null;
    }

    private int GetBaseOpenSlotCountFromShip()
    {
        ShipDefinition def = ShipCatalogService.GetById(shipId);
        if (def == null)
            return Mathf.Clamp(3, 0, EquipmentSlotCount);

        return Mathf.Clamp(def.unlockedModuleSlots, 0, EquipmentSlotCount);
    }

    private int GetUnlockedSlotsInRunFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return 0;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return 0;

        return Mathf.Max(0, run.unlockedModuleSlotsInRun);
    }

    private void SetUnlockedSlotsInRunToSave(int value)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        run.unlockedModuleSlotsInRun = Mathf.Max(0, value);
        SaveManager.Instance.Save();
    }

    private void EnsureUnlockedSlotsInRunInitialized()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        int baseOpen = GetBaseOpenSlotCountFromShip();
        int inRun = Mathf.Max(0, run.unlockedModuleSlotsInRun);

        if (inRun <= 0 || inRun < baseOpen)
        {
            run.unlockedModuleSlotsInRun = baseOpen;
            SaveManager.Instance.Save();
        }
    }

    private void ResetUnlockedSlotsInRunToShipBase()
    {
        int baseOpen = GetBaseOpenSlotCountFromShip();
        SetUnlockedSlotsInRunToSave(baseOpen);
    }

    private int GetOpenSlotCountEffective()
    {
        int baseOpen = GetBaseOpenSlotCountFromShip();
        int inRun = GetUnlockedSlotsInRunFromSave();
        int effective = Mathf.Max(baseOpen, inRun);
        return Mathf.Clamp(effective, 0, EquipmentSlotCount);
    }

    public bool IsEquipmentSlotLocked(int slotIndex)
    {
        EnsureEquipmentInitialized();

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return true;

        int openCount = GetOpenSlotCountEffective();
        return slotIndex >= openCount;
    }

    public string GetEquippedModuleId(int slotIndex)
    {
        EnsureEquipmentInitialized();
        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return null;

        return equippedModuleIds[slotIndex];
    }

    public bool TryEquipModuleToSlot(string moduleId, int slotIndex)
    {
        EnsureEquipmentInitialized();

        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return false;

        if (IsEquipmentSlotLocked(slotIndex))
            return false;

        if (!ModuleCatalogService.EnsureLoaded())
            return false;

        ModuleDefinition newDef = ModuleCatalogService.GetById(moduleId);
        if (newDef == null)
            return false;

        if (!IsOwnedRuntime(moduleId))
            return false;

        if (!MeetsTierChainPrerequisites(newDef, out string missingPrereqId))
            return false;

        // Exclusivité famille: auto-déséquipement
        UnequipOtherModulesInSameFamily(newDef, slotIndex);

        equippedModuleIds[slotIndex] = moduleId;
        PushEquipmentToSave();

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        OnEquipmentChanged.Invoke();
        RaiseHullEvents();

        return true;
    }

    public bool UnequipSlot(int slotIndex)
    {
        EnsureEquipmentInitialized();

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return false;

        if (string.IsNullOrEmpty(equippedModuleIds[slotIndex]))
            return false;

        equippedModuleIds[slotIndex] = null;
        PushEquipmentToSave();

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        OnEquipmentChanged.Invoke();
        RaiseHullEvents();

        return true;
    }

    public void ClearAllEquippedModules()
    {
        EnsureEquipmentInitialized();

        bool changed = false;
        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(equippedModuleIds[i]))
            {
                equippedModuleIds[i] = null;
                changed = true;
            }
        }

        if (!changed)
            return;

        PushEquipmentToSave();

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: true);

        OnEquipmentChanged.Invoke();
        RaiseHullEvents();
    }

    public bool TryUnlockOneModuleSlotInRun()
    {
        int open = GetOpenSlotCountEffective();
        if (open >= EquipmentSlotCount)
            return false;

        int newOpen = open + 1;

        SetUnlockedSlotsInRunToSave(newOpen);

        OnEquipmentChanged.Invoke();
        return true;
    }

    // ------------------------------------------------------------
    // REGLES MODULES (runtime)
    // ------------------------------------------------------------

    private bool IsOwnedRuntime(string moduleId)
    {
        if (DebugTreatAllModulesAsOwnedGlobal)
            return true;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        return SaveManager.Instance.HasOwnedModule(moduleId);
    }

    private bool MeetsTierChainPrerequisites(ModuleDefinition def, out string missingPrereqId)
    {
        missingPrereqId = null;

        if (def == null)
            return false;

        int tier = Mathf.Max(1, def.tier);

        if (tier <= 1)
            return true;

        if (string.IsNullOrEmpty(def.familyId))
            return true;

        List<ModuleDefinition> modules = ModuleCatalogService.Catalog != null ? ModuleCatalogService.Catalog.modules : null;
        if (modules == null)
            return true;

        for (int requiredTier = 1; requiredTier < tier; requiredTier++)
        {
            ModuleDefinition prereq = modules.Find(m =>
                m != null &&
                string.Equals(m.familyId, def.familyId, StringComparison.Ordinal) &&
                m.tier == requiredTier);

            if (prereq == null)
                continue;

            if (!IsOwnedRuntime(prereq.id))
            {
                missingPrereqId = prereq.id;
                return false;
            }
        }

        return true;
    }

    private void UnequipOtherModulesInSameFamily(ModuleDefinition targetDef, int targetSlotIndex)
    {
        if (targetDef == null)
            return;

        if (string.IsNullOrEmpty(targetDef.familyId))
            return;

        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            if (i == targetSlotIndex)
                continue;

            string otherId = equippedModuleIds[i];
            if (string.IsNullOrEmpty(otherId))
                continue;

            ModuleDefinition otherDef = ModuleCatalogService.GetById(otherId);
            if (otherDef == null)
                continue;

            if (string.Equals(otherDef.familyId, targetDef.familyId, StringComparison.Ordinal))
                equippedModuleIds[i] = null;
        }
    }

    private void SanitizeEquippedModulesRuntime()
    {
        EnsureEquipmentInitialized();

        if (!ModuleCatalogService.EnsureLoaded())
            return;

        bool changed = false;

        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string id = equippedModuleIds[i];
            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition def = ModuleCatalogService.GetById(id);
            if (def == null)
            {
                equippedModuleIds[i] = null;
                changed = true;
                continue;
            }

            if (!IsOwnedRuntime(id))
            {
                equippedModuleIds[i] = null;
                changed = true;
                continue;
            }

            if (!MeetsTierChainPrerequisites(def, out string missing))
            {
                equippedModuleIds[i] = null;
                changed = true;
                continue;
            }
        }

        // Normalisation famille unique: premier slot garde, suivants virent
        HashSet<string> seenFamilies = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string id = equippedModuleIds[i];
            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition def = ModuleCatalogService.GetById(id);
            if (def == null || string.IsNullOrEmpty(def.familyId))
                continue;

            if (seenFamilies.Contains(def.familyId))
            {
                equippedModuleIds[i] = null;
                changed = true;
                continue;
            }

            seenFamilies.Add(def.familyId);
        }

        if (changed)
        {
            PushEquipmentToSave();
            OnEquipmentChanged.Invoke();
        }
    }

    // ------------------------------------------------------------
    // API PUBLIQUE: SHOP (warning) - prerequis tiers manquant
    // ------------------------------------------------------------

    public bool TryGetMissingTierPrerequisite(string moduleId, out string missingPrereqId)
    {
        missingPrereqId = null;

        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (!ModuleCatalogService.EnsureLoaded())
            return false;

        ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
        if (def == null)
            return false;

        int tier = Mathf.Max(1, def.tier);

        if (tier <= 1)
            return true;

        if (string.IsNullOrEmpty(def.familyId))
            return true;

        List<ModuleDefinition> modules = ModuleCatalogService.Catalog != null ? ModuleCatalogService.Catalog.modules : null;
        if (modules == null)
            return true;

        for (int requiredTier = 1; requiredTier < tier; requiredTier++)
        {
            ModuleDefinition prereq = modules.Find(m =>
                m != null &&
                string.Equals(m.familyId, def.familyId, StringComparison.Ordinal) &&
                m.tier == requiredTier);

            if (prereq == null)
                continue;

            if (!IsOwnedRuntime(prereq.id))
            {
                missingPrereqId = prereq.id;
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------
    // API PUBLIQUE: TUNING (message) - expliquer un échec d'équipement
    // ------------------------------------------------------------

    public bool TryExplainEquipFailure(string moduleId, int slotIndex, out EquipFailReason reason, out string missingPrereqId)
    {
        reason = EquipFailReason.None;
        missingPrereqId = null;

        EnsureEquipmentInitialized();

        if (string.IsNullOrEmpty(moduleId))
        {
            reason = EquipFailReason.InvalidModule;
            return true;
        }

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length || IsEquipmentSlotLocked(slotIndex))
        {
            reason = EquipFailReason.SlotLocked;
            return true;
        }

        if (!ModuleCatalogService.EnsureLoaded())
        {
            reason = EquipFailReason.InvalidModule;
            return true;
        }

        ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
        if (def == null)
        {
            reason = EquipFailReason.InvalidModule;
            return true;
        }

        if (!IsOwnedRuntime(moduleId))
        {
            reason = EquipFailReason.NotOwned;
            return true;
        }

        if (!MeetsTierChainPrerequisites(def, out missingPrereqId))
        {
            reason = EquipFailReason.MissingPrerequisite;
            return true;
        }

        reason = EquipFailReason.None;
        return true;
    }

    // ------------------------------------------------------------
    // EQUIPMENT PERSIST (equippedModuleIds)
    // ------------------------------------------------------------

    private void PullEquipmentFromSave()
    {
        EnsureEquipmentInitialized();

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            ResetEquipmentEmpty();
            return;
        }

        SaveManager.Instance.EnsureEquipmentArrays(EquipmentSlotCount);
        RunStateData run = SaveManager.Instance.GetRunState();

        bool needsInit =
            run.equippedModuleIds == null ||
            run.equippedModuleIds.Length != EquipmentSlotCount;

        if (needsInit)
        {
            ResetEquipmentEmpty();
            PushEquipmentToSave();
            return;
        }

        Array.Copy(run.equippedModuleIds, equippedModuleIds, EquipmentSlotCount);
    }

    private void PushEquipmentToSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureEquipmentArrays(EquipmentSlotCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        Array.Copy(equippedModuleIds, run.equippedModuleIds, EquipmentSlotCount);

        SaveManager.Instance.Save();
    }

    /// <summary>
    /// Retourne le bonus de seuil de flush apporté par les modules GREED.
    /// Ex: T1=+1, T2=+2, T3=+3.
    /// </summary>
    public int GetFlushMinBallsBonusFromModules()
    {
        EnsureEquipmentInitialized();

        if (!ModuleCatalogService.EnsureLoaded())
            return 0;

        int bonus = 0;

        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string id = equippedModuleIds[i];
            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(id);
            if (mod == null)
                continue;

            if (string.Equals(mod.familyId, "GREED", StringComparison.Ordinal))
                bonus += Mathf.Max(0, mod.flushMinBallsAdd);
        }

        return bonus;
    }


    /// <summary>
    /// Calcule les bonus de fin de level apportés par les modules de la famille H (Sustain).
    ///
    /// Design actuel :
    /// H1 -> +1 Hull
    /// H2 -> +1 Hull +1 Money
    /// H3 -> +2 Hull +1 Money
    ///
    /// Important :
    /// - Cette méthode NE modifie rien.
    /// - Elle se contente de lire les modules équipés.
    /// - L'application réelle des bonus se fait dans le flow de fin de niveau.
    /// </summary>
    public (int hullGain, int moneyGain) GetEndLevelSustainBonus()
    {
        EnsureEquipmentInitialized();

        int hullGain = 0;
        int moneyGain = 0;

        // Sécurité : si le catalogue n'est pas chargé on ne donne aucun bonus
        if (!ModuleCatalogService.EnsureLoaded())
            return (0, 0);

        // Parcours des modules équipés
        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string id = equippedModuleIds[i];

            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(id);

            if (mod == null)
                continue;

            // On ne s'intéresse qu'à la famille H
            if (!string.Equals(mod.familyId, "H", StringComparison.Ordinal))
                continue;

            // Application du design par tier
            switch (mod.tier)
            {
                case 1:
                    hullGain += 1;
                    break;

                case 2:
                    hullGain += 1;
                    moneyGain += 1;
                    break;

                case 3:
                    hullGain += 2;
                    moneyGain += 1;
                    break;
            }
        }

        return (hullGain, moneyGain);
    }


}
