// Chemin recommandé (projet Unity) : Scripts/Systems/Save/SaveManager.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SaveManager
///
/// Responsabilités :
/// - Charger / sauvegarder GameSaveData dans PlayerPrefs (JSON unique).
/// - Garantir une base cohérente (ship de base, runState non-null).
/// - Appliquer une migration légère (ex: worldId/runId manquants).
/// - Appliquer la pénalité d'abandon (quit sauvage) si nécessaire.
/// - Sanity-check : empêcher un "Continue" sur une run morte.
/// - Gérer un token + snapshot de fin de niveau (anti double-commit / replay cérémonie).
///
/// RÈGLE (Modules) :
/// - 100% RUN-ONLY : ownedModuleIdsInRun + equipped + offers + slots.
/// - Quit / crash : conservé (RunStateData persisté).
/// - Fin de run / GameOver : purgé via une méthode UNIQUE EndRun_GameOver().
///
/// IMPORTANT (API) :
/// - On conserve l'API historique HasOwnedModule/TryAddOwnedModule/etc.
/// - Le SaveManager est la couche d'abstraction : les autres scripts ne doivent pas être cassés.
/// </summary>
public class SaveManager : MonoBehaviour
{
    private const string PlayerPrefsKey = "GameSave_v1";

    public static SaveManager Instance { get; private set; }
    public GameSaveData Current { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Load();
    }

    // ------------------------------------------------------------
    // LOAD / SAVE
    // ------------------------------------------------------------

    public void Load()
    {
        GameSaveData loaded = null;

        if (PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            string json = PlayerPrefs.GetString(PlayerPrefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    loaded = JsonUtility.FromJson<GameSaveData>(json);
                }
                catch
                {
                    Debug.LogWarning("[SaveManager] JSON corrompu, creation d'une nouvelle sauvegarde.");
                }
            }
        }

        if (loaded == null)
        {
            loaded = CreateDefaultSave();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        Current = loaded;

        EnsureBaseShipUnlocked();
        EnsureRunStateReady();
        EnsureRunStateMigration();
        EnsureRunOnlyCollectionsReady();

        ApplyAbortPenaltyIfNeeded();
        ReconcilePendingEndIfNeeded();
        ForceCloseInvalidOngoingRunIfNeeded();

        Debug.Log("[SaveManager] Sauvegarde chargee. selectedShipId=" + Current.selectedShipId);
    }

