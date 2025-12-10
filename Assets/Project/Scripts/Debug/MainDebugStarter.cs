using UnityEngine;
using System.IO;


/// <summary>
/// Outil de debug pour lancer directement la scene Main.
/// - Ne s active que si BootRoot.GameFlow est null (scene Main lancee seule).
/// - Desactive RunSessionBootstrapper pour ne pas retourner au Title.
/// - Initialise RunSessionState en local (hull + vies de contrat).
/// - Force un vaisseau et un JSON de niveau.
/// - Configure les flags de skip briefing / intro dans le LevelManager.
/// </summary>
[DefaultExecutionOrder(-500)]
public class MainDebugStarter : MonoBehaviour
{
    [Header("Debug mode")]
    [SerializeField] private bool debugEnabled = true;

    [Header("Vaisseau")]
    [SerializeField] private string debugShipId = "CORE_SCOUT";
    [SerializeField] private int defaultContractLives = 3;

    [Header("Niveau")]
    [SerializeField] private TextAsset debugLevelJson;

    [Header("Skip sequences")]
    [SerializeField] private bool debugSkipBriefing = true;
    [SerializeField] private bool debugSkipIntro = true;

    [Header("Refs")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private RunSessionBootstrapper runSessionBootstrapper;

    [Header("Dialogs debug")]
    [SerializeField] private bool enableDialogsInDebug = true;
    [SerializeField] private DialogManager dialogManagerPrefab;


    private void Awake()
    {
        if (!debugEnabled)
            return;

        // Flow normal present ? On ne fait rien.
        if (BootRoot.GameFlow != null)
            return;

        Debug.Log("[MainDebugStarter] Debug mode actif pour la scene Main (BootRoot.GameFlow null).");

        // 1) On s assure d avoir un ShipCatalog charge pour le debug
        TryEnsureShipCatalogLoaded();

        // 2) On desactive RunSessionBootstrapper pour eviter le retour auto au Title
        if (runSessionBootstrapper != null)
        {
            runSessionBootstrapper.enabled = false;
        }

        // 3) Setup du vaisseau dans RunConfig
        var runConfig = RunConfig.Instance;
        if (runConfig != null && !string.IsNullOrEmpty(debugShipId))
        {
            runConfig.SetSelectedShip(debugShipId);
            Debug.Log("[MainDebugStarter] RunConfig ship id = " + debugShipId);
        }

        // 4) Init directe de RunSessionState
        SetupRunSessionState();

        // 5) Override JSON de niveau
        if (levelManager != null && debugLevelJson != null)
        {
            levelManager.DebugOverrideLevelJson(debugLevelJson);
        }

        //6) Ajout des dialogues
        EnsureDialogManagerForDebug();

        // 7) Flags de skip briefing / intro
        if (levelManager != null)
        {
            levelManager.SetDebugSkipFlags(debugSkipBriefing, debugSkipIntro);
        }
    }


    /// <summary>
    /// Initialise le RunSessionState localement en debug
    /// (hull + vies de contrat) a partir du ShipCatalog si possible.
    /// </summary>
    private void SetupRunSessionState()
    {
        if (runSessionState == null)
        {
            Debug.LogWarning("[MainDebugStarter] RunSessionState non assigne, impossible de l initialiser en debug.");
            return;
        }

        int hullValue = 3;

        // On essaie de recuperer le vaisseau dans le catalog pour prendre son maxHull.
        if (ShipCatalogService.Catalog != null &&
            ShipCatalogService.Catalog.ships != null &&
            ShipCatalogService.Catalog.ships.Count > 0)
        {
            string shipId = string.IsNullOrEmpty(debugShipId) ? "CORE_SCOUT" : debugShipId;
            var ship = ShipCatalogService.Catalog.ships.Find(s => s.id == shipId);
            if (ship != null)
            {
                hullValue = Mathf.Max(1, ship.maxHull);
            }
            else
            {
                Debug.LogWarning("[MainDebugStarter] Vaisseau introuvable dans le catalog pour id=" + shipId + ", hull par defaut = " + hullValue);
            }
        }
        else
        {
            Debug.LogWarning("[MainDebugStarter] ShipCatalog non disponible, hull par defaut = " + hullValue);
        }

        runSessionState.InitHull(hullValue);
        runSessionState.InitContractLives(defaultContractLives);

        Debug.Log("[MainDebugStarter] RunSessionState init en debug: hull=" + hullValue + ", contractLives=" + defaultContractLives);
    }

    /// <summary>
    /// Charge ShipCatalog.json depuis StreamingAssets si le service global est vide.
    /// Utilise pour le debug de la scene Main lancee seule.
    /// </summary>
    private void TryEnsureShipCatalogLoaded()
    {
        if (ShipCatalogService.Catalog != null &&
            ShipCatalogService.Catalog.ships != null &&
            ShipCatalogService.Catalog.ships.Count > 0)
        {
            // Deja charge par un autre flow
            return;
        }

        string path = Path.Combine(Application.streamingAssetsPath, "Ships", "ShipCatalog.json");

        if (!File.Exists(path))
        {
            Debug.LogWarning("[MainDebugStarter] ShipCatalog introuvable a ce chemin: " + path);
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var catalog = JsonUtility.FromJson<ShipCatalog>(json);

            if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
            {
                Debug.LogWarning("[MainDebugStarter] ShipCatalog charge mais vide ou invalide.");
                return;
            }

            ShipCatalogService.Catalog = catalog;
            Debug.Log("[MainDebugStarter] ShipCatalog charge en debug (" + catalog.ships.Count + " vaisseaux).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[MainDebugStarter] Exception lors du chargement du ShipCatalog: " + ex.Message);
        }
    }

    /// <summary>
    /// En mode debug Main, s assure qu un DialogManager existe dans la scene.
    /// Si un DialogManager est deja present (par exemple scene de test speciale),
    /// on ne fait rien. Sinon, on instancie le prefab fourni.
    /// </summary>
    private void EnsureDialogManagerForDebug()
    {
        if (!enableDialogsInDebug)
            return;

        // Si un DialogManager existe deja (scene speciale), on ne touche a rien.
        var existing = FindFirstObjectByType<DialogManager>();
        if (existing != null)
            return;

        if (dialogManagerPrefab == null)
        {
            Debug.LogWarning("[MainDebugStarter] Dialog debug actif mais aucun prefab de DialogManager n est assigne.");
            return;
        }

        var dm = Instantiate(dialogManagerPrefab);
        dm.name = "[Debug] DialogManager";

        // Si ton DialogManager a une methode explicite d initialisation (par exemple Init ou LoadAll),
        // c est ici qu il faut l appeler.
        // Exemple fictif :
        // dm.Initialize();
    }

}
