using UnityEngine;

public class EndLevel : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private GameObject overlayPanel;  // panneau de fin de niveau
    [SerializeField] private EndLevelUI endLevelUI;    // script d'affichage (texte TMP)

    private void Awake()
    {
        // Masquer l'overlay au démarrage
        if (overlayPanel != null)
            overlayPanel.SetActive(false);
    }

    /// <summary>
    /// Active le panneau de fin et affiche les résultats du niveau.
    /// </summary>
    public void ShowResult(bool success, float achievedPercent, float targetPercent, EndLevelStats stats = null)
    {
        if (overlayPanel != null)
            overlayPanel.SetActive(true);

        if (endLevelUI != null)
        {
            // On passe les stats complètes à l'UI
            endLevelUI.DisplayResult(success, achievedPercent, targetPercent, stats);
        }
        else
        {
            Debug.LogWarning("[EndLevel] Aucun EndLevelUI assigné dans l'inspecteur.");
        }

        Debug.Log($"[EndLevel] Fin du niveau -> {(success ? "réussi" : "échoué")} ({achievedPercent:0.0}% / {targetPercent}%)");
    }

    /// <summary>
    /// Cache le panneau de fin et nettoie les textes.
    /// </summary>
    public void Hide()
    {
        if (overlayPanel != null)
            overlayPanel.SetActive(false);

        endLevelUI?.Clear();
    }
}
