using UnityEngine;

/// <summary>
/// Applique les stats runtime du vaisseau pour le niveau :
/// - resolve ship selection (RunSessionState -> ShipCatalog, fallback RunConfig)
/// - fournit runDurationSec (+ shieldSeconds si tu gardes ce label UI)
/// - initialise le background du vaisseau
/// - initialise ScoreManager + binding ScoreUI (optionnel)
///
/// IMPORTANT (nouvelle convention) :
/// - Hull (current + max) n'est JAMAIS piloté ici.
/// - Source de vérité Hull = RunSessionState.
/// - L'initialisation/sync du HUD Hull est faite par HullBinder (RunSessionState-only).
/// </summary>
public class ShipRuntimeSetup : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Score")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private GameplayScoreImpactUI gameplayScoreImpactUI;

    [Header("Background")]
    [SerializeField] private ShipBackgroundController shipBackgroundController;

    /// <summary>
    /// Applique le setup runtime.
    /// maxHull est renvoyé uniquement pour debug/log/compat, mais sa source est RunSessionState.
    /// </summary>
    public bool TryApply(out int maxHull, out float runDurationSec, out float shieldSeconds)
    {
        maxHull = 0;
        runDurationSec = 0f;
        shieldSeconds = 0f;

        if (runSession == null)
        {
            Debug.LogError("[ShipRuntimeSetup] RunSessionState non assigne.");
            return false;
        }

        // La source de vérité Hull est la run (modules futurs, etc.)
        maxHull = Mathf.Max(1, runSession.HullMax);

        // ------------------------------------------------------------
        // SCORE (optionnel) + reset
        // ------------------------------------------------------------
        if (scoreManager != null)
        {
            /*
            if (scoreUI != null)
            {
                // Idempotent (utile en debug rerun)
                scoreManager.onScoreChanged.RemoveListener(scoreUI.UpdateScoreText);
                scoreManager.onScoreChanged.AddListener(scoreUI.UpdateScoreText);
            }
            */
            scoreManager.ResetScore(0);
            if (gameplayScoreImpactUI != null)
            {
                gameplayScoreImpactUI.SetInstant(0);
            }
        }

        // ------------------------------------------------------------
        // RESOLVE SHIP (pour durée + background)
        // ------------------------------------------------------------
        ShipDefinition ship;
        if (!TryResolveSelectedShip(out ship))
        {
            Debug.LogError("[ShipRuntimeSetup] Impossible de resoudre le vaisseau selectionne.");
            return false;
        }

        // ------------------------------------------------------------
        // BACKGROUND
        // ------------------------------------------------------------
        if (shipBackgroundController != null && !string.IsNullOrEmpty(ship.imagePath))
        {
            shipBackgroundController.Init(ship.imagePath);
        }

        // ------------------------------------------------------------
        // DUREE RUNTIME (ship)
        // ------------------------------------------------------------
        float duration = ship.baseLevelDurationSec;

        if (duration <= 0f)
        {
            Debug.LogError("[ShipRuntimeSetup] baseLevelDurationSec invalide pour ship=" + ship.id + " (" + duration + ").");
            return false;
        }

        float moduleBonusDuration = 0f;

        if (ModuleRuntimeStats.Instance != null)
            moduleBonusDuration = Mathf.Max(0f, ModuleRuntimeStats.Instance.LevelDurationBonusSec);

        runDurationSec = duration + moduleBonusDuration;
        shieldSeconds = runDurationSec;

        return true;
    }

    private bool TryResolveSelectedShip(out ShipDefinition ship)
    {
        ship = null;

        ShipCatalog catalog = ShipCatalogService.Catalog;
        if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            Debug.LogWarning("[ShipRuntimeSetup] ShipCatalog manquant ou vide.");
            return false;
        }

        string shipId = null;

        // 1) Source de verite: RunSessionState
        if (runSession != null && !string.IsNullOrEmpty(runSession.ShipId))
            shipId = runSession.ShipId;

        // 2) Fallback: RunConfig
        if (string.IsNullOrEmpty(shipId))
        {
            RunConfig run = RunConfig.Instance;
            if (run != null && !string.IsNullOrEmpty(run.SelectedShipId))
                shipId = run.SelectedShipId;
        }

        // 3) Fallback ultime: premier du catalog (defensif)
        if (string.IsNullOrEmpty(shipId))
            shipId = catalog.ships[0].id;

        ship = catalog.ships.Find(s => s.id == shipId);
        if (ship == null)
        {
            Debug.LogWarning("[ShipRuntimeSetup] Vaisseau introuvable: " + shipId);
            return false;
        }

        return true;
    }
}
