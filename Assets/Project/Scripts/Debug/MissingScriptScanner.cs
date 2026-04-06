using UnityEngine;
using UnityEngine.SceneManagement;

public class MissingScriptScanner : MonoBehaviour
{
    [ContextMenu("Scan Active Scene For Missing Scripts")]
    public void ScanActiveSceneForMissingScripts()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        int totalMissing = 0;

        foreach (GameObject root in roots)
        {
            totalMissing += ScanRecursive(root, root.name);
        }

        Debug.Log("[MissingScriptScanner] Total missing scripts in scene '" + scene.name + "': " + totalMissing);
    }

    private int ScanRecursive(GameObject go, string path)
    {
        int count = 0;

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.LogWarning("[MissingScriptScanner] Missing script on: " + path);
                count++;
            }
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            Transform child = go.transform.GetChild(i);
            count += ScanRecursive(child.gameObject, path + "/" + child.name);
        }

        return count;
    }
}