#if UNITY_EDITOR
// Chemin recommandé (projet Unity) : Scripts/Debug/Editor/MainDebugStarterV3Editor.cs

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MainDebugStarterV3))]
public class MainDebugStarterV3Editor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1) Inspector normal (tous tes champs)
        DrawDefaultInspector();

        MainDebugStarterV3 t = (MainDebugStarterV3)target;

        // 2) Node Picker (ton code existant)
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Node Picker (WorldCatalog)", EditorStyles.boldLabel);

        string worldId = GetPrivateString(t, "debugWorldId");
        if (string.IsNullOrEmpty(worldId))
        {
            EditorGUILayout.HelpBox("debugWorldId is empty.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        WorldCatalogService.WorldEntry world;
        if (!WorldCatalogService.TryGetWorld(worldId, out world) || world.levelIds == null || world.levelIds.Length == 0)
        {
            EditorGUILayout.HelpBox("WorldCatalogService has no world/levels for worldId=" + worldId + ".", MessageType.Warning);
            EditorGUILayout.LabelField("Tip: Open Boot once to initialize catalogs, or ensure WorldCatalogService is editor-ready.");
            serializedObject.ApplyModifiedProperties();
            return;
        }

        int currentIndex = GetPrivateInt(t, "debugNodeIndex");
        currentIndex = Mathf.Clamp(currentIndex, 0, world.levelIds.Length - 1);

        int newIndex = EditorGUILayout.Popup("Start Level (nodeIndex)", currentIndex, world.levelIds);
        if (newIndex != currentIndex)
        {
            SetPrivateInt(t, "debugNodeIndex", newIndex);
            EditorUtility.SetDirty(t);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private string GetPrivateString(object obj, string fieldName)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? (string)f.GetValue(obj) : "";
    }

    private int GetPrivateInt(object obj, string fieldName)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? (int)f.GetValue(obj) : 0;
    }

    private void SetPrivateInt(object obj, string fieldName, int value)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(obj, value);
    }


}
#endif
