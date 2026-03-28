// Chemin recommandé (projet Unity) : Scripts/UI/ShipSelect/ShipSelectController.cs

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controle la scene Ship Select.
/// Gere la navigation dans le catalogue, l affichage, le choix du vaisseau et l initialisation d une run.
/// Les transitions de scene sont deleguees a GameFlowController via BootRoot.
///
/// Regle debug :
/// - Les vaisseaux marques debugOnly ne doivent pas apparaitre en mode normal.
/// - Ils restent visibles uniquement si le debug global est actif (PlayerPref VS_DEBUG_MAIN = 1).
///
/// Hypothese de design :
/// - Ce controleur est le seul endroit du jeu ou le joueur choisit son vaisseau.
/// - On applique donc ici un filtrage local, sans refactor plus large du catalogue.
/// </summary>
public class ShipSelectController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private Image shipImage;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private TMP_Text paddleWidthText;

    // Index du vaisseau courant dans le catalogue brut.
    private int index = 0;

    // Cle du PlayerPref utilisee par ton flow debug.
    private const string DebugPlayerPrefKey = "VS_DEBUG_MAIN";

    private void Awake()
    {
        if (ShipCatalogService.Catalog == null ||
            ShipCatalogService.Catalog.ships == null ||
            ShipCatalogService.Catalog.ships.Count == 0)
        {
            Debug.LogError("[ShipSelectController] ShipCatalog non charge ou vide.");
            enabled = false;
            return;
        }

        // Securite : si aucun vaisseau visible n existe pour ce mode,
        // on desactive le controleur pour eviter une navigation infinie.
        if (!HasAnyVisibleShip())
        {
            Debug.LogError("[ShipSelectController] Aucun vaisseau visible pour le mode courant.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        if (BootRoot.GameFlow == null)
            Debug.LogError("[ShipSelectController] BootRoot.GameFlow est null. ShipSelect doit etre charge depuis Boot/Title.");

        if (TitleMusicPlayer.Instance != null)
            TitleMusicPlayer.Instance.SnapToTargetVolume();

        string savedId = ResolveInitialShipIdForUI();

        var ships = ShipCatalogService.Catalog.ships;
        int found = ships.FindIndex(s => s != null && s.id == savedId);

        // Si on retrouve le ship sauvegarde, on s aligne dessus.
        // Sinon fallback sur 0.
        index = (found >= 0) ? found : 0;

        // Si l index tombe sur un ship debug non visible en mode normal,
        // on avance jusqu au prochain ship autorise.
        EnsureCurrentIndexIsVisible();

        RefreshUI();
    }

    // ---------------------------------------------------------
    // Navigation callbacks (Inspector)
    // ---------------------------------------------------------

    /// <summary>
    /// Navigue vers le vaisseau precedent visible.
    /// Les ships debugOnly sont ignores en mode normal.
    /// </summary>
    public void OnPreviousPressed()
    {
        var ships = ShipCatalogService.Catalog.ships;
        int count = ships.Count;

        if (count == 0)
            return;

        do
        {
            index = (index - 1 + count) % count;
        }
        while (!IsShipVisibleAt(index));

        RefreshUI();
    }

    /// <summary>
    /// Navigue vers le vaisseau suivant visible.
    /// Les ships debugOnly sont ignores en mode normal.
    /// </summary>
    public void OnNextPressed()
    {
        var ships = ShipCatalogService.Catalog.ships;
        int count = ships.Count;

        if (count == 0)
            return;

        do
        {
            index = (index + 1) % count;
        }
        while (!IsShipVisibleAt(index));

        RefreshUI();
    }

    // ---------------------------------------------------------
    // Back button
    // ---------------------------------------------------------

    /// <summary>
    /// Retour au Title.
    /// On indique au RunConfig de ne pas rejouer l intro title au retour.
    /// </summary>
    public void OnBackPressed()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;

        BootRoot.GameFlow.GoToTitle();
    }

    // ---------------------------------------------------------
    // Start button + run init (NEW CONVENTION)
    // ---------------------------------------------------------

    /// <summary>
    /// Initialise une nouvelle run avec le ship actuellement selectionne,
    /// puis lance le RunHub apres fade out de la musique.
    /// </summary>
    public void OnStartPressed()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[ShipSelectController] SaveManager manquant. Impossible de demarrer une run.");
            return;
        }

        EnsureCurrentIndexIsVisible();

        var ship = ShipCatalogService.Catalog.ships[index];

        // Securite supplementaire : empeche le demarrage d un ship debug en mode normal.
        if (ship != null && ship.debugOnly && !IsDebugActive())
        {
            Debug.LogWarning("[ShipSelectController] Tentative de demarrage avec un ship debug en mode normal.");
            return;
        }

        // 1) RunConfig (UI): utile pour l affichage et le flow Title, mais pas source de verite gameplay
        if (RunConfig.Instance != null)
            RunConfig.Instance.SetSelectedShip(ship.id);

        // 2) Save (source de verite): on persiste le choix du profil + le ship de run
        GameSaveData save = SaveManager.Instance.Current;

        // Choix persistant du profil (dernier vaisseau choisi)
        save.selectedShipId = ship.id;

        if (save.runState == null)
            save.runState = new RunStateData();

        RunStateData run = save.runState;

        run.hasOngoingRun = true;

        // Ship utilise pour cette run (gele)
        run.currentShipId = ship.id;

        // Convention: worldId + currentNodeIndex (next playable)
        run.worldId = "W1";
        run.currentNodeIndex = 0;

        // Ressources de run
        // IMPORTANT: HullMax n'est pas persisté. Il est dérivé (ship + modules) par RunSessionState.
        // Ici on initialise simplement le hull courant "plein" sur la base du ship.
        run.remainingHullInRun = Mathf.Max(1, ship.maxHull);

        run.remainingContractLives = 3;
        run.currentRunScore = 0;
        run.nodesClearedInRun = 0;

        // Pas en gameplay
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        SaveManager.Instance.Save();

        Debug.Log("[ShipSelectController] StartRun shipId=" + ship.id
                  + " profileSelected=" + save.selectedShipId
                  + " runShip=" + run.currentShipId);

        // 3) Demarre le niveau apres fade out musique
        StartCoroutine(StartAfterMusicFadeRoutine());
    }

    /// <summary>
    /// Attend le fade out de la musique du title avant de lancer le RunHub.
    /// </summary>
    private IEnumerator StartAfterMusicFadeRoutine()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        BootRoot.GameFlow.GoToRunHub();
    }

    // ---------------------------------------------------------
    // UI refresh
    // ---------------------------------------------------------

    /// <summary>
    /// Rafraichit tout l affichage a partir du ship courant.
    /// </summary>
    private void RefreshUI()
    {
        EnsureCurrentIndexIsVisible();

        var ship = ShipCatalogService.Catalog.ships[index];
        if (ship == null)
        {
            Debug.LogWarning("[ShipSelectController] Ship null a l index " + index);
            return;
        }

        if (shipNameText != null)
            shipNameText.text = ship.displayName;

        if (descriptionText != null)
            descriptionText.text = ship.description;

        if (hullText != null)
            hullText.text = ship.maxHull.ToString();

        if (shieldText != null)
            shieldText.text = ship.levelDurationSec.ToString("0") + "s";

        if (paddleWidthText != null)
            paddleWidthText.text = ship.paddleWidthMult.ToString();

        // Image depuis Resources (plus de StreamingAssets)
        if (shipImage != null)
        {
            string key = StripExtension(ship.imageFile);
            Sprite sprite = Resources.Load<Sprite>("Ships/Images/" + key);

            if (sprite == null)
                Debug.LogWarning("[ShipSelectController] Sprite introuvable dans Resources: Ships/Images/" + key);
            else
                shipImage.sprite = sprite;

            shipImage.preserveAspect = true;
        }
    }

    /// <summary>
    /// Retire l extension d un nom de fichier, car Resources.Load ne veut pas ".png".
    /// </summary>
    private string StripExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        int dot = fileName.LastIndexOf('.');
        if (dot <= 0)
            return fileName;

        return fileName.Substring(0, dot);
    }

    // ---------------------------------------------------------
    // Initial selection
    // ---------------------------------------------------------

    /// <summary>
    /// Determine quel ship doit etre selectionne a l ouverture de l ecran.
    ///
    /// Priorite:
    /// 1) Ship de run si une run est en cours (resume)
    /// 2) Dernier choix persistant du profil
    /// 3) RunConfig (fallback dev)
    /// 4) CORE_SCOUT
    /// </summary>
    private string ResolveInitialShipIdForUI()
    {
        string id = "CORE_SCOUT";

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            GameSaveData save = SaveManager.Instance.Current;

            if (save.runState != null && save.runState.hasOngoingRun && !string.IsNullOrEmpty(save.runState.currentShipId))
                return save.runState.currentShipId;

            if (!string.IsNullOrEmpty(save.selectedShipId))
                return save.selectedShipId;
        }

        if (RunConfig.Instance != null && !string.IsNullOrEmpty(RunConfig.Instance.SelectedShipId))
            id = RunConfig.Instance.SelectedShipId;

        return id;
    }

    // ---------------------------------------------------------
    // Debug visibility helpers
    // ---------------------------------------------------------

    /// <summary>
    /// Retourne true si le debug global est actif.
    /// </summary>
    private bool IsDebugActive()
    {
        return PlayerPrefs.GetInt(DebugPlayerPrefKey, 0) == 1;
    }

    /// <summary>
    /// Retourne true si le ship a l index donne est visible dans le mode courant.
    /// </summary>
    private bool IsShipVisibleAt(int shipIndex)
    {
        var ships = ShipCatalogService.Catalog.ships;

        if (ships == null || shipIndex < 0 || shipIndex >= ships.Count)
            return false;

        ShipDefinition ship = ships[shipIndex];
        if (ship == null)
            return false;

        if (ship.debugOnly && !IsDebugActive())
            return false;

        return true;
    }

    /// <summary>
    /// Verifie qu il existe au moins un ship visible pour le mode courant.
    /// Evite les boucles infinies dans Previous/Next.
    /// </summary>
    private bool HasAnyVisibleShip()
    {
        var ships = ShipCatalogService.Catalog.ships;
        if (ships == null || ships.Count == 0)
            return false;

        for (int i = 0; i < ships.Count; i++)
        {
            if (IsShipVisibleAt(i))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Si l index courant pointe vers un ship non visible,
    /// avance jusqu au prochain ship autorise.
    /// </summary>
    private void EnsureCurrentIndexIsVisible()
    {
        var ships = ShipCatalogService.Catalog.ships;
        if (ships == null || ships.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, ships.Count - 1);

        if (IsShipVisibleAt(index))
            return;

        int startIndex = index;

        do
        {
            index = (index + 1) % ships.Count;
        }
        while (!IsShipVisibleAt(index) && index != startIndex);
    }
}