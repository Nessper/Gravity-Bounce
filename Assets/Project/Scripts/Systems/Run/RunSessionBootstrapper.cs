using UnityEngine;

/// <summary>
/// Initialise l'état de run pour la scène Main à partir des données persistantes
/// et de la configuration actuelle.
/// - Synchronise le Hull du vaisseau (remainingHullInRun -> RunSessionState)
/// - Synchronise les vies de contrat (remainingContractLives -> RunSessionState)
/// - Gère le flag "keepCurrentHullOnNextRestart" (Retry après DEFEAT)
/// - Marque le level comme "en cours" (levelInProgress = true) pour la règle "quit = défaite".
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

        // CAS 1 : retry avec conservation du hull (flag consommé ici)
        if (runSessionState.ConsumeKeepFlag())
        {
            int currentHull = Mathf.Max(0, runSessionState.Hull);
            runSessionState.InitHull(currentHull);

            // Les vies de contrat viennent toujours de la persistance (logique centralisée).
            int contractLives = ResolveContractLivesFromSaveOrDefault();
            runSessionState.InitContractLives(contractLives);

            MarkLevelInProgressIfRunExists();

            Debug.Log("[RunSessionBootstrapper] Keeping existing hull for retry: "
                      + currentHull + " | contractLives=" + contractLives);
            return;
        }

        // CAS 2 : run persistante existante -> on prend Hull depuis la save
        // et les vies de contrat via la fonction centralisée.
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Current != null &&
            SaveManager.Instance.Current.runState != null &&
            SaveManager.Instance.Current.runState.hasOngoingRun)
        {
            var run = SaveManager.Instance.Current.runState;

            int hull = Mathf.Max(0, run.remainingHullInRun);
            runSessionState.InitHull(hull);

            int contractLives = ResolveContractLivesFromSaveOrDefault();
            runSessionState.InitContractLives(contractLives);

            // Level en cours pour la règle "quit = défaite"
            run.levelInProgress = true;
            SaveManager.Instance.Save();

            Debug.Log("[RunSessionBootstrapper] Hull from persistent run: " + hull
                      + " | contractLives=" + contractLives
                      + " (LevelId=" + run.currentLevelId + ")");
            return;
        }

        // CAS 3 : fallback (pas de run persistante) -> hull depuis le vaisseau,
        // contrat via la même fonction (valeur par défaut 3 si rien en save).
        int fallbackHull = ResolveFallbackHullFromShip();
        runSessionState.InitHull(fallbackHull);

        int fallbackContractLives = ResolveContractLivesFromSaveOrDefault();
        runSessionState.InitContractLives(fallbackContractLives);

        Debug.LogWarning("[RunSessionBootstrapper] Fallback init: hull=" + fallbackHull
                         + " | contractLives=" + fallbackContractLives);
    }

    /// <summary>
    /// Si une run persistante existe, marque le level comme "en cours" et sauvegarde.
    /// Utilisé dans le cas du retry avec conservation du hull.
    /// </summary>
    private void MarkLevelInProgressIfRunExists()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        var run = SaveManager.Instance.Current.runState;
        if (run == null || !run.hasOngoingRun)
            return;

        run.levelInProgress = true;
        SaveManager.Instance.Save();
    }

    /// <summary>
    /// Détermine le nombre de vies à partir du vaisseau sélectionné dans RunConfig / ShipCatalog.
    /// Utilisé uniquement en fallback si aucune run persistante n'est valide.
    /// </summary>
    private int ResolveFallbackHullFromShip()
    {
        int lives = 3;

        var runConfig = RunConfig.Instance;
        var catalog = ShipCatalogService.Catalog;

        if (runConfig == null || catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            Debug.LogWarning("[RunSessionBootstrapper] ShipCatalog or RunConfig missing. Defaulting hull to " + lives);
            return lives;
        }

        string shipId = string.IsNullOrEmpty(runConfig.SelectedShipId) ? "CORE_SCOUT" : runConfig.SelectedShipId;
        var ship = catalog.ships.Find(s => s.id == shipId);
        if (ship == null)
        {
            Debug.LogWarning("[RunSessionBootstrapper] Ship not found: " + shipId + ". Defaulting hull to " + lives);
            return lives;
        }

        lives = Mathf.Max(0, ship.maxHull);
        return lives;
    }

    /// <summary>
    /// Lit les vies de contrat depuis la sauvegarde.
    /// Comportement :
    /// - Si pas de save / pas de runState -> 3
    /// - Si remainingContractLives <= 0 (ancienne save ou non initialisé) -> on force 3 et on sauve
    /// - Sinon -> on clamp entre 0 et 3
    /// </summary>
    private int ResolveContractLivesFromSaveOrDefault()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Current == null ||
            SaveManager.Instance.Current.runState == null)
        {
            return 3;
        }

        var run = SaveManager.Instance.Current.runState;

        int lives = run.remainingContractLives;

        // Si la valeur n'a jamais été initialisée ou est invalide,
        // on force 3 comme valeur par défaut pour le contrat.
        if (lives <= 0)
        {
            lives = 3;
            run.remainingContractLives = 3;
            SaveManager.Instance.Save();
        }

        return Mathf.Clamp(lives, 0, 3);
    }
}
