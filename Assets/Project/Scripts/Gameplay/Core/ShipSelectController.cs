using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Ship Select scene.
/// Handles ship navigation, data display, ship choice and run initialization.
/// Scene transitions are delegated to GameFlowController through BootRoot.
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

    /// <summary>
    /// Index of the currently selected ship in the catalog.
    /// </summary>
    private int index = 0;

    private void Awake()
    {
        // Safety: make sure the ship catalog is loaded and not empty.
        if (ShipCatalogService.Catalog == null ||
            ShipCatalogService.Catalog.ships == null ||
            ShipCatalogService.Catalog.ships.Count == 0)
        {
            Debug.LogError("[ShipSelectController] Ship catalog not loaded or empty.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Safety: ShipSelect should be reached through the normal game flow.
        if (BootRoot.GameFlow == null)
        {
            Debug.LogError("[ShipSelectController] BootRoot.GameFlow is null. ShipSelect must be loaded from Boot and Title flow.");
        }

        // Ensure title music is at full volume when entering ShipSelect.
        if (TitleMusicPlayer.Instance != null)
            TitleMusicPlayer.Instance.SnapToTargetVolume();

        // Restore previously selected ship id from RunConfig if available.
        string savedId = RunConfig.Instance != null
            ? RunConfig.Instance.SelectedShipId
            : "CORE_SCOUT"; // default fallback for now

        var ships = ShipCatalogService.Catalog.ships;
        int found = ships.FindIndex(s => s.id == savedId);

        // If we find the saved ship id, start from that index, otherwise fallback to first ship.
        index = (found >= 0) ? found : 0;

        // Initial UI refresh for the current ship.
        RefreshUI();
    }

    // ---------------------------------------------------------
    // Navigation callbacks
    // These are meant to be wired through the Inspector.
    // ---------------------------------------------------------

    /// <summary>
    /// Called by the Previous button.
    /// Cycles to the previous ship with wrap-around.
    /// </summary>
    public void OnPreviousPressed()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index - 1 + count) % count;
        RefreshUI();
    }

    /// <summary>
    /// Called by the Next button.
    /// Cycles to the next ship with wrap-around.
    /// </summary>
    public void OnNextPressed()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index + 1) % count;
        RefreshUI();
    }

    // ---------------------------------------------------------
    // Back button callback
    // ---------------------------------------------------------

    /// <summary>
    /// Called by the Back button.
    /// Sets SkipTitleIntroOnce to true and returns to the Title scene through GameFlow.
    /// </summary>
    public void OnBackPressed()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;

        BootRoot.GameFlow.GoToTitle();
    }

    // ---------------------------------------------------------
    // Start button callback and run initialization
    // ---------------------------------------------------------

    /// <summary>
    /// Called by the Start button.
    /// 1) Stores the selected ship in RunConfig and persistent save data.
    /// 2) Initializes a new run in GameSaveData.runState.
    /// 3) Starts the level after a music fade out using GameFlow.
    /// </summary>
    public void OnStartPressed()
    {
        var ship = ShipCatalogService.Catalog.ships[index];

        // 1) Update run configuration and persistent selected ship.
        if (RunConfig.Instance != null)
        {
            RunConfig.Instance.SetSelectedShip(ship.id);
        }

        // 2) Initialize run state in the persistent save.
        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            GameSaveData save = SaveManager.Instance.Current;

            // Ensure runState is not null.
            if (save.runState == null)
            {
                save.runState = new RunStateData();
            }

            RunStateData run = save.runState;

            // Mark that there is an ongoing run.
            run.hasOngoingRun = true;

            // Set ship used for this run.
            run.currentShipId = ship.id;

            // For now, we hardcode world and level.
            // TODO: replace with CampaignPlan when available.
            run.currentWorld = 1;
            run.currentLevelIndex = 0;
            run.currentLevelId = "W1-L1";

            // Hull for the run equals the ship base hull.
            run.remainingHullInRun = Mathf.Max(0, ship.maxHull);

            // Reset run score.
            run.currentRunScore = 0;

            // No level cleared at the start of the run.
            run.levelsClearedInRun = 0;

            // No level is in progress when leaving ShipSelect.
            run.levelInProgress = false;

            // Save the updated run state immediately.
            SaveManager.Instance.Save();
        }

        // 3) Start the level after fading out the title music.
        StartCoroutine(StartAfterMusicFadeRoutine());
    }

    /// <summary>
    /// Waits for title music fade out, then uses GameFlow to start the level.
    /// </summary>
    private IEnumerator StartAfterMusicFadeRoutine()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        // Scene change is done through GameFlow.
        BootRoot.GameFlow.StartLevel();
    }

    // ---------------------------------------------------------
    // UI refresh
    // ---------------------------------------------------------

    /// <summary>
    /// Updates all UI elements to reflect the currently selected ship.
    /// </summary>
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
            shieldText.text = ship.shieldSecondsPerLevel.ToString();

        if (paddleWidthText != null)
            paddleWidthText.text = ship.paddleWidthMult.ToString();

        // Load ship image from StreamingAssets if an image file is defined.
        if (!string.IsNullOrEmpty(ship.imageFile) && shipImage != null)
            StartCoroutine(LoadSpriteFromStreamingAssetsRoutine(ship.imageFile, shipImage));
    }

    // ---------------------------------------------------------
    // Image loading from StreamingAssets
    // ---------------------------------------------------------

    /// <summary>
    /// Loads a texture from StreamingAssets and converts it to a Sprite for UI.
    /// </summary>
    private IEnumerator LoadSpriteFromStreamingAssetsRoutine(string fileName, Image target)
    {
        string url = Path.Combine(Application.streamingAssetsPath, "Ships/Images", fileName);

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ShipSelectController] Failed to load texture: " + req.error + " (" + url + ")");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
            {
                Debug.LogError("[ShipSelectController] Downloaded texture is null: " + url);
                yield break;
            }

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            target.sprite = sprite;
            target.preserveAspect = true;
        }
    }
}
