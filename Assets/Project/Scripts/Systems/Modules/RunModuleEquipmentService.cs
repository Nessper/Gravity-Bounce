// Chemin recommandé (projet Unity) : Scripts/Systems/Modules/RunModuleEquipmentService.cs

using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// RunModuleEquipmentService
///
/// Responsabilites :
/// - Appliquer les regles d equipement des modules.
/// - Valider ownership, prerequis de tier, exclusivite de famille.
/// - Equiper / desequiper / sanitiser l equipement de run.
/// - Expliquer les raisons d echec d equipement.
///
/// Important :
/// - La source de verite des slots reste RunSessionState.
/// - La persistance reste geree par RunSessionState / SaveManager.
/// - Ce service ne stocke pas d etat durable hors runtime.
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

    public bool TryEquipModuleToSlot(string moduleId, int slotIndex)
    {
        if (runSessionState == null)
            return false;

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (slotIndex < 0 || slotIndex >= runSessionState.EquipmentSlotCount)
            return false;

        if (runSessionState.IsEquipmentSlotLocked(slotIndex))
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

        UnequipOtherModulesInSameFamily(newDef, slotIndex);

        runSessionState.SetEquippedModuleIdRaw_Internal(slotIndex, moduleId);
        runSessionState.PushEquipmentToSave_Internal();
        runSessionState.NotifyEquipmentChanged_Internal();

        return true;
    }

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

    public void SanitizeEquippedModulesRuntime()
    {
        if (runSessionState == null)
            return;

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (!ModuleCatalogService.EnsureLoaded())
            return;

        bool changed = false;

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string id = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition def = ModuleCatalogService.GetById(id);
            if (def == null)
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }

            if (!IsOwnedRuntime(id))
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }

            if (!MeetsTierChainPrerequisites(def, out string missing))
            {
                runSessionState.SetEquippedModuleIdRaw_Internal(i, null);
                changed = true;
                continue;
            }
        }

        HashSet<string> seenFamilies = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string id = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(id))
                continue;

            ModuleDefinition def = ModuleCatalogService.GetById(id);
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

        return MeetsTierChainPrerequisites(def, out missingPrereqId);
    }

    public bool TryExplainEquipFailure(string moduleId, int slotIndex, out RunSessionState.EquipFailReason reason, out string missingPrereqId)
    {
        reason = RunSessionState.EquipFailReason.None;
        missingPrereqId = null;

        if (runSessionState == null)
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return true;
        }

        runSessionState.EnsureEquipmentInitialized_Internal();

        if (string.IsNullOrEmpty(moduleId))
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return true;
        }

        if (slotIndex < 0 || slotIndex >= runSessionState.EquipmentSlotCount || runSessionState.IsEquipmentSlotLocked(slotIndex))
        {
            reason = RunSessionState.EquipFailReason.SlotLocked;
            return true;
        }

        if (!ModuleCatalogService.EnsureLoaded())
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return true;
        }

        ModuleDefinition def = ModuleCatalogService.GetById(moduleId);
        if (def == null)
        {
            reason = RunSessionState.EquipFailReason.InvalidModule;
            return true;
        }

        if (!IsOwnedRuntime(moduleId))
        {
            reason = RunSessionState.EquipFailReason.NotOwned;
            return true;
        }

        if (!MeetsTierChainPrerequisites(def, out missingPrereqId))
        {
            reason = RunSessionState.EquipFailReason.MissingPrerequisite;
            return true;
        }

        reason = RunSessionState.EquipFailReason.None;
        return true;
    }

    private bool IsOwnedRuntime(string moduleId)
    {
        if (RunSessionState.DebugTreatAllModulesAsOwnedGlobal)
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