// Chemin recommandé (projet Unity) : Scripts/Systems/Modules/RunModuleEquipmentService.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service runtime responsable des regles d'equipement des modules.
///
/// Responsabilites :
/// - valider si un module peut etre equipe dans un slot
/// - verifier l'ownership runtime
/// - verifier les prerequis de tiers (T1 avant T2, etc.)
/// - garantir l'exclusivite par famille (un seul module d'une famille equipe a la fois)
/// - equiper / desequiper / nettoyer l'equipement invalide
/// - expliquer proprement les raisons d'un refus d'equipement
///
/// Important :
/// - la source de verite des slots reste RunSessionState
/// - la persistance reste geree par RunSessionState / SaveManager
/// - ce service ne stocke pas d'etat durable
/// - toute ecriture "brute" dans les slots doit passer par ce service
///   afin d'eviter qu'une UI contourne les regles metier
/// </summary>
public class RunModuleEquipmentService : MonoBehaviour
{
    public static RunModuleEquipmentService Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RunSessionState runSessionState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // ------------------------------------------------------------
    // API PUBLIQUE
    // ------------------------------------------------------------

    /// <summary>
    /// Tente d'equiper un module dans un slot donne.
    ///
    /// Regles appliquees :
    /// - module valide
    /// - slot valide et non verrouille
    /// - module possede runtime
    /// - prerequis de tiers respectes
    ///
    /// Si l'equipement est autorise :
    /// - on desequipe d'abord les autres modules de la meme famille
    /// - puis on ecrit le module dans le slot cible
    /// - puis on persiste et on notifie
    /// </summary>
    public bool TryEquipModuleToSlot(string moduleId, int slotIndex)
    {
        if (runSessionState == null)
            return false;

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (!TryExplainEquipFailure(
                moduleId,
                slotIndex,
                out RunSessionState.EquipFailReason reason,
                out string missingPrereqId))
        {
            return false;
        }

        ModuleDefinition newDef = ModuleCatalogService.GetById(moduleId);
        if (newDef == null)
            return false;

        UnequipOtherModulesInSameFamily(newDef, slotIndex);

        runSessionState.SetEquippedModuleIdRaw_Internal(slotIndex, moduleId);
        runSessionState.PushEquipmentToSave_Internal();
        runSessionState.NotifyEquipmentChanged_Internal();

        return true;
    }

    /// <summary>
    /// Tente de desequiper un slot.
    /// Retourne false si le slot est invalide ou deja vide.
    /// </summary>
    public bool UnequipSlot(int slotIndex)
    {
        if (runSessionState == null)
            return false;

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (slotIndex < 0 || slotIndex >= runSessionState.EquipmentSlotCount)
            return false;

        if (string.IsNullOrEmpty(runSessionState.GetEquippedModuleId(slotIndex)))
            return false;

        runSessionState.SetEquippedModuleIdRaw_Internal(slotIndex, null);
        runSessionState.PushEquipmentToSave_Internal();
        runSessionState.NotifyEquipmentChanged_Internal();

        return true;
    }

