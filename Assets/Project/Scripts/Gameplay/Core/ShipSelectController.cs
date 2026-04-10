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
/// - Afficher les modules equipes de depart du vaisseau.
///
/// Cette version est preparee pour une future transition visuelle
/// (fade UI + fermeture/ouverture des portes du hangar).
///
/// Bloc modules :
/// - Ligne 1 : X / Y emplacements debloques
/// - Ligne 2 : "aucun module installe" ou liste horizontale de modules equipes
/// - Ligne 3 : vide si aucun module, sinon texte d aide par defaut puis detail localise au hover
/// </summary>
public class ShipSelectController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private TMP_Text descriptionText;

    [SerializeField] private TMP_Text hullText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text openSlotsText;

    // SpriteRenderer utilise pour afficher visuellement le vaisseau
    // dans la zone principale de presentation.
    [SerializeField] private SpriteRenderer shipImageRenderer;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Modules UI")]
    // Parent horizontal qui accueille les icones des modules equipes.
    [SerializeField] private Transform equippedModulesRoot;

    // Prefab UI d une icone de module equipe.
    [SerializeField] private GameObject equippedModuleIconPrefab;

    // Texte alternatif affiche quand aucun module n est equipe.
    [SerializeField] private TMP_Text noModulesText;

    // Panneau qui affiche soit un texte d aide par defaut,
    // soit le detail du module actuellement survole.
    [SerializeField] private ModuleDetailsPanelUI moduleDetailsPanel;

    // Liste runtime des items UI modules actuellement instancies.
    // Sert notamment a debrancher proprement les events de hover.
    private readonly List<ModuleItemUI> equippedModuleItems = new List<ModuleItemUI>();

    // Association entre un item UI instancie et sa definition de module.
    // Utilise au moment du hover pour retrouver le bon ModuleDefinition.
    private readonly Dictionary<ModuleItemUI, ModuleDefinition> itemToModuleDef = new Dictionary<ModuleItemUI, ModuleDefinition>();

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

    /// <summary>
    /// Validation minimale au chargement du composant.
    ///
    /// Ici on verifie que le ShipCatalog est bien charge et non vide.
    /// Si ce n est pas le cas, on desactive le script pour eviter
    /// toute suite de NullReference plus loin.
    /// </summary>
    private void Awake()
    {
        if (!HasShips)
        {
            Debug.LogError("[ShipSelectController] ShipCatalog non charge ou vide.");
            enabled = false;
            return;
        }
    }

    /// <summary>
    /// Initialisation de l ecran.
    ///
    /// - Verifie certains services critiques.
    /// - Resout quel ship doit etre affiche au demarrage.
    /// - Positionne l index sur ce ship.
    /// - Rafraichit toute l UI.
    /// </summary>
    private void Start()
    {
        if (BootRoot.GameFlow == null)
            Debug.LogError("[ShipSelectController] BootRoot.GameFlow est null. ShipSelect doit etre charge depuis Boot/Title.");

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            Debug.LogError("[ShipSelectController] LocalizationManager non pret.");

        // Aligne directement le volume musique de titre sur sa cible
        // pour eviter un etat intermediaire bizarre si on arrive ici
        // depuis une autre scene.
        if (TitleMusicPlayer.Instance != null)
            TitleMusicPlayer.Instance.SnapToTargetVolume();

        string initialShipId = ResolveInitialShipIdForUI();
        int initialIndex = FindShipIndexById(initialShipId);

        // Si l id n existe pas dans le catalog, on retombe sur 0.
        ApplyShipByIndex(initialIndex >= 0 ? initialIndex : 0);
    }

    /// <summary>
    /// Navigation immediate vers le ship precedent.
    ///
    /// Cette methode est encore utile tant que les boutons appellent
    /// directement ce controller. Plus tard, un TransitionController
    /// pourra intercepter ce clic et piloter lui-meme la sequence visuelle.
    /// </summary>
    public void OnPreviousPressed()
    {
        if (!CanNavigate())
            return;

        ApplyShipByIndex(GetPreviousIndex());
    }

    /// <summary>
    /// Navigation immediate vers le ship suivant.
    ///
    /// Cette methode est encore utile tant que les boutons appellent
    /// directement ce controller. Plus tard, un TransitionController
    /// pourra intercepter ce clic et piloter lui-meme la sequence visuelle.
    /// </summary>
    public void OnNextPressed()
    {
        if (!CanNavigate())
            return;

        ApplyShipByIndex(GetNextIndex());
    }

    /// <summary>
    /// Retourne a l ecran titre.
    ///
    /// On demande au RunConfig de skipper l intro du titre
    /// lors du prochain chargement pour eviter un flow trop lourd.
    /// </summary>
    public void OnBackPressed()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;

        BootRoot.GameFlow.GoToTitle();
    }

    /// <summary>
    /// Lance une nouvelle run avec le ship actuellement selectionne.
    ///
    /// Cette methode :
    /// - valide les dependances critiques
    /// - recupere le ship courant
    /// - persiste la selection
    /// - initialise l etat runtime de la run
    /// - sauvegarde
    /// - lance la transition vers le RunHub apres fade musique
    /// </summary>
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

    /// <summary>
    /// Attend la fin du fade-out de la musique de titre
    /// avant de charger le RunHub.
    /// </summary>
    private IEnumerator StartAfterMusicFadeRoutine()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        BootRoot.GameFlow.GoToRunHub();
    }

    /// <summary>
    /// Indique si la navigation entre ships est possible.
    /// </summary>
    public bool CanNavigate()
    {
        List<ShipDefinition> ships = GetShips();
        return ships != null && ships.Count > 0;
    }

    /// <summary>
    /// Retourne l index precedent en wrap circulaire.
    /// Ne modifie pas l etat courant.
    /// </summary>
    public int GetPreviousIndex()
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return -1;

        return (currentIndex - 1 + ships.Count) % ships.Count;
    }

    /// <summary>
    /// Retourne l index suivant en wrap circulaire.
    /// Ne modifie pas l etat courant.
    /// </summary>
    public int GetNextIndex()
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return -1;

        return (currentIndex + 1) % ships.Count;
    }

    /// <summary>
    /// Applique un index de ship et rafraichit toute l UI.
    ///
    /// C est la methode cle pour la future transition :
    /// le TransitionController pourra l appeler une fois les portes fermees.
    /// </summary>
    public void ApplyShipByIndex(int newIndex)
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return;

        currentIndex = Mathf.Clamp(newIndex, 0, ships.Count - 1);
        RefreshUI();
    }

    /// <summary>
    /// Active ou desactive l interaction des boutons du Ship Select.
    ///
    /// Utile pendant une transition visuelle pour eviter
    /// les doubles clics et le spam input.
    /// </summary>
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

    /// <summary>
    /// Retourne le ship courant a partir de l index courant.
    /// </summary>
    public ShipDefinition GetCurrentShip()
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return null;

        currentIndex = Mathf.Clamp(currentIndex, 0, ships.Count - 1);
        return ships[currentIndex];
    }

    /// <summary>
    /// Retourne le ship correspondant a un index donne.
    /// </summary>
    public ShipDefinition GetShipAtIndex(int shipIndex)
    {
        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return null;

        if (shipIndex < 0 || shipIndex >= ships.Count)
            return null;

        return ships[shipIndex];
    }

    /// <summary>
    /// Retourne l index d un ship a partir de son id.
    /// Retourne -1 si introuvable.
    /// </summary>
    public int FindShipIndexById(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
            return -1;

        List<ShipDefinition> ships = GetShips();
        if (ships == null || ships.Count == 0)
            return -1;

        return ships.FindIndex(s => s != null && s.id == shipId);
    }

    /// <summary>
    /// Retourne la liste des ships depuis le catalog.
    /// Petit helper centralise pour eviter de repeter la meme expression partout.
    /// </summary>
    private List<ShipDefinition> GetShips()
    {
        return ShipCatalogService.Catalog != null
            ? ShipCatalogService.Catalog.ships
            : null;
    }

    /// <summary>
    /// Rafraichit l ensemble de l UI a partir du ship courant.
    ///
    /// Concretement :
    /// - nom
    /// - description
    /// - stats
    /// - texte slots
    /// - bloc modules
    /// - image principale du vaisseau
    /// </summary>
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

        if (descriptionText != null)
            descriptionText.text = LocalizationManager.Instance.GetTextOrKey(ShipsPackName, ship.descriptionLocKey);

        if (hullText != null)
            hullText.text = ship.baseHull.ToString();

        if (durationText != null)
            durationText.text = ship.baseLevelDurationSec.ToString("0") + "s";

        if (moneyText != null)
            moneyText.text = ship.startingMoney.ToString();

        if (openSlotsText != null)
        {
            openSlotsText.text = LocalizationManager.Instance.FormatText(
                UiPackName,
                "ship_select.slots.unlocked_format",
                ship.startingUnlockedModuleSlots,
                ship.totalModuleSlots
            );
        }

        RefreshEquippedModules(ship);
        RefreshShipImage(ship);
    }

    /// <summary>
    /// Charge et affiche le sprite principal du ship courant.
    ///
    /// Le chargement se fait depuis Resources a partir du imagePath
    /// contenu dans le ShipDefinition.
    /// </summary>
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

    /// <summary>
    /// Determine quel ship doit etre affiche a l ouverture du Ship Select.
    ///
    /// Priorite :
    /// 1. Ship de la run en cours s il y a une run active
    /// 2. selectedShipId sauvegarde
    /// 3. SelectedShipId du RunConfig
    /// 4. DefaultShipId
    /// </summary>
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
    /// Reconstruit integralement le bloc UI des modules equipes.
    ///
    /// Flow :
    /// - clear de l UI precedente
    /// - reset du panneau de details
    /// - extraction des ids modules equipes au depart
    /// - affichage soit du texte "aucun module", soit des icones
    /// - initialisation du texte d aide par defaut sur le panneau de details
    /// </summary>
    private void RefreshEquippedModules(ShipDefinition ship)
    {
        ClearEquippedModulesUI();

        if (moduleDetailsPanel != null)
            moduleDetailsPanel.Clear();

        if (ship == null)
            return;

        List<string> equippedIds = ship.startingEquippedModuleIds == null
            ? new List<string>()
            : ship.startingEquippedModuleIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

        bool hasModules = equippedIds.Count > 0;

        if (equippedModulesRoot != null)
            equippedModulesRoot.gameObject.SetActive(hasModules);

        if (noModulesText != null)
        {
            noModulesText.gameObject.SetActive(!hasModules);

            if (!hasModules)
            {
                noModulesText.text = LocalizationManager.Instance.GetTextOrKey(
                    UiPackName,
                    "ship_select.modules.none"
                );
            }
        }

        if (!hasModules)
            return;

        if (moduleDetailsPanel != null)
        {
            string defaultText = LocalizationManager.Instance.GetTextOrKey(
                UiPackName,
                "ship_select.modules.hover_for_details"
            );

            moduleDetailsPanel.SetDefaultText(defaultText);
            moduleDetailsPanel.ShowDefault();
        }

        for (int i = 0; i < equippedIds.Count; i++)
            CreateEquippedModuleItem(equippedIds[i], i);
    }

    /// <summary>
    /// Nettoie completement l UI des modules equipes deja affiches.
    ///
    /// Important :
    /// - on debranche les events de hover des items existants
    /// - on vide les collections runtime
    /// - on detruit tous les enfants UI du root modules
    /// </summary>
    private void ClearEquippedModulesUI()
    {
        for (int i = 0; i < equippedModuleItems.Count; i++)
        {
            ModuleItemUI item = equippedModuleItems[i];
            if (item == null)
                continue;

            item.HoverEntered -= OnModuleHoverEntered;
            item.HoverExited -= OnModuleHoverExited;
        }

        equippedModuleItems.Clear();
        itemToModuleDef.Clear();

        if (equippedModulesRoot == null)
            return;

        for (int i = equippedModulesRoot.childCount - 1; i >= 0; i--)
            Destroy(equippedModulesRoot.GetChild(i).gameObject);
    }

    /// <summary>
    /// Instancie un item UI pour un module equipe donne.
    ///
    /// Etapes :
    /// - resolve la ModuleDefinition a partir de l id
    /// - instancie le prefab
    /// - bind visuellement l item
    /// - branche les events hover
    /// - memorise la relation item <-> definition
    /// </summary>
    private void CreateEquippedModuleItem(string moduleId, int itemIndex)
    {
        if (equippedModulesRoot == null || equippedModuleIconPrefab == null)
            return;

        ModuleDefinition def = ResolveModuleDefinition(moduleId);
        if (def == null)
        {
            Debug.LogWarning("[ShipSelectController] ModuleDefinition introuvable pour: " + moduleId);
            return;
        }

        GameObject instance = Instantiate(equippedModuleIconPrefab, equippedModulesRoot);
        instance.name = "EquippedModule_" + itemIndex + "_" + moduleId;

        ModuleItemUI item = instance.GetComponent<ModuleItemUI>();
        if (item == null)
        {
            Debug.LogError("[ShipSelectController] Le prefab de module n'a pas de ModuleItemUI.");
            Destroy(instance);
            return;
        }

        item.Bind(def);
        item.HoverEntered += OnModuleHoverEntered;
        item.HoverExited += OnModuleHoverExited;

        equippedModuleItems.Add(item);
        itemToModuleDef[item] = def;
    }

    /// <summary>
    /// Callback appele quand la souris entre sur une icone de module.
    ///
    /// On retrouve la ModuleDefinition correspondante,
    /// puis on demande au panneau de details de l afficher.
    /// </summary>
    private void OnModuleHoverEntered(ModuleItemUI item)
    {
        if (moduleDetailsPanel == null || item == null)
            return;

        if (!itemToModuleDef.TryGetValue(item, out ModuleDefinition def) || def == null)
            return;

        moduleDetailsPanel.ShowModule(def);
    }

    /// <summary>
    /// Callback appele quand la souris quitte une icone de module.
    ///
    /// On revient au texte d aide par defaut.
    /// </summary>
    private void OnModuleHoverExited(ModuleItemUI item)
    {
        if (moduleDetailsPanel == null)
            return;

        moduleDetailsPanel.ShowDefault();
    }

    /// <summary>
    /// Recherche une ModuleDefinition dans le ModuleCatalog
    /// a partir de son id.
    /// </summary>
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