    public void Save()
    {
        if (Current == null)
        {
            Debug.LogWarning("[SaveManager] Save appele mais Current est null.");
            return;
        }

        string json = JsonUtility.ToJson(Current);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------
    // RESET
    // ------------------------------------------------------------

    public void ResetSave()
    {
        Current = CreateDefaultSave();
        EnsureBaseShipUnlocked();
        EnsureRunStateReady();
        EnsureRunOnlyCollectionsReady();
        Save();
    }

    public void ResetRunState()
    {
        EnsureRunStateReady();
        EnsureRunOnlyCollectionsReady();

        RunStateData run = Current.runState;

        run.runId = "";
        run.hasOngoingRun = false;

        run.worldId = "W1";
        run.currentNodeIndex = 0;

        run.currentShipId = Current.selectedShipId;

        run.remainingHullInRun = 0;

        // New run contract lives forced to 3 by design.
        run.remainingContractLives = 3;

        run.currentRunScore = 0;
        run.nodesClearedInRun = 0;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        run.pendingAbortHullPenaltyFeedback = false;
        run.lastAbortHullPenaltyAmount = 0;
        run.pendingGameOverFromAbort = false;

        // Token fin de niveau
        run.hasPendingEndToken = false;
        run.pendingEndTokenCommitted = false;
        run.pendingEndToken = default;

        // Snapshot fin de niveau (replay ceremonie)
        run.hasPendingEndSnapshot = false;
        run.pendingEndSnapshot = null;

        // Money (design actuel) : reset à chaque new run
        Current.money = 0;

        // RUN-ONLY : purge modules + shop + slots
        run.unlockedModuleSlotsInRun = 0;

        // Owned modules (run-only)
        if (run.ownedModuleIdsInRun != null)
            run.ownedModuleIdsInRun.Clear();

        // Equip / offers
        run.equippedModuleIds = null;
        run.shopOfferModuleIds = null;
        run.shopRerollCount = 0;

        Save();
    }

    // ------------------------------------------------------------
    // HELPERS RUN STATE (Hull / Contract / Score / Money)
    // ------------------------------------------------------------

    public int GetRemainingHullInRun()
    {
        EnsureRunStateReady();
        return Mathf.Max(0, Current.runState.remainingHullInRun);
    }

    public void SetRemainingHullInRun(int hull)
    {
        EnsureRunStateReady();
        Current.runState.remainingHullInRun = Mathf.Max(0, hull);
        Save();
    }

    public int GetRemainingContractLives()
    {
        EnsureRunStateReady();
        return Mathf.Max(0, Current.runState.remainingContractLives);
    }

    public void SetRemainingContractLives(int lives)
    {
        EnsureRunStateReady();
        Current.runState.remainingContractLives = Mathf.Max(0, lives);
        Save();
    }

    public int GetCurrentRunScore()
    {
        EnsureRunStateReady();
        return Mathf.Max(0, Current.runState.currentRunScore);
    }

    public void SetCurrentRunScore(int score)
    {
        EnsureRunStateReady();
        Current.runState.currentRunScore = Mathf.Max(0, score);
        Save();
    }

    public int GetMoney()
    {
        if (Current == null)
            return 0;

        return Mathf.Max(0, Current.money);
    }

    public void SetMoney(int value)
    {
        if (Current == null)
            return;

        Current.money = Mathf.Max(0, value);
        Save();
    }

    public void AddMoney(int amount)
    {
        if (Current == null)
            return;

        Current.money = Mathf.Max(0, Current.money + Mathf.Max(0, amount));
        Save();
    }

    public RunStateData GetRunState()
    {
        EnsureRunStateReady();
        return Current.runState;
    }

    private void EnsureRunStateReady()
    {
        if (Current == null)
            Current = CreateDefaultSave();

        if (Current.runState == null)
            Current.runState = new RunStateData();
    }

    /// <summary>
    /// Garantit que toutes les collections RUN-ONLY existent (évite les null après chargement d'une ancienne save).
    /// </summary>
    private void EnsureRunOnlyCollectionsReady()
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;

        // Owned modules (run-only)
        if (run.ownedModuleIdsInRun == null)
            run.ownedModuleIdsInRun = new List<string>();
    }

    // ------------------------------------------------------------
    // HELPERS RUN STATE (EQUIPMENT RUN-ONLY)
    // ------------------------------------------------------------

    public void EnsureEquipmentArrays(int slotCount)
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;
        int count = Mathf.Max(0, slotCount);