    /// <summary>
    /// Vide tous les slots equipes.
    /// Utile pour reset global, changement de contexte, debug, etc.
    /// </summary>
    public void ClearAllEquippedModules()
    {
        if (runSessionState == null)
            return;

        runSessionState.EnsureEquipmentInitialized_Internal();

        bool changed = false;

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            if (!string.IsNullOrEmpty(runSessionState.GetEquippedModuleId(i)))
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
            }
        }

        if (!changed)
            return;

        runSessionState.PushEquipmentToSave_Internal();
        runSessionState.NotifyEquipmentChanged_Internal();
    }

    /// <summary>
    /// Tente de debloquer un slot supplementaire pendant la run.
    ///
    /// Retourne false si tous les slots sont deja ouverts.
    /// </summary>
    public bool TryUnlockOneModuleSlotInRun()
    {
        if (runSessionState == null)
            return false;

        int open = runSessionState.GetOpenSlotCountEffective_Internal();
        if (open >= runSessionState.EquipmentSlotCount)
            return false;

        int newOpen = open + 1;
        runSessionState.SetUnlockedSlotsInRunToSave_Internal(newOpen);
        runSessionState.NotifyEquipmentChanged_Internal();

        return true;
    }

    /// <summary>
    /// Nettoie l'equipement runtime si des incoherences sont detectees.
    ///
    /// Cas geres :
    /// - module introuvable dans le catalog
    /// - module non possede
    /// - prerequis de tiers non respectes
    /// - doublon de famille
    ///
    /// Cette methode est defensive.
    /// Elle sert surtout au chargement, restore ou migration de donnees.
    /// </summary>
    public void SanitizeEquippedModulesRuntime()
    {
        if (runSessionState == null)
            return;

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (!ModuleCatalogService.EnsureLoaded())
            return;

        bool changed = false;

        // --------------------------------------------------------
        // 1) Validation unitaire de chaque slot
        // --------------------------------------------------------
        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
            if (def == null)
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }

            if (!IsOwnedRuntime(moduleId))
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }

            if (!MeetsTierChainPrerequisites(def, out string missingPrereqId))
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }
        }

        // --------------------------------------------------------
        // 2) Verification de l'exclusivite par famille
        // On garde le premier rencontre, on retire les suivants.
        // --------------------------------------------------------
        HashSet<string> seenFamilies = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
            if (def == null || string.IsNullOrEmpty(def.familyId))
                continue;

            if (seenFamilies.Contains(def.familyId))
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }

            seenFamilies.Add(def.familyId);
        }

        if (!changed)
            return;

        runSessionState.PushEquipmentToSave_Internal();
        runSessionState.NotifyEquipmentChanged_Internal();
    }

    /// <summary>
    /// Retourne true s'il manque un prerequis de tier pour ce module.
    ///
    /// Exemple :
    /// - on tente d'utiliser un T2
    /// - le T1 n'est pas possede
    /// => retourne true et remplit missingPrereqId
    ///
    /// Attention :
    /// - true  = il manque quelque chose
    /// - false = aucun prerequis manquant detecte
    /// </summary>
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

        bool meetsPrereqs = MeetsTierChainPrerequisites(def, out missingPrereqId);
        return !meetsPrereqs;
    }

    /// <summary>
    /// Explique si l'equipement est possible ou non.
    ///
    /// Convention importante :
    /// - true  = l'equipement est autorise
    /// - false = l'equipement doit etre refuse
    ///
    /// En cas de false :
    /// - reason indique la cause principale
    /// - missingPrereqId est renseigne si pertinent
    /// </summary>
    public bool TryExplainEquipFailure(
        string moduleId,
        int slotIndex,
        out RunSessionState.EquipFailReason reason,
        out string missingPrereqId)
    {
        reason = RunSessionState.EquipFailReason.None;
        missingPrereqId = null;

        if (runSessionState == null)
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return false;
        }

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (string.IsNullOrEmpty(moduleId))
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return false;
        }

        if (slotIndex < 0 || slotIndex >= runSessionState.EquipmentSlotCount)
        {
            reason = RunSessionState.EquipFailReason.SlotLocked;
            return false;
        }

        if (runSessionState.IsEquipmentSlotLocked(slotIndex))
        {
            reason = RunSessionState.EquipFailReason.SlotLocked;
            return false;
        }

        if (!ModuleCatalogService.EnsureLoaded())
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return false;
        }

        ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
        if (def == null)
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return false;
        }

        if (!IsOwnedRuntime(moduleId))
        {
            reason = RunSessionState.EquipFailReason.NotOwned;
            return false;
        }

        if (!MeetsTierChainPrerequisites(def, out missingPrereqId))
        {
            reason = RunSessionState.EquipFailReason.MissingPrerequisite;
            return false;
        }

        reason = RunSessionState.EquipFailReason.None;
        return true;
    }

    // ------------------------------------------------------------
    // HELPERS INTERNES
    // ------------------------------------------------------------

    /// <summary>
    /// Indique si un module est possede dans le contexte runtime actuel.
    /// Cette verif doit refleter l'etat reel de la run, sans override debug global.
    /// </summary>
    private bool IsOwnedRuntime(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        return SaveManager.Instance.HasOwnedModule(moduleId);
    }

    /// <summary>
    /// Verifie si les prerequis de tiers sont respectes pour un module.
    ///
    /// Regle :
    /// - pour equiper un T2, il faut posseder le T1
    /// - pour equiper un T3, il faut posseder T1 + T2
    ///
    /// Si un tier precedent n'existe pas dans le catalog,
    /// on considere les donnees comme invalides et on refuse aussi.
    /// </summary>
    private bool MeetsTierChainPrerequisites(ModuleDefinition def, out string missingPrereqId)
    {
        missingPrereqId = null;

        if (def == null)
            return false;

        int tier = Mathf.Max(1, def.tier);

        if (tier <= 1)
            return true;

        if (string.IsNullOrEmpty(def.familyId))
            return false;

        List<ModuleDefinition> modules =
            ModuleCatalogService.Catalog != null ? ModuleCatalogService.Catalog.modules : null;

        if (modules == null)
            return false;

        for (int requiredTier = 1; requiredTier < tier; requiredTier++)
        {
            ModuleDefinition prereq = modules.Find(m =>
                m != null &&
                string.Equals(m.familyId, def.familyId, StringComparison.Ordinal) &&
                m.tier == requiredTier);

            if (prereq == null)
            {
                missingPrereqId = def.familyId + "_T" + requiredTier;
                Debug.LogError(
                    "[RunModuleEquipmentService] Catalog invalide : prerequis introuvable pour module=" +
                    def.id + " familyId=" + def.familyId + " requiredTier=" + requiredTier);
                return false;
            }

            if (!IsOwnedRuntime(prereq.id))
            {
                missingPrereqId = prereq.id;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Retire les autres modules de la meme famille que le module cible.
    ///
    /// But :
    /// - garantir qu'une seule version d'une famille soit equipee a la fois
    /// - eviter par exemple d'avoir T1 et T2 equipes en meme temps
    ///
    /// Le slot cible est ignore : on ne retire pas ce qu'on est en train
    /// d'equiper dans ce slot.
    /// </summary>
    private void UnequipOtherModulesInSameFamily(ModuleDefinition targetDef, int targetSlotIndex)
    {
        if (targetDef == null || string.IsNullOrEmpty(targetDef.familyId))
            return;

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            if (i == targetSlotIndex)
                continue;

            string otherId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(otherId))
                continue;

            ModuleDefinition otherDef = ModuleCatalogService.GetById(otherId);
            if (otherDef == null)
                continue;

            if (string.Equals(otherDef.familyId, targetDef.familyId, StringComparison.Ordinal))
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
        }
    }
}