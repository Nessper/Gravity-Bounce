using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Controle l affichage des slots d equipement dans Ship Systems.
///
/// Responsabilites :
/// - lit l etat des slots depuis RunSessionState
/// - resolve les ModuleDefinition
/// - recupere les icones via ModulesHubController
/// - construit une representation UI complete par slot
/// - envoie le resultat a ShipSystemsSlotsPanelUI
///
/// Etats supportes :
/// - slot verrouille
/// - slot ouvert vide
/// - slot ouvert avec module
///
/// Important :
/// - aucune logique metier d equipement ici
/// - toute la preparation de data se fait ici
/// </summary>
public class ShipSystemsEquippedSlotsController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private ModulesHubController modulesHub;

    [Header("UI")]
    [SerializeField] private ShipSystemsSlotsPanelUI slotsPanelUI;

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[ShipSystemsEquippedSlotsController] runSessionState non assigne.");
            enabled = false;
            return;
        }

        if (modulesHub == null)
        {
            Debug.LogError("[ShipSystemsEquippedSlotsController] modulesHub non assigne.");
            enabled = false;
            return;
        }

        if (slotsPanelUI == null)
        {
            Debug.LogError("[ShipSystemsEquippedSlotsController] slotsPanelUI non assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();

        if (slotsPanelUI != null)
            slotsPanelUI.Clear();
    }

    private void SubscribeEvents()
    {
        runSessionState.OnShipChanged.AddListener(OnShipChanged);
        runSessionState.OnEquipmentChanged.AddListener(OnEquipmentChanged);
    }

    private void UnsubscribeEvents()
    {
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
        runSessionState.OnEquipmentChanged.RemoveListener(OnEquipmentChanged);
    }

    public void RefreshUI()
    {
        List<ShipSystemSlotViewData> slots = BuildSlotViewData();
        slotsPanelUI.ShowSlots(slots);
    }

    private void OnShipChanged(string _)
    {
        RefreshUI();
    }

    private void OnEquipmentChanged()
    {
        RefreshUI();
    }

    private List<ShipSystemSlotViewData> BuildSlotViewData()
    {
        List<ShipSystemSlotViewData> result = new List<ShipSystemSlotViewData>();

        int slotCount = runSessionState.EquipmentSlotCount;

        for (int i = 0; i < slotCount; i++)
        {
            bool isLocked = runSessionState.IsEquipmentSlotLocked(i);
            string moduleId = isLocked ? null : runSessionState.GetEquippedModuleId(i);

            ModuleDefinition def = null;
            Sprite icon = null;

            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                def = ResolveModuleDefinition(moduleId);
                icon = modulesHub.GetModuleIconSprite(moduleId);
            }

            ShipSystemSlotViewData data = new ShipSystemSlotViewData
            {
                slotIndex = i,
                isLocked = isLocked,
                moduleId = moduleId,
                moduleDefinition = def,
                moduleIcon = icon
            };

            result.Add(data);
        }

        return result;
    }

    private ModuleDefinition ResolveModuleDefinition(string moduleId)
    {
        if (ModuleCatalogService.Catalog == null || ModuleCatalogService.Catalog.modules == null)
            return null;

        return ModuleCatalogService.Catalog.modules.FirstOrDefault(
            m => m != null && m.id == moduleId
        );
    }
}