using UnityEngine;

/// <summary>
/// Composition root minimal pour lancer Main en standalone.
/// Cree dynamiquement BootRoot + SaveManager si absents.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class MainStandaloneInstaller : MonoBehaviour
{
    private void Awake()
    {
        // Si BootRoot existe deja (flow normal), on ne fait rien.
        if (BootRoot.Instance != null)
            return;

        // 1) BootRoot
        var bootGO = new GameObject("[Standalone] BootRoot");
        bootGO.AddComponent<BootRoot>();

        // 2) SaveManager
        if (SaveManager.Instance == null)
        {
            var saveGO = new GameObject("[Standalone] SaveManager");
            saveGO.AddComponent<SaveManager>();
            // SaveManager.Awake() => Instance + Load() => Current pret
        }
    }
}
