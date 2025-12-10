using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Detecte une sequence de taps rapides sur le logo du Title pour activer le mode debug Main.
/// Optionnellement, peut charger directement la scene Main apres activation.
/// </summary>
public class DebugLogoActivator : MonoBehaviour
{
    [Header("Config du tap secret")]
    [SerializeField] private int tapsToActivate = 4;
    [SerializeField] private float maxDelayBetweenTaps = 0.6f;

    [Header("Action a l activation")]
    [SerializeField] private ToggleMainDebugButton toggleDebug;
    [SerializeField] private bool loadMainSceneOnActivate = true;
    [SerializeField] private string mainSceneName = "Main";

    private int tapCount = 0;
    private float lastTapTime = 0f;

    /// <summary>
    /// A appeler depuis l OnClick du Button sur le logo.
    /// </summary>
    public void OnLogoClicked()
    {
        float t = Time.time;

        // Si trop de temps s est ecoule depuis le dernier tap, on reset la sequence.
        if (t - lastTapTime > maxDelayBetweenTaps)
        {
            tapCount = 0;
        }

        tapCount++;
        lastTapTime = t;

        if (tapCount >= tapsToActivate)
        {
            // 1) On toggle le flag debug
            if (toggleDebug != null)
            {
                toggleDebug.ToggleMainDebug();
            }
            else
            {
                Debug.LogWarning("[DebugLogoActivator] Aucun ToggleMainDebugButton assigne.");
            }

            // 2) Optionnel : on charge la scene Main directement
            if (loadMainSceneOnActivate && !string.IsNullOrEmpty(mainSceneName))
            {
                Debug.Log("[DebugLogoActivator] Chargement de la scene " + mainSceneName + " apres activation du debug.");
                SceneManager.LoadScene(mainSceneName);
            }

            // On reset la sequence apres activation.
            tapCount = 0;
        }
    }
}
