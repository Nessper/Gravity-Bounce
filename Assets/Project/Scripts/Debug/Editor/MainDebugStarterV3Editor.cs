#if UNITY_EDITOR
// Chemin recommandé (projet Unity) : Scripts/Debug/Editor/MainDebugStarterV3Editor.cs

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector de MainDebugStarterV3.
/// 
/// Rôles :
/// - Afficher l'inspector standard.
/// - Proposer un Ship Picker basé sur ShipCatalog.
/// - Proposer un Node Picker basé sur WorldCatalog.
/// - Proposer un Debug Modules Picker (3 slots) basé sur ModuleCatalog.
/// 
/// Important :
/// - Le runtime stocke les vrais moduleId dans debugEquippedModuleIds.
/// - L'editor sert uniquement au confort de sélection (family + tier).
/// - Les doublons de famille entre slots sont interdits.
/// </summary>
[CustomEditor(typeof(MainDebugStarterV3))]
public class MainDebugStarterV3Editor : Editor
{
    private SerializedProperty debugWorldIdProp;
    private SerializedProperty debugNodeIndexProp;
    private SerializedProperty debugShipIdProp;
    private SerializedProperty debugEquippedModuleIdsProp;

    private void OnEnable()
    {
        debugWorldIdProp = serializedObject.FindProperty("debugWorldId");
        debugNodeIndexProp = serializedObject.FindProperty("debugNodeIndex");
        debugShipIdProp = serializedObject.FindProperty("debugShipId");
        debugEquippedModuleIdsProp = serializedObject.FindProperty("debugEquippedModuleIds");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        DrawShipPicker();
        DrawNodePicker();
        DrawModulesPicker();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawShipPicker()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Ship Picker (ShipCatalog)", EditorStyles.boldLabel);

        ShipCatalog catalog = LoadShipCatalog();
        if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "ShipCatalog introuvable ou vide à Resources/Ships/ShipCatalog.",
                MessageType.Warning);
            return;
        }

        string currentShipId = string.IsNullOrWhiteSpace(debugShipIdProp.stringValue)
            ? "CORE_SCOUT"
            : debugShipIdProp.stringValue;

        string[] shipIds = new string[catalog.ships.Count];
        int selectedIndex = 0;

        for (int i = 0; i < catalog.ships.Count; i++)
        {
            shipIds[i] = catalog.ships[i] != null ? catalog.ships[i].id : string.Empty;

            if (string.Equals(shipIds[i], currentShipId, StringComparison.Ordinal))
                selectedIndex = i;
        }

        int newIndex = EditorGUILayout.Popup("Ship", selectedIndex, shipIds);
        newIndex = Mathf.Clamp(newIndex, 0, Mathf.Max(0, shipIds.Length - 1));

        debugShipIdProp.stringValue = shipIds[newIndex];
    }

    private void DrawNodePicker()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Node Picker (WorldCatalog)", EditorStyles.boldLabel);

        string worldId = debugWorldIdProp != null ? debugWorldIdProp.stringValue : "";
        if (string.IsNullOrWhiteSpace(worldId))
        {
            EditorGUILayout.HelpBox("debugWorldId is empty.", MessageType.Warning);
            return;
        }

        WorldCatalogService.WorldEntry world;
        if (!WorldCatalogService.TryGetWorld(worldId, out world) ||
            world.levelIds == null ||
            world.levelIds.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "WorldCatalogService has no world/levels for worldId=" + worldId + ".",
                MessageType.Warning);
            EditorGUILayout.LabelField(
                "Tip: Open Boot once to initialize catalogs, or ensure WorldCatalogService is editor-ready.");
            return;
        }

        int currentIndex = Mathf.Clamp(
            debugNodeIndexProp != null ? debugNodeIndexProp.intValue : 0,
            0,
            world.levelIds.Length - 1);

        int newIndex = EditorGUILayout.Popup("Start Level (nodeIndex)", currentIndex, world.levelIds);
        debugNodeIndexProp.intValue = newIndex;
    }

    private void DrawModulesPicker()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Modules (Debug Loadout)", EditorStyles.boldLabel);

        if (debugEquippedModuleIdsProp == null)
        {
            EditorGUILayout.HelpBox("debugEquippedModuleIds property not found.", MessageType.Error);
            return;
        }

        EnsureDebugArraySize(debugEquippedModuleIdsProp, 3);

        if (!ModuleCatalogService.EnsureLoaded())
        {
            EditorGUILayout.HelpBox(
                "ModuleCatalog introuvable ou invalide. Impossible d'afficher le picker modules.",
                MessageType.Warning);
            return;
        }

        for (int slotIndex = 0; slotIndex < debugEquippedModuleIdsProp.arraySize; slotIndex++)
        {
            DrawModuleSlot(slotIndex);
            GUILayout.Space(4);
        }
    }

    private void DrawModuleSlot(int slotIndex)
    {
        SerializedProperty slotProp = debugEquippedModuleIdsProp.GetArrayElementAtIndex(slotIndex);

        string currentModuleId = slotProp.stringValue;
        ModuleDefinition currentDef = ModuleCatalogService.GetById(currentModuleId);

        string currentFamilyId = currentDef != null ? currentDef.familyId : string.Empty;
        int currentTier = currentDef != null ? currentDef.tier : 1;

        List<string> allowedFamilyIds = BuildAllowedFamilyIds(slotIndex, currentFamilyId);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Slot " + (slotIndex + 1), EditorStyles.boldLabel);

        List<string> familyOptions = new List<string> { "None" };
        familyOptions.AddRange(allowedFamilyIds);

        int currentFamilyPopupIndex = 0;
        if (!string.IsNullOrEmpty(currentFamilyId))
        {
            int familyFoundIndex = allowedFamilyIds.FindIndex(f => string.Equals(f, currentFamilyId, StringComparison.Ordinal));
            if (familyFoundIndex >= 0)
                currentFamilyPopupIndex = familyFoundIndex + 1;
        }

        int newFamilyPopupIndex = EditorGUILayout.Popup("Family", currentFamilyPopupIndex, familyOptions.ToArray());
        string selectedFamilyId = newFamilyPopupIndex <= 0 ? string.Empty : allowedFamilyIds[newFamilyPopupIndex - 1];

        if (string.IsNullOrEmpty(selectedFamilyId))
        {
            slotProp.stringValue = string.Empty;
            EditorGUILayout.LabelField("Tier", "—");
            EditorGUILayout.LabelField("Module Id", "Empty");
            EditorGUILayout.EndVertical();
            return;
        }

        List<int> tiers = ModuleCatalogService.GetAvailableTiersForFamily(selectedFamilyId);
        if (tiers == null || tiers.Count == 0)
        {
            slotProp.stringValue = string.Empty;
            EditorGUILayout.HelpBox("Aucun tier disponible pour la famille " + selectedFamilyId + ".", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        int currentTierPopupIndex = Mathf.Max(0, tiers.IndexOf(currentTier));
        if (!string.Equals(selectedFamilyId, currentFamilyId, StringComparison.Ordinal))
            currentTierPopupIndex = 0;

        string[] tierLabels = new string[tiers.Count];
        for (int i = 0; i < tiers.Count; i++)
            tierLabels[i] = "T" + tiers[i];

        int newTierPopupIndex = EditorGUILayout.Popup("Tier", currentTierPopupIndex, tierLabels);
        newTierPopupIndex = Mathf.Clamp(newTierPopupIndex, 0, tiers.Count - 1);

        int selectedTier = tiers[newTierPopupIndex];
        ModuleDefinition resolved = ModuleCatalogService.GetByFamilyAndTier(selectedFamilyId, selectedTier);

        if (resolved == null)
        {
            slotProp.stringValue = string.Empty;
            EditorGUILayout.HelpBox(
                "Impossible de résoudre un module pour familyId=" + selectedFamilyId + " tier=" + selectedTier + ".",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        slotProp.stringValue = resolved.id;

        EditorGUILayout.LabelField("Module Id", resolved.id);
        EditorGUILayout.LabelField("Display Name", string.IsNullOrEmpty(resolved.displayName) ? "—" : resolved.displayName);

        EditorGUILayout.EndVertical();
    }

    private List<string> BuildAllowedFamilyIds(int slotIndex, string currentFamilyId)
    {
        List<string> allFamilies = ModuleCatalogService.GetFamilyIds();
        HashSet<string> blockedFamilies = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < debugEquippedModuleIdsProp.arraySize; i++)
        {
            if (i == slotIndex)
                continue;

            string otherModuleId = debugEquippedModuleIdsProp.GetArrayElementAtIndex(i).stringValue;
            if (string.IsNullOrWhiteSpace(otherModuleId))
                continue;

            ModuleDefinition otherDef = ModuleCatalogService.GetById(otherModuleId);
            if (otherDef == null || string.IsNullOrWhiteSpace(otherDef.familyId))
                continue;

            blockedFamilies.Add(otherDef.familyId);
        }

        List<string> result = new List<string>();

        for (int i = 0; i < allFamilies.Count; i++)
        {
            string familyId = allFamilies[i];
            if (string.IsNullOrWhiteSpace(familyId))
                continue;

            if (blockedFamilies.Contains(familyId) &&
                !string.Equals(familyId, currentFamilyId, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(familyId);
        }

        return result;
    }

    private void EnsureDebugArraySize(SerializedProperty arrayProp, int expectedSize)
    {
        if (arrayProp == null || !arrayProp.isArray)
            return;

        if (arrayProp.arraySize != expectedSize)
            arrayProp.arraySize = expectedSize;
    }

    private ShipCatalog LoadShipCatalog()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Ships/ShipCatalog");
        if (jsonAsset == null)
            return null;

        try
        {
            return JsonUtility.FromJson<ShipCatalog>(jsonAsset.text);
        }
        catch
        {
            return null;
        }
    }
}
#endif