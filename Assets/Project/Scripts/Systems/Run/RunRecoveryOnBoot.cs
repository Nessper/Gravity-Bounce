using UnityEngine;

/// <summary>
/// Vérifie au démarrage s'il y avait un level en cours au moment où le jeu a été fermé.
/// Si levelInProgress et abortPenaltyArmed sont true,
/// on retire une vie (comme une défaite automatique).
/// Si plus de vies, la run est terminée (hasOngoingRun = false),
/// mais la sauvegarde (profil, vaisseaux débloqués, etc.) n'est pas effacée.
/// </summary>
[DefaultExecutionOrder(-100)]
public class RunRecoveryOnBoot : MonoBehaviour
{
    private void Start()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var run = SaveManager.Instance.Current.runState;
        if (run == null)
            return;

        // Pas de run active => rien à faire
        if (!run.hasOngoingRun)
            return;

        // Si le level n'était pas marqué comme en cours ou que la pénalité n'était pas armée,
        // on considère que tout s'est terminé proprement (victoire/défaite/retour menu).
        if (!run.levelInProgress || !run.abortPenaltyArmed)
            return;

        // Ici : on a détecté une "abandon" de level.
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        if (run.remainingHullInRun > 0)
        {
            run.remainingHullInRun = Mathf.Max(0, run.remainingHullInRun - 1);
        }

        if (run.remainingHullInRun <= 0)
        {
            run.remainingHullInRun = 0;
            run.hasOngoingRun = false;

            Debug.Log("[RunRecoveryOnBoot] Aborted level detected. Life lost -> RUN ENDED (no more lives).");
        }
        else
        {
            Debug.Log("[RunRecoveryOnBoot] Aborted level detected. Life lost. Remaining lives = "
                      + run.remainingHullInRun);
        }

        SaveManager.Instance.Save();
    }
}
