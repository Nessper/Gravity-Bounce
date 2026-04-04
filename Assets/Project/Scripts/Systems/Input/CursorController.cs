using UnityEngine;

/// <summary>
/// Gestion centralisée du curseur (lock / unlock).
/// Evite les états incohérents et garantit un reset propre.
/// </summary>
public static class CursorController
{
    private static bool isLocked;

    /// <summary>
    /// A appeler au démarrage du jeu (Boot).
    /// Garantit un état propre quoi qu'il se soit passé avant.
    /// </summary>
    public static void Initialize()
    {
        ForceUnlock();
    }

    /// <summary>
    /// Lock le curseur (mode gameplay).
    /// </summary>
    public static void Lock()
    {
        if (isLocked)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isLocked = true;
    }

    /// <summary>
    /// Unlock propre (menus, pause, etc).
    /// </summary>
    public static void Unlock()
    {
        if (!isLocked)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isLocked = false;
    }

    /// <summary>
    /// Force un unlock (sécurité).
    /// A utiliser en OnDisable / OnDestroy / Boot.
    /// </summary>
    public static void ForceUnlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isLocked = false;
    }

    /// <summary>
    /// Debug / état actuel.
    /// </summary>
    public static bool IsLocked => isLocked;
}