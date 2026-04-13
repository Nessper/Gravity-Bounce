using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Initialise completement une nouvelle run a partir d un ship selectionne.
///
/// Responsabilites :
/// - reset propre des donnees de run
/// - initialisation des ressources de depart
/// - initialisation modules / equipement / shop
/// - application de la regle :
///   un module de depart est equipe, owned,
///   et tous les tiers precedents de sa famille sont aussi owned
///
/// Classe stateless, sans MonoBehaviour.
/// </summary>
public static class NewRunInitializer
{
    private const string DefaultWorldId = "W1";
    private const int DefaultContractLives = 3;

    /// <summary>
    /// Initialise une nouvelle run dans la save a partir du ship selectionne.
    /// </summary>
    public static bool Initialize(GameSaveData save, ShipDefinition ship)
    {
        if (save == null)
        {
            Debug.LogError("[NewRunInitializer] GameSaveData null.");
            return false;
        }

        if (ship == null)
        {
            Debug.LogError("[NewRunInitializer] ShipDefinition null.");
            return false;
        }

        if (save.runState == null)
            save.runState = new RunStateData();

        RunStateData run = save.runState;

        // ------------------------------------------------------------
        // SAVE ROOT
        // ------------------------------------------------------------
        save.selectedShipId = ship.id;
        save.money = Mathf.Max(0, ship.startingMoney);

        // ------------------------------------------------------------
        // IDENTITE DE RUN
        // ------------------------------------------------------------
        run.runId = Guid.NewGuid().ToString();
        run.hasOngoingRun = true;

        // ------------------------------------------------------------
        // PROGRESSION
        // ------------------------------------------------------------
        run.worldId = DefaultWorldId;
        run.currentNodeIndex = 0;
        run.currentShipId = ship.id;

        // ------------------------------------------------------------
        // RESSOURCES
        // ------------------------------------------------------------
        run.remainingHullInRun = Mathf.Max(1, ship.baseHull);
        run.remainingContractLives = DefaultContractLives;
        run.currentRunScore = 0;
        run.nodesClearedInRun = 0;
        run.bonusHullMaxInRun = 0;

        // ------------------------------------------------------------
        // ETAT GAMEPLAY
        // ------------------------------------------------------------
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        run.pendingAbortHullPenaltyFeedback = false;
        run.lastAbortHullPenaltyAmount = 0;
        run.pendingGameOverFromAbort = false;

        // ------------------------------------------------------------
        // END TOKEN / SNAPSHOT
        // ------------------------------------------------------------
        run.hasPendingEndToken = false;
        run.pendingEndToken = default;
        run.pendingEndTokenCommitted = false;

        run.hasPendingEndSnapshot = false;
        run.pendingEndSnapshot = null;

        // ------------------------------------------------------------
        // MODULES / EQUIPEMENT / SHOP
        // ------------------------------------------------------------
        run.unlockedModuleSlotsInRun = Mathf.Max(0, ship.startingUnlockedModuleSlots);

        run.equippedModuleIds = BuildEquippedModulesForNewRun(ship);
        run.ownedModuleIdsInRun = BuildOwnedModulesForNewRun(run.equippedModuleIds);

        run.shopOfferModuleIds = null;
        run.shopRerollCount = 0;
        run.shopOfferInitialized = false;

        Debug.Log("[NewRunInitializer] New run initialized for shipId=" + ship.id);
        Debug.Log("[NewRunInitializer] money=" + save.money);
        Debug.Log("[NewRunInitializer] equipped=" + string.Join(", ", run.equippedModuleIds));
        Debug.Log("[NewRunInitializer] owned=" + string.Join(", ", run.ownedModuleIdsInRun));

        return true;
    }

    /// <summary>
    /// Construit le tableau des modules equipes de depart de la run
    /// a partir du ship.
    ///
    /// IMPORTANT :
    /// - Le tableau retourne doit etre de taille fixe (nombre total de slots du ship)
    /// - Les modules sont places dans les premiers slots
    /// - Les slots restants restent a null
    ///
    /// Cela garantit la compatibilite avec RunSessionState,
    /// qui attend un tableau d equipement indexe par slot.
    /// </summary>
    private static string[] BuildEquippedModulesForNewRun(ShipDefinition ship)
    {
        if (ship == null)
            return Array.Empty<string>();

        int slotCount = Mathf.Max(0, ship.totalModuleSlots);
        string[] equipped = new string[slotCount];

        if (ship.startingEquippedModuleIds == null || ship.startingEquippedModuleIds.Count == 0)
            return equipped;

        int writeIndex = 0;

        for (int i = 0; i < ship.startingEquippedModuleIds.Count; i++)
        {
            string moduleId = ship.startingEquippedModuleIds[i];

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (writeIndex >= equipped.Length)
                break;

            equipped[writeIndex] = moduleId;
            writeIndex++;
        }

        return equipped;
    }

    /// <summary>
    /// Construit la liste des modules owned au debut de la run.
    ///
    /// Regle :
    /// - les modules equipes sont owned
    /// - tous les tiers precedents de leur famille sont aussi owned
    /// </summary>
    private static List<string> BuildOwnedModulesForNewRun(string[] equippedModuleIds)
    {
        HashSet<string> owned = new HashSet<string>();

        if (equippedModuleIds == null || equippedModuleIds.Length == 0)
            return new List<string>();

        for (int i = 0; i < equippedModuleIds.Length; i++)
        {
            string moduleId = equippedModuleIds[i];
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            AddModuleAndPreviousTiers(moduleId, owned);
        }

        return owned.ToList();
    }

    /// <summary>
    /// Ajoute un module a la liste owned, puis ajoute aussi
    /// tous les tiers precedents de la meme famille.
    /// </summary>
    private static void AddModuleAndPreviousTiers(string moduleId, HashSet<string> owned)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || owned == null)
            return;

        ModuleDefinition def = ResolveModuleDefinition(moduleId);
        if (def == null)
        {
            Debug.LogWarning("[NewRunInitializer] ModuleDefinition introuvable pour init owned: " + moduleId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(def.id))
            owned.Add(def.id);

        string familyId = def.familyId;
        int tier = Mathf.Max(1, def.tier);

        if (string.IsNullOrWhiteSpace(familyId))
            return;

        if (ModuleCatalogService.Catalog == null || ModuleCatalogService.Catalog.modules == null)
            return;

        for (int t = 1; t < tier; t++)
        {
            ModuleDefinition previousTier = ModuleCatalogService.Catalog.modules.FirstOrDefault(
                m => m != null &&
                     m.familyId == familyId &&
                     m.tier == t);

            if (previousTier != null && !string.IsNullOrWhiteSpace(previousTier.id))
                owned.Add(previousTier.id);
        }
    }

    /// <summary>
    /// Resolve une ModuleDefinition a partir de son id.
    /// </summary>
    private static ModuleDefinition ResolveModuleDefinition(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        if (ModuleCatalogService.Catalog == null ||
            ModuleCatalogService.Catalog.modules == null)
        {
            Debug.LogWarning("[NewRunInitializer] ModuleCatalog non charge.");
            return null;
        }

        return ModuleCatalogService.Catalog.modules.FirstOrDefault(
            m => m != null && m.id == moduleId);
    }
}