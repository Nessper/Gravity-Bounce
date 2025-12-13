using UnityEngine;

/// <summary>
/// Initialise l'etat de run pour la scene Main a partir des donnees persistantes.
/// - Synchronise Hull (remainingHullInRun -> RunSessionState)
/// - Synchronise vies de contrat (remainingContractLives -> RunSessionState)
/// - Gere le flag keepCurrentHullOnNextRestart (Retry apres DEFEAT)
/// - Si run invalide, retour au Title via GameFlow
///
/// IMPORTANT :
/// - Ce composant NE marque PLUS levelInProgress.
///   C'est LevelRunStateController (au vrai debut du gameplay) qui doit appeler MarkLevelStarted().
/// </summary>
public class RunSessionBootstrapper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RunSessionState runSessionState;

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[RunSessionBootstrapper] RunSessionState manquant.");
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[RunSessionBootstrapper] SaveManager ou GameSaveData introuvable. Retour au Title.");
            SafeReturnToTitle();
            return;
        }

        RunStateData run = SaveManager.Instance.GetRunState();

        if (!IsRunStateValid(run))
        {
            Debug.LogError("[RunSessionBootstrapper] RunState invalide ou pas de run en cours. Retour au Title.");
            SafeReturnToTitle();
            return;
        }

        // Vies de contrat (avec correction auto si valeur invalide)
        int contractLives = ResolveContractLivesFromRun(run);

        // CAS 1 : retry avec conservation du hull (flag consomme ici).
        if (runSessionState.ConsumeKeepFlag())
        {
            int currentHull = Mathf.Max(0, runSessionState.Hull);

            // IMPORTANT : InitHull persiste (via PersistHull -> SaveManager.SetRemainingHullInRun)
            runSessionState.InitHull(currentHull);
            runSessionState.InitContractLives(contractLives);

            Debug.Log("[RunSessionBootstrapper] Retry avec hull conserve: hull=" + currentHull
                      + " | contractLives=" + contractLives);
            return;
        }

        // CAS 2 : run persistante classique -> hull depuis la save.
        int hullFromSave = Mathf.Max(0, run.remainingHullInRun);

        runSessionState.InitHull(hullFromSave);
        runSessionState.InitContractLives(contractLives);

        Debug.Log("[RunSessionBootstrapper] Hull depuis save: " + hullFromSave
                  + " | contractLives=" + contractLives
                  + " (LevelId=" + run.currentLevelId + ")");
    }

    /// <summary>
    /// Verifie que l etat de run contient les informations minimales
    /// pour pouvoir lancer un niveau.
    /// </summary>
    private bool IsRunStateValid(RunStateData run)
    {
        if (run == null)
        {
            Debug.LogWarning("[RunSessionBootstrapper] runState est null.");
            return false;
        }

        if (!run.hasOngoingRun)
        {
            Debug.LogWarning("[RunSessionBootstrapper] hasOngoingRun est false.");
            return false;
        }

        if (string.IsNullOrEmpty(run.currentShipId))
        {
            Debug.LogWarning("[RunSessionBootstrapper] currentShipId est vide.");
            return false;
        }

        if (string.IsNullOrEmpty(run.currentLevelId))
        {
            Debug.LogWarning("[RunSessionBootstrapper] currentLevelId est vide.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Lit les vies de contrat depuis la run.
    /// Regle :
    /// - Si valeur invalide (<= 0), force 3 et sauvegarde.
    /// - Sinon clamp 0..3.
    /// </summary>
    private int ResolveContractLivesFromRun(RunStateData run)
    {
        if (run == null)
            return 3;

        int lives = run.remainingContractLives;

        if (lives <= 0)
        {
            lives = 3;
            run.remainingContractLives = 3;

            if (SaveManager.Instance != null)
                SaveManager.Instance.Save();
        }

        return Mathf.Clamp(lives, 0, 3);
    }

    /// <summary>
    /// Retourne proprement vers la scene Title en passant par GameFlow.
    /// </summary>
    private void SafeReturnToTitle()
    {
        if (BootRoot.GameFlow != null)
        {
            BootRoot.GameFlow.GoToTitle();
        }
        else
        {
            Debug.LogError("[RunSessionBootstrapper] BootRoot.GameFlow est null. Impossible de retourner au Title.");
        }
    }
}
