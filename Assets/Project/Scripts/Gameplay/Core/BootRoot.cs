using UnityEngine;

/// <summary>
/// Racine des systèmes globaux du jeu.
/// Persiste entre les scènes et garantit un seul BootRoot.
/// </summary>
public class BootRoot : MonoBehaviour
{
    private static BootRoot instance;

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
}
