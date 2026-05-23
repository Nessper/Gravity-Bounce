using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneShortcuts
{
    // F1 = Boot
    [MenuItem("Tools/Scenes/Boot _F1")]
    public static void OpenBoot()
    {
        OpenScene("Assets/Project/Scenes/Boot.unity");
    }

    // F2 = Main
    [MenuItem("Tools/Scenes/Main _F2")]
    public static void OpenMain()
    {
        OpenScene("Assets/Project/Scenes/Main.unity");
    }

    // F3 = DebugLauncher
    [MenuItem("Tools/Scenes/DebugLauncher _F3")]
    public static void OpenDebugLauncher()
    {
        OpenScene("Assets/Project/Scenes/DebugLauncher.unity");
    }

    // F4 = Title
    [MenuItem("Tools/Scenes/Title _F4")]
    public static void OpenTitle()
    {
        OpenScene("Assets/Project/Scenes/Title.unity");
    }

    // F5 = ShipSelect
    [MenuItem("Tools/Scenes/ShipSelect _F5")]
    public static void OpenShipSelect()
    {
        OpenScene("Assets/Project/Scenes/ShipSelect.unity");
    }

    // F6 = RunHub
    [MenuItem("Tools/Scenes/RunHub _F6")]
    public static void OpenRunHub()
    {
        OpenScene("Assets/Project/Scenes/RunHub.unity");
    }

    // F7 = Credits
    [MenuItem("Tools/Scenes/Credits _F7")]
    public static void OpenCredits()
    {
        OpenScene("Assets/Project/Scenes/CreditsScene.unity");
    }

    private static void OpenScene(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogError("[SceneShortcuts] Scene introuvable : " + path);
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path);
        }
    }
}