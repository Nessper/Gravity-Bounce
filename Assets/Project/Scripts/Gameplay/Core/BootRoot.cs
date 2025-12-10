using UnityEngine;

/// <summary>
/// Racine des systèmes globaux du jeu.
/// Persiste entre les scènes et garantit un seul BootRoot.
/// </summary>
public class BootRoot : MonoBehaviour
{
    private static BootRoot instance;
    public static BootRoot Instance => instance;

    /// <summary>
    /// Accès global au GameFlowController.
    /// </summary>
    public static GameFlowController GameFlow { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Appelé par GameFlowController pour s'enregistrer.
    /// </summary>
    public static void RegisterGameFlow(GameFlowController controller)
    {
        if (GameFlow != null && GameFlow != controller)
        {
            Debug.LogWarning("[BootRoot] Multiple GameFlowController detected. Keeping the first one.");
            return;
        }

        GameFlow = controller;
    }
}
