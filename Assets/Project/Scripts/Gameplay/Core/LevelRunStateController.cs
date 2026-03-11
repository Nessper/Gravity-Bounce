using UnityEngine;

/// <summary>
/// Controleur de liaison entre le niveau et la persistance de run.
/// - Marque le niveau comme "en cours" dans la sauvegarde.
/// - Arme le flag abortPenaltyArmed pour la regle "quit = defaite".
/// - Expose un etat runtime pour savoir si le gameplay reel a demarre.
/// 
/// Convention: source de vérité = SaveManager.Current.runState.
/// </summary>
public class LevelRunStateController : MonoBehaviour
{
    // Etat runtime : true uniquement pendant le gameplay reel (pas briefing/intro).
    public bool GameplayArmed { get; private set; }

    /// <summary>
    /// Proxy de runId (utile pour tokens / anti double commit / logs).
    /// </summary>
    public string RunId
    {
        get
        {
            if (SaveManager.Instance == null || SaveManager.Instance.Current == null || SaveManager.Instance.Current.runState == null)
                return "debug";
            return string.IsNullOrEmpty(SaveManager.Instance.Current.runState.runId) ? "debug" : SaveManager.Instance.Current.runState.runId;
        }
    }

    /// <summary>
    /// Proxy du currentNodeIndex (index du node A JOUER MAINTENANT).
    /// </summary>
    public int CurrentNodeIndex
    {
        get
        {
            if (SaveManager.Instance == null || SaveManager.Instance.Current == null || SaveManager.Instance.Current.runState == null)
                return -1;
            return SaveManager.Instance.Current.runState.currentNodeIndex;
        }
    }

    /// <summary>
    /// A appeler au moment ou le gameplay commence vraiment. Dans LevelManager.StartLevel().
    /// </summary>
    public void MarkLevelStarted()
    {
        GameplayArmed = true;

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[LevelRunStateController] SaveManager absent, impossible de marquer le niveau comme demarre.");
            return;
        }

        SaveManager.Instance.MarkLevelStartedInRun();
        Debug.Log("[LevelRunStateController] Level started -> flags runState armes.");
    }

    /// <summary>
    /// A appeler au moment ou le gameplay fini vraiment. Dans EndSequenceController / fin de niveau.
    /// </summary>
    public void MarkLevelEnded()
    {
        GameplayArmed = false;

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[LevelRunStateController] SaveManager absent, impossible de marquer la fin du niveau.");
            return;
        }

        SaveManager.Instance.MarkLevelEndedNormally();
        Debug.Log("[LevelRunStateController] Level ended -> flags runState desarmes.");
    }
}
