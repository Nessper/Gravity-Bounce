using UnityEngine;

/// <summary>
/// Initialise l etat de run pour la scene Main a partir des donnees persistantes
/// et de la configuration actuelle.
/// - Synchronise le Hull du vaisseau (remainingHullInRun -> RunSessionState)
/// - Synchronise les vies de contrat (remainingContractLives -> RunSessionState)
/// - Gere le flag "keepCurrentHullOnNextRestart" (Retry apres DEFEAT)
/// - Marque le level comme "en cours" (levelInProgress = true) pour la regle "quit = defaite".
/// - Si aucune run valide n est disponible, renvoie vers la scene Title via GameFlow.
/// </summary>
public class RunSessionBootstrapper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RunSessionState runSessionState;

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[RunSessionBootstrapper] Missing RunSessionState reference.");
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled)
            return;

        // Verification basique de la presence de SaveManager et de la save courante.
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[RunSessionBootstrapper] SaveManager ou GameSaveData introuvable. Retour au Title.");
            SafeReturnToTitle();
            return;
        }

        GameSaveData save = SaveManager.Instance.Current;
        RunStateData run = save.runState;

        // On refuse de lancer Main sans run persistante valide.
        if (!IsRunStateValid(run))
        {
            Debug.LogError("[RunSessionBootstrapper] RunState invalide ou aucune run en cours. Retour au Title.");
            SafeReturnToTitle();
            return;
        }

        // A partir d ici, on sait que run != null, hasOngoingRun == true, et que les ids sont renseignes.
        int contractLives = ResolveContractLivesFromRun(run);

        // CAS 1 : retry avec conservation du hull (flag consomme ici).
        if (runSessionState.ConsumeKeepFlag())
        {
            int currentHull = Mathf.Max(0, runSessionState.Hull);
            runSessionState.InitHull(currentHull);
            runSessionState.InitContractLives(contractLives);

            MarkLevelInProgress(run);

            Debug.Log("[RunSessionBootstrapper] Retry avec hull conserve: hull=" + currentHull
                      + " | contractLives=" + contractLives);
            return;
        }

        // CAS 2 : run persistante classique -> hull depuis la save.
        int hullFromSave = Mathf.Max(0, run.remainingHullInRun);
        runSessionState.InitHull(hullFromSave);
        runSessionState.InitContractLives(contractLives);

        MarkLevelInProgress(run);

        Debug.Log("[RunSessionBootstrapper] Hull depuis la run persistante: " + hullFromSave
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
            Debug.LogWarning("[RunSessionBootstrapper] hasOngoingRun est false, pas de run en cours.");
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
    /// Marque le level comme "en cours" dans la run et sauvegarde.
    /// Utilise pour la regle "quit = defaite".
    /// </summary>
    private void MarkLevelInProgress(RunStateData run)
    {
        if (run == null)
            return;

        run.levelInProgress = true;

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

    /// <summary>
    /// Lit les vies de contrat depuis la run.
    /// Comportement :
    /// - Si remainingContractLives <= 0 -> force 3, ecrit en save et clamp
    /// - Sinon -> clamp entre 0 et 3
    /// </summary>
    private int ResolveContractLivesFromRun(RunStateData run)
    {
        if (run == null)
            return 3;

        int lives = run.remainingContractLives;

        // Si la valeur n a jamais ete initialisee ou est invalide,
        // on force 3 comme valeur par defaut pour le contrat.
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
    /// Si GameFlow est indisponible, loggue simplement une erreur.
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
