using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controle la scene Ship Select.
/// Gere la navigation dans le catalogue, l affichage, le choix du vaisseau et l initialisation d une run.
/// Les transitions de scene sont deleguees a GameFlowController via BootRoot.
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

    // Index du vaisseau courant dans le catalogue
    private int index = 0;

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
    }

    private void Start()
    {
        if (BootRoot.GameFlow == null)
            Debug.LogError("[ShipSelectController] BootRoot.GameFlow est null. ShipSelect doit etre charge depuis Boot/Title.");

        if (TitleMusicPlayer.Instance != null)
            TitleMusicPlayer.Instance.SnapToTargetVolume();

        string savedId = ResolveInitialShipIdForUI();

        var ships = ShipCatalogService.Catalog.ships;
        int found = ships.FindIndex(s => s.id == savedId);
        index = (found >= 0) ? found : 0;

        RefreshUI();
    }

    // ---------------------------------------------------------
    // Navigation callbacks (Inspector)
    // ---------------------------------------------------------

    public void OnPreviousPressed()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index - 1 + count) % count;
        RefreshUI();
    }

    public void OnNextPressed()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index + 1) % count;
        RefreshUI();
    }

    // ---------------------------------------------------------
    // Back button
    // ---------------------------------------------------------

    public void OnBackPressed()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;

        BootRoot.GameFlow.GoToTitle();
    }

    // ---------------------------------------------------------
    // Start button + run init (NEW CONVENTION)
    // ---------------------------------------------------------

    public void OnStartPressed()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[ShipSelectController] SaveManager manquant. Impossible de demarrer une run.");
            return;
        }

        var ship = ShipCatalogService.Catalog.ships[index];

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

    private IEnumerator StartAfterMusicFadeRoutine()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        BootRoot.GameFlow.GoToRunHub();
    }

    // ---------------------------------------------------------
    // UI refresh
    // ---------------------------------------------------------

    private void RefreshUI()
    {
        var ship = ShipCatalogService.Catalog.ships[index];

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

    private string StripExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        // Resources.Load ne veut pas l extension (.png)
        int dot = fileName.LastIndexOf('.');
        if (dot <= 0)
            return fileName;

        return fileName.Substring(0, dot);
    }

    // ---------------------------------------------------------
    // Initial selection
    // ---------------------------------------------------------

    private string ResolveInitialShipIdForUI()
    {
        // Priorite:
        // 1) Ship de run si une run est en cours (resume)
        // 2) Dernier choix persistant du profil
        // 3) RunConfig (fallback dev)
        // 4) CORE_SCOUT

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

    
}
