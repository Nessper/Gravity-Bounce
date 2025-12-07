using UnityEngine;

/// <summary>
/// Initialise l'état de run pour la scène Main à partir des données persistantes
/// et de la configuration actuelle.
/// - Synchronise les vies de campagne (RunStateData.remainingLivesInRun -> RunSessionState)
/// - Gère le flag "keepCurrentLivesOnNextRestart" (Retry après DEFEAT)
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

        // CAS 1 : retry avec conservation des vies (flag consommé ici)
        if (runSessionState.ConsumeKeepFlag())
        {
            int current = Mathf.Max(0, runSessionState.Hull);
            runSessionState.InitHull(current);

            MarkLevelInProgressIfRunExists();

            Debug.Log("[RunSessionBootstrapper] Keeping existing hull for retry: " + current);
            return;
        }

        // CAS 2 : run persistante existante -> on prend les vies de la campagne
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Current != null &&
            SaveManager.Instance.Current.runState != null &&
            SaveManager.Instance.Current.runState.hasOngoingRun)
        {
            var run = SaveManager.Instance.Current.runState;
            int hull = Mathf.Max(0, run.remainingHullInRun);

            runSessionState.InitHull(hull);

            // Level en cours pour la règle "quit = défaite"
            run.levelInProgress = true;
            SaveManager.Instance.Save();

            Debug.Log("[RunSessionBootstrapper] Hull from persistent run: " + hull
                      + " (LevelId=" + run.currentLevelId + ")");
            return;
        }

        // CAS 3 : fallback (pas de run persistante) -> on récupère les vies depuis le vaisseau sélectionné
        int fallbackHull = ResolveFallbackHullFromShip();
        runSessionState.InitHull(fallbackHull);

        Debug.LogWarning("[RunSessionBootstrapper] Fallback hull init: " + fallbackHull);
    }

    /// <summary>
    /// Si une run persistante existe, marque le level comme "en cours" et sauvegarde.
    /// Utilisé dans le cas du retry avec conservation des vies.
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
            Debug.LogWarning("[RunSessionBootstrapper] ShipCatalog or RunConfig missing. Defaulting lives to " + lives);
            return lives;
        }

        string shipId = string.IsNullOrEmpty(runConfig.SelectedShipId) ? "CORE_SCOUT" : runConfig.SelectedShipId;
        var ship = catalog.ships.Find(s => s.id == shipId);
        if (ship == null)
        {
            Debug.LogWarning("[RunSessionBootstrapper] Ship not found: " + shipId + ". Defaulting lives to " + lives);
            return lives;
        }

        lives = Mathf.Max(0, ship.maxHull);
        return lives;
    }
}
