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

        runSessionState.OnEquipmentChanged.AddListener(OnEquipmentChanged);
        runSessionState.OnShipChanged.AddListener(OnShipChanged);

        RefreshAllUI();
    }

    private void OnDisable()
    {
        slotsPanelUI.OnSlotClicked -= HandleSlotClicked;
        ownedModulesListPanel.OnModuleClicked -= HandleOwnedModuleClicked;

        runSessionState.OnEquipmentChanged.RemoveListener(OnEquipmentChanged);
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
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

        if (selectedOwnedModule != null && !isReclicSameSlot)
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

        if (selectedSlotIndex < 0)
        {
            selectedOwnedModule = isSameModule ? null : moduleDef;
            RefreshOwnedSelectionOnly();
            return;
        }

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