        if (run.equippedModuleIds == null || run.equippedModuleIds.Length != count)
            run.equippedModuleIds = new string[count];
    }

    public void EnsureShopOfferArrays(int offerCount)
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;
        int count = Mathf.Max(0, offerCount);

        if (run.shopOfferModuleIds == null || run.shopOfferModuleIds.Length != count)
            run.shopOfferModuleIds = new string[count];
    }

    public int GetUnlockedModuleSlotsInRun()
    {
        EnsureRunStateReady();
        return Mathf.Max(0, Current.runState.unlockedModuleSlotsInRun);
    }

    public void SetUnlockedModuleSlotsInRun(int value)
    {
        EnsureRunStateReady();
        Current.runState.unlockedModuleSlotsInRun = Mathf.Max(0, value);
        Save();
    }

    public string[] GetEquippedModuleIds()
    {
        EnsureRunStateReady();
        return Current.runState.equippedModuleIds;
    }

    public void SetEquippedModuleId(int slotIndex, string moduleId)
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;
        if (run.equippedModuleIds == null) return;
        if (slotIndex < 0 || slotIndex >= run.equippedModuleIds.Length) return;

        run.equippedModuleIds[slotIndex] = moduleId;
        Save();
    }

    public void ClearRunEquipment()
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;

        run.unlockedModuleSlotsInRun = 0; // 0 = non initialisé, sera recalé au boot run

        if (run.equippedModuleIds != null)
        {
            for (int i = 0; i < run.equippedModuleIds.Length; i++)
                run.equippedModuleIds[i] = null;
        }

        Save();
    }

    // ------------------------------------------------------------
    // MODULES (RUN-ONLY) - API HISTORIQUE (ne pas casser les callsites)
    // ------------------------------------------------------------

    /// <summary>
    /// Retourne la liste des modules possédés dans la RUN (run-only).
    /// </summary>
    public List<string> GetOwnedModuleIds()
    {
        if (Current == null)
            return null;

        EnsureRunOnlyCollectionsReady();
        return Current.runState.ownedModuleIdsInRun;
    }

    /// <summary>
    /// Retourne true si le module est possédé dans la RUN (run-only).
    /// </summary>
    public bool HasOwnedModule(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId) || Current == null)
            return false;

        EnsureRunOnlyCollectionsReady();
        return Current.runState.ownedModuleIdsInRun.Contains(moduleId);
    }

    /// <summary>
    /// Ajoute un module dans l'inventaire de la RUN (run-only).
    /// Retourne false si déjà présent.
    /// </summary>
    public bool TryAddOwnedModule(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId) || Current == null)
            return false;

        EnsureRunOnlyCollectionsReady();

        List<string> list = Current.runState.ownedModuleIdsInRun;
        if (list.Contains(moduleId))
            return false;

        list.Add(moduleId);
        Save();
        return true;
    }

    /// <summary>
    /// Purge l'inventaire owned de la RUN (run-only).
    /// </summary>
    public void ClearOwnedModules()
    {
        if (Current == null)
            return;

        EnsureRunOnlyCollectionsReady();
        Current.runState.ownedModuleIdsInRun.Clear();
        Save();
    }

    // ------------------------------------------------------------
    // MODULES (RUN-ONLY) - ALIAS OPTIONNELS (si tu en as déjà dans d'autres scripts)
    // ------------------------------------------------------------

    public List<string> GetOwnedModuleIdsInRun() => GetOwnedModuleIds();
    public bool HasOwnedModuleInRun(string moduleId) => HasOwnedModule(moduleId);
    public bool TryAddOwnedModuleInRun(string moduleId) => TryAddOwnedModule(moduleId);
    public void ClearOwnedModulesInRun() => ClearOwnedModules();

    // ------------------------------------------------------------
    // END TOKEN API
    // ------------------------------------------------------------

    public void SetPendingEndToken(EndLevelToken token)
    {
        EnsureRunStateReady();
        RunStateData run = Current.runState;

        // Par securite: ne pas ecraser un token pending non consomme.
        if (run.hasPendingEndToken && !run.pendingEndTokenCommitted)
        {
            Debug.LogWarning("[SaveManager] PendingEndToken deja present. On n'ecrase pas.");
            return;
        }

        run.pendingEndToken = token;
        run.hasPendingEndToken = true;
        run.pendingEndTokenCommitted = false;

        Save();
    }

    public bool TryGetPendingEndToken(out EndLevelToken token)
    {
        token = default;

        if (Current == null || Current.runState == null)
            return false;

        RunStateData run = Current.runState;
        if (!run.hasPendingEndToken)
            return false;

        token = run.pendingEndToken;
        return true;
    }

    public void MarkPendingEndTokenCommitted()
    {
        EnsureRunStateReady();
        RunStateData run = Current.runState;

        if (!run.hasPendingEndToken)
            return;

        run.pendingEndTokenCommitted = true;
        run.hasPendingEndToken = false;

        Save();
    }

    public void ClearPendingEndToken()
    {
        EnsureRunStateReady();
        RunStateData run = Current.runState;

        run.hasPendingEndToken = false;
        run.pendingEndTokenCommitted = false;
        run.pendingEndToken = default;

        Save();
    }

    // ------------------------------------------------------------
    // END SNAPSHOT API (replay ceremonie + idempotence)
    // ------------------------------------------------------------

    public void SetPendingEndSnapshot(EndLevelSnapshot snapshot)
    {
        EnsureRunStateReady();
        RunStateData run = Current.runState;

        if (snapshot == null)
        {
            Debug.LogWarning("[SaveManager] SetPendingEndSnapshot appele avec snapshot null.");
            return;
        }

        // Par securite: ne pas ecraser un snapshot pending non committé.
        if (run.hasPendingEndSnapshot && run.pendingEndSnapshot != null && !run.pendingEndSnapshot.RewardsCommitted)
        {
            Debug.LogWarning("[SaveManager] PendingEndSnapshot deja present (non committed). On n'ecrase pas.");
            return;
        }

        run.pendingEndSnapshot = snapshot;
        run.hasPendingEndSnapshot = true;

        Save();
    }

    public bool TryGetPendingEndSnapshot(out EndLevelSnapshot snapshot)
    {
        snapshot = null;

        if (Current == null || Current.runState == null)
            return false;

        RunStateData run = Current.runState;

        if (!run.hasPendingEndSnapshot || run.pendingEndSnapshot == null)
            return false;

        snapshot = run.pendingEndSnapshot;
        return true;
    }

    public void ClearPendingEndSnapshot()
    {
        EnsureRunStateReady();
        RunStateData run = Current.runState;

        run.hasPendingEndSnapshot = false;
        run.pendingEndSnapshot = null;

        Save();
    }

    public bool MarkPendingEndSnapshotCommitted(EndLevelToken token)
    {
        EnsureRunStateReady();
        RunStateData run = Current.runState;

        if (!run.hasPendingEndSnapshot || run.pendingEndSnapshot == null)
            return false;

        // Deja committé => idempotent
        if (run.pendingEndTokenCommitted || run.pendingEndSnapshot.RewardsCommitted)
            return false;

        // Safety runId
        if (!string.IsNullOrEmpty(run.runId) &&
            !string.IsNullOrEmpty(token.RunId) &&
            !string.Equals(run.runId, token.RunId, StringComparison.Ordinal))
        {
            Debug.LogWarning("[SaveManager] MarkPendingEndSnapshotCommitted refuse: runId mismatch.");
            return false;
        }

        EndLevelSnapshot snap = run.pendingEndSnapshot;
        snap.RewardsCommitted = true;
        run.pendingEndSnapshot = snap;

        run.pendingEndTokenCommitted = true;

        // Token pending consomme
        run.hasPendingEndToken = false;
        run.pendingEndToken = default;

        Save();
        return true;
    }

    private void ReconcilePendingEndIfNeeded()
    {
        if (Current == null || Current.runState == null)
            return;

        RunStateData run = Current.runState;

        if (!run.hasPendingEndSnapshot || run.pendingEndSnapshot == null)
            return;

        EndLevelSnapshot snap = run.pendingEndSnapshot;

        // Si la progression a déjà avancé, on considère le commit fait.
        if (run.currentNodeIndex > snap.Token.NodeIndex)
        {
            run.hasPendingEndSnapshot = false;
            run.pendingEndSnapshot = null;

            run.hasPendingEndToken = false;
            run.pendingEndToken = default;

            run.pendingEndTokenCommitted = true;
            Save();
            return;
        }

        // Si snapshot déjà committed, on peut aussi purger (évite toute ambiguïté au boot)
        if (snap.RewardsCommitted)
        {
            run.hasPendingEndSnapshot = false;
            run.pendingEndSnapshot = null;

            run.hasPendingEndToken = false;
            run.pendingEndToken = default;

            run.pendingEndTokenCommitted = true;
            Save();
            return;
        }
    }

    // ------------------------------------------------------------
    // BEST SCORE
    // ------------------------------------------------------------

    public int GetBestRunScore()
    {
        if (Current == null)
            return 0;

        return Mathf.Max(0, Current.bestRunScore);
    }

    public bool TryUpdateBestRunScore(int runScore)
    {
        if (Current == null)
            return false;

        int clamped = Mathf.Max(0, runScore);
        if (clamped > Current.bestRunScore)
        {
            Current.bestRunScore = clamped;
            Save();
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------
    // DEFAULT SAVE
    // ------------------------------------------------------------

    private GameSaveData CreateDefaultSave()
    {
        GameSaveData data = new GameSaveData();

        data.profileId = "DefaultProfile";
        data.selectedShipId = "CORE_SCOUT";
        data.unlockedShips = new List<string> { "CORE_SCOUT" };

        data.runState = new RunStateData();
        data.runState.hasOngoingRun = false;

        data.runState.worldId = "W1";
        data.runState.currentNodeIndex = 0;

        data.runState.currentShipId = "CORE_SCOUT";

        data.runState.remainingHullInRun = 0;
        data.runState.remainingContractLives = 3;
        data.runState.currentRunScore = 0;
        data.runState.nodesClearedInRun = 0;

        data.runState.levelInProgress = false;
        data.runState.abortPenaltyArmed = false;

        data.runState.hasPendingEndToken = false;
        data.runState.pendingEndTokenCommitted = false;
        data.runState.pendingEndToken = default;

        data.runState.hasPendingEndSnapshot = false;
        data.runState.pendingEndSnapshot = null;

        // RUN-ONLY : modules
        data.runState.unlockedModuleSlotsInRun = 0;
        data.runState.ownedModuleIdsInRun = new List<string>();
        data.runState.equippedModuleIds = null;
        data.runState.shopOfferModuleIds = null;

        data.bestRunScore = 0;
        data.money = 0;

        return data;
    }

    private void EnsureBaseShipUnlocked()
    {
        if (Current == null)
            return;

        if (Current.unlockedShips == null)
            Current.unlockedShips = new List<string>();

        if (!Current.unlockedShips.Contains("CORE_SCOUT"))
            Current.unlockedShips.Add("CORE_SCOUT");

        if (string.IsNullOrEmpty(Current.selectedShipId))
            Current.selectedShipId = "CORE_SCOUT";
    }

    // ------------------------------------------------------------
    // MIGRATION LEGERE
    // ------------------------------------------------------------

    private void EnsureRunStateMigration()
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;
        if (run == null)
            return;

        bool changed = false;

        if (run.hasOngoingRun && string.IsNullOrEmpty(run.runId))
        {
            run.runId = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (string.IsNullOrEmpty(run.worldId))
        {
            run.worldId = "W1";
            changed = true;
        }

        if (run.currentNodeIndex < 0)
        {
            run.currentNodeIndex = 0;
            changed = true;
        }

        // Safety: contract lives jamais < 0
        if (run.remainingContractLives < 0)
        {
            run.remainingContractLives = 0;
            changed = true;
        }

        if (changed)
            Save();
    }

    // ------------------------------------------------------------
    // ABORT PENALTY (quit sauvage)
    // ------------------------------------------------------------

    private void ApplyAbortPenaltyIfNeeded()
    {
        if (Current == null || Current.runState == null)
            return;

        RunStateData run = Current.runState;

        if (!run.hasOngoingRun)
            return;

        if (!run.levelInProgress)
            return;

        if (!run.abortPenaltyArmed)
            return;

        run.remainingHullInRun = Mathf.Max(0, run.remainingHullInRun - 1);

        run.pendingAbortHullPenaltyFeedback = true;
        run.lastAbortHullPenaltyAmount = 1;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        // Abort fatal -> fin de run CENTRALISÉE
        if (run.remainingHullInRun <= 0)
        {
            EndRun_GameOver(fromAbortPenalty: true);
            return;
        }

        // Run vivante
        run.pendingGameOverFromAbort = false;
        Save();
    }

    public bool ApplyAbortPenaltyNow(int amount)
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;
        if (run == null)
            return false;

        if (!run.hasOngoingRun)
            return false;

        int a = Mathf.Max(1, amount);

        run.remainingHullInRun = Mathf.Max(0, run.remainingHullInRun - a);

        run.pendingAbortHullPenaltyFeedback = true;
        run.lastAbortHullPenaltyAmount = a;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        // Abort fatal -> fin de run CENTRALISÉE
        if (run.remainingHullInRun <= 0)
        {
            EndRun_GameOver(fromAbortPenalty: true);
            return true;
        }

        // Run vivante
        run.pendingGameOverFromAbort = false;
        Save();
        return true;
    }

    // ------------------------------------------------------------
    // SANITY CHECK
    // ------------------------------------------------------------

    private void ForceCloseInvalidOngoingRunIfNeeded()
    {
        if (Current == null || Current.runState == null)
            return;

        RunStateData run = Current.runState;

        if (!run.hasOngoingRun)
            return;

        bool invalid = (run.remainingHullInRun <= 0) || (run.remainingContractLives <= 0);
        if (!invalid)
            return;

        EndRun_GameOver(fromAbortPenalty: true);
    }

    // ------------------------------------------------------------
    // RUN FLAGS (armement / desarmement)
    // ------------------------------------------------------------

    public void MarkLevelStartedInRun()
    {
        EnsureRunStateReady();
        EnsureRunOnlyCollectionsReady();

        RunStateData run = Current.runState;

        run.hasOngoingRun = true;
        run.levelInProgress = true;
        run.abortPenaltyArmed = true;

        if (string.IsNullOrEmpty(run.runId))
            run.runId = Guid.NewGuid().ToString("N");

        Save();
    }

    public void MarkLevelEndedNormally()
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        Save();
    }

    public void MarkGameOverInRun()
    {
        EndRun_GameOver(fromAbortPenalty: false);
    }

    /// <summary>
    /// Méthode UNIQUE de fin de run.
    /// Toute mort de run doit passer par ici.
    /// </summary>
    public void EndRun_GameOver(bool fromAbortPenalty)
    {
        EnsureRunStateReady();
        EnsureRunOnlyCollectionsReady();

        RunStateData run = Current.runState;

        // Flags run
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;
        run.hasOngoingRun = false;

        // Cause
        run.pendingGameOverFromAbort = fromAbortPenalty;

        // Purge token + snapshot
        run.hasPendingEndToken = false;
        run.pendingEndTokenCommitted = false;
        run.pendingEndToken = default;

        run.hasPendingEndSnapshot = false;
        run.pendingEndSnapshot = null;

        // RUN-ONLY : purge modules + shop + slots
        run.unlockedModuleSlotsInRun = 0;

        if (run.ownedModuleIdsInRun != null)
            run.ownedModuleIdsInRun.Clear();

        run.equippedModuleIds = null;
        run.shopOfferModuleIds = null;
        run.shopRerollCount = 0;

        Save();
    }

    // ------------------------------------------------------------
    // MONEY (run) - Dépense
    // ------------------------------------------------------------

    public bool TrySpendMoney(int amount)
    {
        if (Current == null)
            return false;

        int cost = Mathf.Max(0, amount);
        if (cost <= 0)
            return true;

        if (Current.money < cost)
            return false;

        Current.money -= cost;
        Save();
        return true;
    }

    // ------------------------------------------------------------
    // SHOP OFFERS
    // ------------------------------------------------------------

    public void ClearShopOffers()
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;

        if (run.shopOfferModuleIds != null)
        {
            for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
                run.shopOfferModuleIds[i] = null;
        }

        Save();
    }
}
