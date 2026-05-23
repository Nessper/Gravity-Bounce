// Chemin recommandé (projet Unity) : Scripts/Systems/Save/GameSaveData.cs

using System;
using System.Collections.Generic;

/// <summary>
/// Données persistantes du jeu (sérialisées en JSON via JsonUtility).
/// Stockées par SaveManager dans PlayerPrefs.
///
/// Important : cette classe doit rester "data only" (pas de logique).
///
/// RÈGLE (Modules) :
/// - Les modules sont 100% RUN-ONLY.
/// - Quit / crash : les modules restent (car RunStateData est persisté).
/// - Fin de run / GameOver : les modules sont purgés.
/// </summary>
[Serializable]
public class GameSaveData
{
    public string profileId = "DefaultProfile";
    public string selectedShipId = "CORE_SCOUT";

    public List<string> unlockedShips = new List<string>();

    public RunStateData runState = new RunStateData();

    public int bestRunScore = 0;

    // Money : ressource de run (persistée pour survivre à quit/crash pendant la run).
    // Reset à New Run selon ton design actuel (cf SaveManager.ResetRunState()).
    public int money = 0;

    // ------------------------------------------------------------
    // FLAG POUR LE TUTO
    // ------------------------------------------------------------
    public bool tutorialCompleted = false;
}

/// <summary>
/// État persistant d'une run (campagne).
///
/// Convention (IMPORTANT) :
/// - currentNodeIndex = index du node A JOUER MAINTENANT (next playable).
/// - À la victoire d’un niveau : currentNodeIndex++ puis Save.
/// - À la défaite : currentNodeIndex ne bouge pas.
///
/// Modules (RUN-ONLY) :
/// - ownedModuleIdsInRun : inventaire "owned" de la run (achats, gains, etc.)
/// - equippedModuleIds : équipement de la run
/// - unlockedModuleSlotsInRun : slots débloqués pendant la run
/// - shopOfferModuleIds : offres du shop pendant la run
/// </summary>
[Serializable]
public class RunStateData
{
    // ------------------------------------------------------------
    // IDENTITÉ DE RUN (anti-triche / anti double-commit)
    // ------------------------------------------------------------
    public string runId = "";
    public bool hasOngoingRun = false;

    // ------------------------------------------------------------
    // PROGRESSION PAR NODES (source de vérité)
    // ------------------------------------------------------------
    public string worldId = "W1";
    public int currentNodeIndex = 0;
    public string currentShipId = "CORE_SCOUT";

    // ------------------------------------------------------------
    // ÉTAT DE RUN (ressources)
    // ------------------------------------------------------------
    public int remainingHullInRun = 0;

    // IMPORTANT: par design, une new run force 3.
    public int remainingContractLives = 3;

    public int currentRunScore = 0;
    public int nodesClearedInRun = 0;

    // ------------------------------------------------------------
    // PÉNALITÉ D'ABANDON (quit sauvage = Hull -X)
    // ------------------------------------------------------------
    public bool levelInProgress = false;
    public bool abortPenaltyArmed = false;

    public bool pendingAbortHullPenaltyFeedback = false;
    public int lastAbortHullPenaltyAmount = 0;
    public bool pendingGameOverFromAbort = false;

    // ------------------------------------------------------------
    // END TOKEN (anti double-commit / reprise fin de niveau)
    // ------------------------------------------------------------
    public bool hasPendingEndToken = false;
    public EndLevelToken pendingEndToken;
    public bool pendingEndTokenCommitted = false;

    // ------------------------------------------------------------
    // END SNAPSHOT (replay cérémonie + commit crash-safe)
    // ------------------------------------------------------------
    public bool hasPendingEndSnapshot = false;
    public EndLevelSnapshot pendingEndSnapshot = null;

    // ------------------------------------------------------------
    // MODULES OWNED (RUN-ONLY)
    // ------------------------------------------------------------
    public List<string> ownedModuleIdsInRun = new List<string>();

    // ------------------------------------------------------------
    // EQUIPMENT (persisté tant que la run est en cours)
    // ------------------------------------------------------------
    public int unlockedModuleSlotsInRun = 0;   // 0 = non initialisé
    public string[] equippedModuleIds;

    // ------------------------------------------------------------
    // SHOP OFFERS (persisté tant que la run est en cours)
    // ------------------------------------------------------------
    public string[] shopOfferModuleIds;

    /// <summary>
    /// Indique si une offre de shop a deja ete generee pour ce node.
    ///
    /// IMPORTANT :
    /// - false => aucune offre n a encore ete generee -> on doit deal
    /// - true  => une offre a deja ete generee (meme si elle est maintenant vide)
    ///
    /// Permet de distinguer :
    /// - "shop jamais initialisé"
    /// - "shop deja consommé (tous les modules achetés)"
    ///
    /// Reset à false :
    /// - au debut d une nouvelle run
    /// - lors d un reroll
    /// </summary>
    public bool shopOfferInitialized = false;
    public int shopOfferNodeIndex = -1;

    // ------------------------------------------------------------
    // SHOP REROLL (pour fixer le prix du reroll)
    // ------------------------------------------------------------
    public int shopRerollCount = 0;

    // ------------------------------------------------------------
    // 
    // ------------------------------------------------------------
    public int bonusHullMaxInRun = 0;
}
