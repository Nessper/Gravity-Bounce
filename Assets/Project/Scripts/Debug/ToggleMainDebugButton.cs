using UnityEngine;

/// <summary>
/// Gère le flag PlayerPrefs qui active le mode MainDebug sur device.
/// </summary>
public class ToggleMainDebugButton : MonoBehaviour
{
    private const string PlayerPrefKey = "VS_DEBUG_MAIN";

    /// <summary>
    /// Inverse la valeur du flag VS_DEBUG_MAIN (0 -> 1, 1 -> 0).
    /// </summary>
    public void ToggleMainDebug()
    {
        int current = PlayerPrefs.GetInt(PlayerPrefKey, 0);
        int next = (current == 0) ? 1 : 0;

        PlayerPrefs.SetInt(PlayerPrefKey, next);
        PlayerPrefs.Save();

        Debug.Log("[ToggleMainDebugButton] VS_DEBUG_MAIN = " + next);
    }

    /// <summary>
    /// Permet de savoir si le debug Main est actuellement actif (utile si tu veux afficher un etat).
    /// </summary>
    public static bool IsDebugMainEnabled()
    {
        return PlayerPrefs.GetInt(PlayerPrefKey, 0) == 1;
    }
}
