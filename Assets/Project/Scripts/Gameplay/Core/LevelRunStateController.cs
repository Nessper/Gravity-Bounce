using UnityEngine;

/// <summary>
/// Controleur de liaison entre le niveau et la persistance de run.
/// - Marque le niveau comme "en cours" dans la sauvegarde.
/// - Arme le flag abortPenaltyArmed pour la regle "quit = defaite".
/// 
/// Ce composant isole la logique SaveManager / runState hors du LevelManager.
/// </summary>
public class LevelRunStateController : MonoBehaviour
{
    /// <summary>
    /// A appeler au moment ou le gameplay commence vraiment
    /// (apres briefing, intro et countdown).
    /// </summary>
    public void MarkLevelStarted()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Current == null ||
            SaveManager.Instance.Current.runState == null)
        {
            Debug.LogWarning("[LevelRunStateController] Impossible de marquer le niveau comme demarre (pas de runState).");
            return;
        }

        var run = SaveManager.Instance.Current.runState;

        run.hasOngoingRun = true;
        run.levelInProgress = true;
        run.abortPenaltyArmed = true;

        SaveManager.Instance.Save();

        Debug.Log("[LevelRunStateController] Level started -> hasOngoingRun=true, levelInProgress=true, abortPenaltyArmed=true.");
    }
}
