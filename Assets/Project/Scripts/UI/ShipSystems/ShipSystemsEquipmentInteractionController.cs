using UnityEngine;

/// <summary>
/// Orchestre les interactions d equipement dans Ship Systems.
///
/// Responsabilites :
/// - ecouter les clics sur les slots
/// - ecouter les clics sur les modules possedes
/// - maintenir la selection du slot courant
/// - maintenir la selection du module owned courant
/// - deleguer l equipement / desequipement au RunSessionState
/// - rafraichir les panneaux UI concernes
///
/// Flows supportes :
/// - slot -> module
/// - module -> slot
/// - reclic slot selectionne -> desequipement ou deselection
/// - reclic module selectionne -> deselection
///
/// Important :
/// - ne construit pas les ViewData
/// - ne lit pas directement la save
/// - ne modifie pas l equipement a la main
/// - utilise uniquement les APIs publiques de RunSessionState
/// </summary>
public class ShipSystemsEquipmentInteractionController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Controllers")]
    [SerializeField] private ShipSystemsEquippedSlotsController equippedSlotsController;
    [SerializeField] private ShipSystemsOwnedModulesController ownedModulesController;

    [Header("Panels")]
    [SerializeField] private ShipSystemsSlotsPanelUI slotsPanelUI;
    [SerializeField] private ModulesListPanelUI ownedModulesListPanel;
    [SerializeField] private ModuleDetailsPanelUI detailsPanel;

    private int selectedSlotIndex = -1;
    private ModuleDefinition selectedOwnedModule;

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[ShipSystemsEquipmentInteractionController] runSessionState non assigne.");
            enabled = false;
            return;
        }

        if (equippedSlotsController == null)
        {
            Debug.LogError("[ShipSystemsEquipmentInteractionController] equippedSlotsController non assigne.");
            enabled = false;
            return;
        }

        if (ownedModulesController == null)
        {
            Debug.LogError("[ShipSystemsEquipmentInteractionController] ownedModulesController non assigne.");
            enabled = false;
            return;
        }

        if (slotsPanelUI == null)
        {
            Debug.LogError("[ShipSystemsEquipmentInteractionController] slotsPanelUI non assigne.");
            enabled = false;
            return;
        }

        if (ownedModulesListPanel == null)
        {
            Debug.LogError("[ShipSystemsEquipmentInteractionController] ownedModulesListPanel non assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        slotsPanelUI.OnSlotClicked += HandleSlotClicked;
        ownedModulesListPanel.OnModuleClicked += HandleOwnedModuleClicked;
        ownedModulesListPanel.OnModuleDoubleClicked += HandleOwnedModuleDoubleClicked;

        runSessionState.OnEquipmentChanged.AddListener(OnEquipmentChanged);
        runSessionState.OnShipChanged.AddListener(OnShipChanged);
        ShipSystemsOverlayTransitionController.ShipSystemsUiHidden += ClearSelectionOnOverlayExit;

        RefreshAllUI();
    }

    private void OnDisable()
    {
        slotsPanelUI.OnSlotClicked -= HandleSlotClicked;
        ownedModulesListPanel.OnModuleClicked -= HandleOwnedModuleClicked;
        ownedModulesListPanel.OnModuleDoubleClicked -= HandleOwnedModuleDoubleClicked;

        runSessionState.OnEquipmentChanged.RemoveListener(OnEquipmentChanged);
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
        ShipSystemsOverlayTransitionController.ShipSystemsUiHidden -= ClearSelectionOnOverlayExit;
    }

    private void OnEquipmentChanged()
    {
        RefreshAllUI();
    }

    private void OnShipChanged(string _)
    {
        ClearSelections();
        RefreshAllUI();
    }

    private void HandleSlotClicked(ShipSystemSlotViewData clickedSlot)
    {
        if (clickedSlot == null || clickedSlot.isLocked)
            return;

        bool isReclicSameSlot = selectedSlotIndex == clickedSlot.slotIndex;

        // Un module selected doit pouvoir etre equipe dans tout slot libre,
        // y compris celui automatiquement surligne a sa selection.
        if (selectedOwnedModule != null &&
            (!isReclicSameSlot || !clickedSlot.HasModule))
        {
            TryEquipSelectedModuleIntoSlot(clickedSlot.slotIndex);
            return;
        }

        if (isReclicSameSlot)
        {
            if (clickedSlot.HasModule)
                runSessionState.UnequipSlot(clickedSlot.slotIndex);

            selectedSlotIndex = -1;
            RefreshAllUI();
            return;
        }

        selectedSlotIndex = clickedSlot.slotIndex;
        RefreshSlotSelectionOnly();
    }

    private void HandleOwnedModuleClicked(ModuleDefinition moduleDef)
    {
        if (moduleDef == null)
            return;

        bool isSameModule =
            selectedOwnedModule != null &&
            selectedOwnedModule.id == moduleDef.id;

        if (isSameModule)
        {
            ClearSelections();
            RefreshSlotSelectionOnly();
            RefreshOwnedSelectionOnly();
            return;
        }

        if (selectedSlotIndex < 0)
        {
            selectedOwnedModule = moduleDef;
            selectedSlotIndex = selectedOwnedModule != null
                ? FindFirstOpenEmptySlotIndex()
                : -1;

            RefreshSlotSelectionOnly();
            RefreshOwnedSelectionOnly();
            return;
        }

        TryEquipModuleIntoSelectedSlot(moduleDef);
    }

    private void HandleOwnedModuleDoubleClicked(ModuleDefinition moduleDef)
    {
        if (moduleDef == null)
            return;

        int emptySlotIndex = FindFirstOpenEmptySlotIndex();
        if (emptySlotIndex < 0)
            return;

        selectedSlotIndex = emptySlotIndex;
        TryEquipModuleIntoSelectedSlot(moduleDef);
    }

    private void TryEquipModuleIntoSelectedSlot(ModuleDefinition moduleDef)
    {
        if (moduleDef == null)
            return;

        if (selectedSlotIndex < 0)
            return;

        if (runSessionState.IsEquipmentSlotLocked(selectedSlotIndex))
        {
            selectedSlotIndex = -1;
            RefreshAllUI();
            return;
        }

        bool equipped = runSessionState.TryEquipModuleToSlot(moduleDef.id, selectedSlotIndex);

        if (equipped)
        {
            ClearSelections();
            RefreshAllUI();
            return;
        }

        ShowEquipFailure(moduleDef, selectedSlotIndex);

        selectedOwnedModule = moduleDef;
        selectedSlotIndex = -1;

        RefreshAllUI();
        RefreshOwnedSelectionOnly();
    }

    private void TryEquipSelectedModuleIntoSlot(int slotIndex)
    {
        if (selectedOwnedModule == null)
            return;

        if (slotIndex < 0)
            return;

        if (runSessionState.IsEquipmentSlotLocked(slotIndex))
        {
            selectedSlotIndex = -1;
            RefreshAllUI();
            return;
        }

        bool equipped = runSessionState.TryEquipModuleToSlot(selectedOwnedModule.id, slotIndex);

        if (equipped)
        {
            ClearSelections();
            RefreshAllUI();
            return;
        }

        ShowEquipFailure(selectedOwnedModule, slotIndex);

        selectedSlotIndex = -1;

        RefreshAllUI();
        RefreshOwnedSelectionOnly();
    }

    private void RefreshAllUI()
    {
        equippedSlotsController.RefreshUI();
        ownedModulesController.RefreshUI();

        RefreshSlotSelectionOnly();
        RefreshOwnedSelectionOnly();
    }

    private void RefreshSlotSelectionOnly()
    {
        if (slotsPanelUI != null)
            slotsPanelUI.SetSelectedSlotIndex(selectedSlotIndex);
    }

    private void RefreshOwnedSelectionOnly()
    {
        if (ownedModulesListPanel == null)
            return;

        if (selectedOwnedModule == null)
        {
            ownedModulesListPanel.ClearSelection();
            return;
        }

        ownedModulesListPanel.SetSelectedModule(selectedOwnedModule);
    }

    private void ClearSelections()
    {
        selectedSlotIndex = -1;
        selectedOwnedModule = null;
    }

    private void ClearSelectionOnOverlayExit()
    {
        ClearSelections();
        RefreshSlotSelectionOnly();
        RefreshOwnedSelectionOnly();
    }

    private int FindFirstOpenEmptySlotIndex()
    {
        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            if (runSessionState.IsEquipmentSlotLocked(i))
                continue;

            if (string.IsNullOrWhiteSpace(runSessionState.GetEquippedModuleId(i)))
                return i;
        }

        return -1;
    }

    private void ShowEquipFailure(ModuleDefinition targetDef, int slotIndex)
    {
        if (targetDef == null)
            return;

        if (detailsPanel != null)
            detailsPanel.ShowModule(targetDef);

        if (runSessionState.TryExplainEquipFailure(
                targetDef.id,
                slotIndex,
                out RunSessionState.EquipFailReason reason,
                out string missingPrereqId))
        {
            Debug.Log(
                "[ShipSystemsEquipmentInteractionController] Equip fail. " +
                "moduleId=" + targetDef.id +
                " slotIndex=" + slotIndex +
                " reason=" + reason +
                " missingPrereqId=" + missingPrereqId
            );
        }
    }
}
