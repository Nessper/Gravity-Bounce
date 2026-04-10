// Chemin recommandé (projet Unity) : Scripts/Systems/Run/RunSessionState.cs

using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// RunSessionState
/// ------------------------------------------------------------
/// État runtime principal d'une run.
///
/// Responsabilités :
/// - Miroir runtime du RunStateData persisté dans SaveManager.
/// - Suivi de la progression dans le RunPlan (worldId, node courant, etc.).
/// - Suivi des ressources de run (Hull, ContractLives, RunScore, Money).
/// - Stockage bas niveau de l'équipement (slots, modules équipés).
/// - Persistance minimale vers la save.
/// - Exposition d'événements pour le reste du jeu.
///
/// Important :
/// - Les règles métier d'équipement (ownership, tiers, exclusivité famille, sanitize)
///   ne vivent plus ici : elles sont déléguées à RunModuleEquipmentService.
/// - L'agrégation gameplay des effets modules ne vit plus ici : elle est gérée
///   par ModuleRuntimeStats.
/// </summary>
[CreateAssetMenu(fileName = "RunSessionState", menuName = "Game/Run Session State")]
public class RunSessionState : ScriptableObject
{
    // ------------------------------------------------------------
    // DEBUG GLOBAL (runtime only)
    // ------------------------------------------------------------

    /// <summary>
    /// Flag global de debug : si vrai, tous les modules sont considérés comme "owned"
    /// pour les règles runtime d'équipement.
    ///
    /// Important :
    /// - Runtime only
    /// - Non persisté en save
    /// - Piloté par MainDebugStarterV3
    /// </summary>
    public static bool DebugTreatAllModulesAsOwnedGlobal = false;

    // ------------------------------------------------------------
    // NODES / PLAN
    // Source de vérité persistée : SaveManager.runState.worldId + currentNodeIndex
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
    // SHIP
    // Source de vérité persistée : SaveManager.runState.currentShipId
    // ------------------------------------------------------------

    [Header("Ship")]
    [SerializeField] private string shipId = "CORE_SCOUT";

    public UnityEvent<string> OnShipChanged = new UnityEvent<string>();

    public string ShipId => shipId;

    // ------------------------------------------------------------
    // RESSOURCES DE RUN
    // Miroir runtime de la save
    // ------------------------------------------------------------

    [Header("Hull")]
    [SerializeField] private int hull;
    [SerializeField] private int hullMax;

    public UnityEvent<int> OnHullChanged = new UnityEvent<int>();
    public UnityEvent<int> OnHullMaxChanged = new UnityEvent<int>();

    public int Hull => hull;
    public int HullMax => hullMax;

    [Header("Contract Strikes")]
    [SerializeField] private int contractLives;

    public UnityEvent<int> OnContractLivesChanged = new UnityEvent<int>();

    public int ContractLives => contractLives;

    [Header("Run Score")]
    [SerializeField] private int runScore;

    public UnityEvent<int> OnRunScoreChanged = new UnityEvent<int>();

    public int RunScore => runScore;

    [SerializeField] private int bonusHullMaxInRun;
    public int BonusHullMaxInRun => bonusHullMaxInRun;

    [Header("Money")]
    [SerializeField] private int money;

    public UnityEvent<int> OnMoneyChanged = new UnityEvent<int>();

    public int Money => money;

    // ------------------------------------------------------------
    // ÉQUIPEMENT
    // Stockage bas niveau des slots et des modules équipés
    // ------------------------------------------------------------

    [Header("Equipment")]
    [SerializeField] private int equipmentSlotCount = 6;
    [SerializeField] private string[] equippedModuleIds;

    public UnityEvent OnEquipmentChanged = new UnityEvent();

    public int EquipmentSlotCount => Mathf.Max(0, equipmentSlotCount);

    // ------------------------------------------------------------
    // DIAGNOSTIC TUNING
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

    /// <summary>
    /// Recharge l'état runtime depuis la save.
    /// </summary>
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

        Debug.Log(
            "[RunSessionState] LoadFromSave worldId=" + worldId +
            " currentNodeIndex=" + currentNodeIndex +
            " hasOngoingRun=" + run.hasOngoingRun +
            " runId=" + run.runId);

        shipId = string.IsNullOrEmpty(run.currentShipId) ? "CORE_SCOUT" : run.currentShipId;

        hull = Mathf.Max(0, run.remainingHullInRun);
        contractLives = Mathf.Max(0, run.remainingContractLives);
        runScore = Mathf.Max(0, run.currentRunScore);
        bonusHullMaxInRun = Mathf.Max(0, run.bonusHullMaxInRun);
        money = Mathf.Max(0, SaveManager.Instance.GetMoney());

        EnsureUnlockedSlotsInRunInitialized();
        PullEquipmentFromSave();

