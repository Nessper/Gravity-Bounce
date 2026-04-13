using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controle la scene Ship Select.
///
/// Responsabilites principales :
/// - Recuperer le vaisseau courant a afficher.
/// - Binder toutes les informations UI liees au ship selectionne.
/// - Gerer la navigation de base entre les ships.
/// - Gerer le lancement d une nouvelle run avec le ship choisi.
/// - Afficher les modules equipes de depart du vaisseau via ModulesListPanelUI.
///
/// Cette version utilise desormais un composant reutilisable
/// pour le bloc modules.
/// </summary>
public class ShipSelectController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text shipNameText;

    [Header("Shared Ship Status Panel")]
    [SerializeField] private ShipStatusPanelUI shipStatusPanel;

    [SerializeField] private ModulesListPanelUI equippedModulesListPanel;

    // SpriteRenderer utilise pour afficher visuellement le vaisseau
    // dans la zone principale de presentation.
    [SerializeField] private SpriteRenderer shipImageRenderer;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    // Index courant dans la liste des ships du catalog.
    private int currentIndex = 0;

    // Noms des packs de localisation.
    private const string ShipsPackName = "ships";
    private const string UiPackName = "ui";

    // Fallback de ship si aucune selection precedente n existe.
    private const string DefaultShipId = "CORE_SCOUT";

    /// <summary>
    /// Index actuellement affiche.
    /// Expose en lecture seule pour les futurs controleurs de transition.
    /// </summary>
    public int CurrentIndex => currentIndex;

    /// <summary>
    /// True si le catalog contient au moins un ship navigable.
    /// </summary>
    public bool HasShips
    {
        get
        {
            List<ShipDefinition> ships = GetShips();
            return ships != null && ships.Count > 0;
        }
    }

    private void Awake()
    {
        if (!HasShips)
        {
            Debug.LogError("[ShipSelectController] ShipCatalog non charge ou vide.");
            enabled = false;
            return;
        }

        if (shipStatusPanel == null)
        {
            Debug.LogError("[ShipSelectController] shipStatusPanel n est pas assigne.");
            enabled = false;
            return;
        }

        if (equippedModulesListPanel == null)
        {
            Debug.LogError("[ShipSelectController] equippedModulesListPanel n est pas assigne.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        if (BootRoot.GameFlow == null)
            Debug.LogError("[ShipSelectController] BootRoot.GameFlow est null. ShipSelect doit etre charge depuis Boot/Title.");

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            Debug.LogError("[ShipSelectController] LocalizationManager non pret.");

        if (TitleMusicPlayer.Instance != null)
            TitleMusicPlayer.Instance.SnapToTargetVolume();

        string initialShipId = ResolveInitialShipIdForUI();
        int initialIndex = FindShipIndexById(initialShipId);

        ApplyShipByIndex(initialIndex >= 0 ? initialIndex : 0);
    }

    public void OnPreviousPressed()
    {
        if (!CanNavigate())
            return;

        ApplyShipByIndex(GetPreviousIndex());
    }

    public void OnNextPressed()
    {
        if (!CanNavigate())
            return;

        ApplyShipByIndex(GetNextIndex());
    }

    public void OnBackPressed()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;

        BootRoot.GameFlow.GoToTitle();
    }

    public void OnStartPressed()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[ShipSelectController] SaveManager manquant. Impossible de demarrer une run.");
            return;
        }

        ShipDefinition ship = GetCurrentShip();
        if (ship == null)
        {
            Debug.LogError("[ShipSelectController] Aucun ship courant.");
            return;
        }

        if (RunConfig.Instance != null)
            RunConfig.Instance.SetSelectedShip(ship.id);

        bool ok = NewRunInitializer.Initialize(SaveManager.Instance.Current, ship);

        if (!ok)
        {
            Debug.LogError("[ShipSelectController] Echec initialisation nouvelle run.");
            return;
        }

        SaveManager.Instance.Save();

        Debug.Log("[ShipSelectController] StartRun shipId=" + ship.id);

        StartCoroutine(StartAfterMusicFadeRoutine());
    }

    private IEnumerator StartAfterMusicFadeRoutine()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        BootRoot.GameFlow.GoToRunHub();
    }

    public bool CanNavigate()
    {
        List<ShipDefinition> ships = GetShips();
        return ships != null && ships.Count > 0;
    }

    public int GetPreviousIndex()
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return -1;

        return (currentIndex - 1 + ships.Count) % ships.Count;
    }

    public int GetNextIndex()
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return -1;

        return (currentIndex + 1) % ships.Count;
    }

    public void ApplyShipByIndex(int newIndex)
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return;

        currentIndex = Mathf.Clamp(newIndex, 0, ships.Count - 1);
        RefreshUI();
    }

    public void SetButtonsInteractable(bool interactable)
    {
        if (startButton != null)
            startButton.interactable = interactable;

        if (backButton != null)
            backButton.interactable = interactable;

        if (previousButton != null)
            previousButton.interactable = interactable;

        if (nextButton != null)
            nextButton.interactable = interactable;
    }

    public ShipDefinition GetCurrentShip()
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return null;

        currentIndex = Mathf.Clamp(currentIndex, 0, ships.Count - 1);
        return ships[currentIndex];
    }

    public ShipDefinition GetShipAtIndex(int shipIndex)
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return null;

        if (shipIndex < 0 || shipIndex >= ships.Count)
            return null;

        return ships[shipIndex];
    }

    public int FindShipIndexById(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
            return -1;

        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return -1;

        return ships.FindIndex(s => s != null && s.id == shipId);
    }

    private List<ShipDefinition> GetShips()
    {
        return ShipCatalogService.Catalog != null
            ? ShipCatalogService.Catalog.ships
            : null;
    }

    private void RefreshUI()
    {
        ShipDefinition ship = GetCurrentShip();
        if (ship == null)
        {
            Debug.LogWarning("[ShipSelectController] Ship null a l index " + currentIndex);
            return;
        }

        if (shipNameText != null)
            shipNameText.text = LocalizationManager.Instance.GetTextOrKey(ShipsPackName, ship.displayNameLocKey);

        string localizedDescription = LocalizationManager.Instance.GetTextOrKey(ShipsPackName, ship.descriptionLocKey);
        string localizedSlotsText = LocalizationManager.Instance.FormatText(
            UiPackName,
            "ship_select.slots.unlocked_format",
            ship.startingUnlockedModuleSlots,
            ship.totalModuleSlots
        );

        shipStatusPanel.SetDescriptionVisible(true);
        shipStatusPanel.SetTuningVisible(false);

        shipStatusPanel.SetDescriptionText(localizedDescription);
        shipStatusPanel.SetHullText(ship.baseHull.ToString());
        shipStatusPanel.SetDurationText(ship.baseLevelDurationSec.ToString("0") + "s");
        shipStatusPanel.SetMoneyText(ship.startingMoney.ToString());
        shipStatusPanel.SetOpenSlotsText(localizedSlotsText);

        RefreshEquippedModules(ship);
        RefreshShipImage(ship);
    }

    private void RefreshShipImage(ShipDefinition ship)
    {
        if (shipImageRenderer == null || ship == null)
            return;

        Sprite sprite = Resources.Load<Sprite>(ship.imagePath);

        if (sprite == null)
        {
            Debug.LogWarning("[ShipSelectController] Sprite introuvable: " + ship.imagePath);
            shipImageRenderer.sprite = null;
            return;
        }

        shipImageRenderer.sprite = sprite;
    }

    private string ResolveInitialShipIdForUI()
    {
        string id = DefaultShipId;

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            GameSaveData save = SaveManager.Instance.Current;

            if (save.runState != null &&
                save.runState.hasOngoingRun &&
                !string.IsNullOrEmpty(save.runState.currentShipId))
            {
                return save.runState.currentShipId;
            }

            if (!string.IsNullOrEmpty(save.selectedShipId))
                return save.selectedShipId;
        }

        if (RunConfig.Instance != null && !string.IsNullOrEmpty(RunConfig.Instance.SelectedShipId))
            id = RunConfig.Instance.SelectedShipId;

        return id;
    }

    /// <summary>
    /// Affiche les modules equipes de depart via ModulesListPanelUI.
    /// </summary>
    private void RefreshEquippedModules(ShipDefinition ship)
    {
        if (equippedModulesListPanel == null)
            return;

        List<ModuleDefinition> modules = new List<ModuleDefinition>();

        if (ship != null && ship.startingEquippedModuleIds != null)
        {
            for (int i = 0; i < ship.startingEquippedModuleIds.Count; i++)
            {
                string moduleId = ship.startingEquippedModuleIds[i];
                if (string.IsNullOrWhiteSpace(moduleId))
                    continue;

                ModuleDefinition def = ResolveModuleDefinition(moduleId);
                if (def != null)
                    modules.Add(def);
            }
        }

        string emptyText = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetTextOrKey(UiPackName, "ship_select.modules.none")
            : "Aucun module installe";

        string defaultHoverText = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetTextOrKey(UiPackName, "ship_select.modules.hover_for_details")
            : "Survolez un module pour voir les details";

        equippedModulesListPanel.ShowModules(modules, emptyText, defaultHoverText);
    }

    private ModuleDefinition ResolveModuleDefinition(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        if (ModuleCatalogService.Catalog == null ||
            ModuleCatalogService.Catalog.modules == null)
        {
            Debug.LogWarning("[ShipSelectController] ModuleCatalog non charge.");
            return null;
        }

        return ModuleCatalogService.Catalog.modules.FirstOrDefault(m => m != null && m.id == moduleId);
    }
}