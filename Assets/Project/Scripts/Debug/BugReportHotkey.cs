// Chemin recommandé : Scripts/Debug/BugReportHotkey.cs

using UnityEngine;

/// <summary>
/// Permet d'ouvrir le formulaire de bug lorsque le joueur appuie sur F8.
/// Utilisé uniquement pour les builds alpha afin de faciliter les retours joueurs.
/// </summary>
public class BugReportHotkey : MonoBehaviour
{
    [SerializeField] private string bugReportUrl = "https://docs.google.com/forms/d/e/1FAIpQLScI0X0k4HxpYQb82TRNdslM2GdZgyse8ni0O1dMkCFIssNZCA/viewform?usp=publish-editor";

    void Update()
    {
        // Détection de la touche F8
        if (Input.GetKeyDown(KeyCode.F8))
        {
            OpenBugReport();
        }
    }

    /// <summary>
    /// Ouvre le formulaire de bug dans le navigateur par défaut.
    /// </summary>
    void OpenBugReport()
    {
        Application.OpenURL(bugReportUrl);
    }
}