        // Défensif : purge l'équipement invalide si le service est présent.
        if (RunModuleEquipmentService.Instance != null)
            RunModuleEquipmentService.Instance.SanitizeEquippedModulesRuntime();

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        if (!EnsurePlanLoaded())
            return false;

        RaiseAllChangedEvents();
        return true;
    }

    /// <summary>
    /// Initialise une nouvelle run complète côté runtime + save.
    /// </summary>
    public bool StartNewRun(
        string newWorldId,
        string initialShipId,
        int initialHull,
        int initialContractLives,
        int initialMoney,
        int initialRunScore)
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
        bonusHullMaxInRun = 0;

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

    /// <summary>
    /// Garantit qu'un RunPlan runtime cohérent est chargé pour le worldId courant.
    /// </summary>
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

    /// <summary>
    /// Valide une victoire et avance le node courant dans la run.
    /// </summary>
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
            Debug.LogWarning("[RunSessionState] CommitVictory: TryAdvance a échoué.");
            return false;
        }

        currentNodeIndex = currentRunPlan.currentIndex;
        PersistProgressAndShip();

        OnNodeChanged.Invoke();
        return true;
    }

    // ------------------------------------------------------------
    // PERSIST MINIMALE
    // Progression + ship
    // ------------------------------------------------------------

    /// <summary>
    /// Persiste la progression minimale de run dans la save.
    /// </summary>
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

    /// <summary>
    /// Réémet tous les événements principaux après un chargement/rebuild.
    /// </summary>
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

    /// <summary>
    /// Réémet les événements liés au Hull.
    /// </summary>
    private void RaiseHullEvents()
    {
        OnHullMaxChanged.Invoke(hullMax);
        OnHullChanged.Invoke(hull);
    }

    // ------------------------------------------------------------
    // SHIP API
    // ------------------------------------------------------------

    /// <summary>
    /// Change le ship courant de la run.
    /// </summary>
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

    /// <summary>
    /// Retire du Hull courant.
    /// </summary>
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

    /// <summary>
    /// Répare du Hull courant.
    /// </summary>
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

    /// <summary>
    /// Écrit le RunScore courant.
    /// </summary>
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

    /// <summary>
    /// Ajoute de l'argent à la run.
    /// </summary>
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

    /// <summary>
    /// Tente de dépenser de l'argent de run.
    /// </summary>
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

    /// <summary>
    /// Retire une ou plusieurs vies de contrat.
    /// </summary>
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

    /// <summary>
    /// Ajoute un delta au score de run.
    /// Le résultat final est clampé à 0 minimum.
    /// </summary>
    public void AddToRunScore(int delta)
    {
        int newValue = Mathf.Max(0, runScore + delta);
        SetRunScore(newValue);
    }

    /// <summary>
    /// Ajoute un bonus permanent de HullMax pour le reste de la run.
    /// 
    /// alsoRepairByDelta :
    /// - true  -> si le max augmente, le Hull courant augmente du même delta
    /// - false -> seul le max augmente, le Hull courant reste clampé
    /// </summary>
    public void AddBonusHullMaxInRun(int amount, bool alsoRepairByDelta)
    {
        int add = Mathf.Max(0, amount);
        if (add <= 0)
            return;

        bonusHullMaxInRun += add;

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            RunStateData run = SaveManager.Instance.GetRunState();
            if (run != null)
            {
                run.bonusHullMaxInRun = bonusHullMaxInRun;
                SaveManager.Instance.Save();
            }
        }

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: alsoRepairByDelta);
        RaiseHullEvents();
    }

    /// <summary>
    /// Force la valeur courante de Hull.
    /// Usage reserve aux systemes de restore / init / tutorial reset.
    /// </summary>
    public void SetHullDirect(int value)
    {
        hull = Mathf.Clamp(Mathf.Max(0, value), 0, Mathf.Max(1, hullMax));

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetRemainingHullInRun(hull);

        OnHullChanged.Invoke(hull);
    }

    /// <summary>
    /// Force le bonus de Hull max de run, puis recalcule le HullMax derive.
    /// Usage reserve aux systemes de restore / init / tutorial reset.
    /// </summary>
    public void SetBonusHullMaxInRunDirect(int value, bool preserveCurrentHullRatio = false)
    {
        int previousMax = Mathf.Max(1, hullMax);

        bonusHullMaxInRun = Mathf.Max(0, value);

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            RunStateData run = SaveManager.Instance.GetRunState();
            if (run != null)
            {
                run.bonusHullMaxInRun = bonusHullMaxInRun;
                SaveManager.Instance.Save();
            }
        }

        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);

        if (preserveCurrentHullRatio)
        {
            float ratio = previousMax > 0 ? (float)hull / previousMax : 0f;
            hull = Mathf.Clamp(Mathf.RoundToInt(ratio * hullMax), 0, hullMax);

            if (SaveManager.Instance != null)
                SaveManager.Instance.SetRemainingHullInRun(hull);
        }

        RaiseHullEvents();
    }

    // ------------------------------------------------------------
    // HULL MAX DÉRIVÉ
    // Pour l'instant : uniquement basé sur le ship
    // ------------------------------------------------------------

    /// <summary>
    /// Recalcule le Hull max dérivé.
    ///
    /// Important :
    /// - Actuellement basé uniquement sur le ship.
    /// - Les bonus modules de HullMax pourront être intégrés plus tard.
    /// </summary>
    private void RecomputeDerivedHullMax(bool applyDeltaToCurrentHull)
    {
        int oldMax = Mathf.Max(1, hullMax);

        int shipBaseMax = GetShipBaseMaxHull();

        int passiveModulesHullBonus = 0;
        if (ModuleRuntimeStats.Instance != null)
            passiveModulesHullBonus = Mathf.Max(0, ModuleRuntimeStats.Instance.HullMaxAdd);

        int persistentRunHullBonus = Mathf.Max(0, bonusHullMaxInRun);

        int newMax = Mathf.Max(1, shipBaseMax + passiveModulesHullBonus + persistentRunHullBonus);

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

    /// <summary>
    /// Retourne le Hull max de base du ship courant.
    /// </summary>
    private int GetShipBaseMaxHull()
    {
        ShipDefinition def = ShipCatalogService.GetById(shipId);
        if (def == null)
            return 3;

        return Mathf.Max(1, def.baseHull);
    }

    // ------------------------------------------------------------
    // ÉQUIPEMENT API BAS NIVEAU
    // ------------------------------------------------------------

    /// <summary>
    /// Garantit que le tableau runtime des modules équipés est initialisé.
    /// </summary>
    private void EnsureEquipmentInitialized()
    {
        int count = Mathf.Max(0, equipmentSlotCount);

        if (equippedModuleIds == null || equippedModuleIds.Length != count)
            equippedModuleIds = new string[count];
    }

    /// <summary>
    /// Vide localement tous les slots d'équipement runtime.
    /// </summary>
    private void ResetEquipmentEmpty()
    {
        EnsureEquipmentInitialized();

        for (int i = 0; i < equippedModuleIds.Length; i++)
            equippedModuleIds[i] = null;
    }

    /// <summary>
    /// Retourne le nombre de slots ouverts de base fourni par le ship.
    /// </summary>
    private int GetBaseOpenSlotCountFromShip()
    {
        ShipDefinition def = ShipCatalogService.GetById(shipId);
        if (def == null)
            return Mathf.Clamp(3, 0, EquipmentSlotCount);

        return Mathf.Clamp(def.startingUnlockedModuleSlots, 0, EquipmentSlotCount);
    }

    /// <summary>
    /// Lit le nombre de slots débloqués en run depuis la save.
    /// </summary>
    private int GetUnlockedSlotsInRunFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return 0;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return 0;

        return Mathf.Max(0, run.unlockedModuleSlotsInRun);
    }

    /// <summary>
    /// Écrit le nombre de slots débloqués en run dans la save.
    /// </summary>
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

    /// <summary>
    /// Garantit que le nombre de slots ouverts en run est au moins égal au minimum du ship.
    /// </summary>
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

    /// <summary>
    /// Réinitialise le nombre de slots de run au minimum fourni par le ship.
    /// </summary>
    private void ResetUnlockedSlotsInRunToShipBase()
    {
        int baseOpen = GetBaseOpenSlotCountFromShip();
        SetUnlockedSlotsInRunToSave(baseOpen);
    }

    /// <summary>
    /// Retourne le nombre effectif de slots ouverts pour la run courante.
    /// </summary>
    private int GetOpenSlotCountEffective()
    {
        int baseOpen = GetBaseOpenSlotCountFromShip();
        int inRun = GetUnlockedSlotsInRunFromSave();
        int effective = Mathf.Max(baseOpen, inRun);

        return Mathf.Clamp(effective, 0, EquipmentSlotCount);
    }

    /// <summary>
    /// Indique si un slot d'équipement est verrouillé.
    /// </summary>
    public bool IsEquipmentSlotLocked(int slotIndex)
    {
        EnsureEquipmentInitialized();

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return true;

        int openCount = GetOpenSlotCountEffective();
        return slotIndex >= openCount;
    }

    /// <summary>
    /// Retourne le module équipé dans un slot donné.
    /// </summary>
    public string GetEquippedModuleId(int slotIndex)
    {
        EnsureEquipmentInitialized();

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return null;

        return equippedModuleIds[slotIndex];
    }

    // ------------------------------------------------------------
    // ÉQUIPEMENT API PUBLIQUE
    // Wrappers temporaires vers RunModuleEquipmentService
    // ------------------------------------------------------------

    /// <summary>
    /// Tente d'équiper un module dans un slot donné.
    /// </summary>
    public bool TryEquipModuleToSlot(string moduleId, int slotIndex)
    {
        if (RunModuleEquipmentService.Instance == null)
            return false;

        return RunModuleEquipmentService.Instance.TryEquipModuleToSlot(moduleId, slotIndex);
    }

    /// <summary>
    /// Tente de déséquiper un slot.
    /// </summary>
    public bool UnequipSlot(int slotIndex)
    {
        if (RunModuleEquipmentService.Instance == null)
            return false;

        return RunModuleEquipmentService.Instance.UnequipSlot(slotIndex);
    }

    /// <summary>
    /// Vide tout l'équipement.
    /// </summary>
    public void ClearAllEquippedModules()
    {
        if (RunModuleEquipmentService.Instance == null)
            return;

        RunModuleEquipmentService.Instance.ClearAllEquippedModules();
    }

    /// <summary>
    /// Tente de débloquer un slot supplémentaire pendant la run.
    /// </summary>
    public bool TryUnlockOneModuleSlotInRun()
    {
        if (RunModuleEquipmentService.Instance == null)
            return false;

        return RunModuleEquipmentService.Instance.TryUnlockOneModuleSlotInRun();
    }

    // ------------------------------------------------------------
    // API PUBLIQUE TUNING / SHOP
    // Wrappers temporaires vers RunModuleEquipmentService
    // ------------------------------------------------------------

    /// <summary>
    /// Retourne le prérequis de tier manquant pour un module, si nécessaire.
    /// </summary>
    public bool TryGetMissingTierPrerequisite(string moduleId, out string missingPrereqId)
    {
        missingPrereqId = null;

        if (RunModuleEquipmentService.Instance == null)
            return false;

        return RunModuleEquipmentService.Instance.TryGetMissingTierPrerequisite(moduleId, out missingPrereqId);
    }

    /// <summary>
    /// Explique la raison d'un échec d'équipement.
    /// </summary>
    public bool TryExplainEquipFailure(string moduleId, int slotIndex, out EquipFailReason reason, out string missingPrereqId)
    {
        reason = EquipFailReason.None;
        missingPrereqId = null;

        if (RunModuleEquipmentService.Instance == null)
        {
            reason = EquipFailReason.InvalidModule;
            return false;
        }

        return RunModuleEquipmentService.Instance.TryExplainEquipFailure(
            moduleId,
            slotIndex,
            out reason,
            out missingPrereqId);
    }

    // ------------------------------------------------------------
    // PERSISTENCE DE L'ÉQUIPEMENT
    // ------------------------------------------------------------

    /// <summary>
    /// Recharge l'équipement depuis la save.
    /// </summary>
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

    /// <summary>
    /// Persiste l'équipement runtime vers la save.
    /// </summary>
    private void PushEquipmentToSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureEquipmentArrays(EquipmentSlotCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        Array.Copy(equippedModuleIds, run.equippedModuleIds, EquipmentSlotCount);

        SaveManager.Instance.Save();
    }

    // ------------------------------------------------------------
    // HELPERS INTERNES
    // Utilisés par RunModuleEquipmentService
    // ------------------------------------------------------------

    /// <summary>
    /// Garantit l'initialisation du tableau d'équipement.
    /// </summary>
    internal void EnsureEquipmentInitialized_Internal()
    {
        EnsureEquipmentInitialized();
    }

    /// <summary>
    /// Écrit directement un module dans un slot runtime, sans validation métier.
    /// Réservé au service d'équipement.
    /// </summary>
    internal void SetEquippedModuleIdRaw_Internal(int slotIndex, string moduleId)
    {
        EnsureEquipmentInitialized();

        if (slotIndex < 0 || slotIndex >= equippedModuleIds.Length)
            return;

        equippedModuleIds[slotIndex] = moduleId;
    }

    /// <summary>
    /// Persiste l'équipement courant dans la save.
    /// </summary>
    internal void PushEquipmentToSave_Internal()
    {
        PushEquipmentToSave();
    }

    /// <summary>
    /// Retourne le nombre effectif de slots ouverts.
    /// </summary>
    internal int GetOpenSlotCountEffective_Internal()
    {
        return GetOpenSlotCountEffective();
    }

    /// <summary>
    /// Écrit le nombre de slots débloqués en run dans la save.
    /// </summary>
    internal void SetUnlockedSlotsInRunToSave_Internal(int value)
    {
        SetUnlockedSlotsInRunToSave(value);
    }

    /// <summary>
    /// Réémet les événements liés à l'équipement après une modification.
    /// </summary>
    internal void NotifyEquipmentChanged_Internal()
    {
        OnEquipmentChanged.Invoke();
        RecomputeDerivedHullMax(applyDeltaToCurrentHull: false);
        RaiseHullEvents();
    }
}