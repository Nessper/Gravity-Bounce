using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panneau UI responsable d afficher la ligne de slots dans Ship Systems.
///
/// Responsabilites :
/// - instancier / recycler les items de slots
/// - binder chaque slot
/// - gerer la selection visuelle
/// - relayer les interactions UI (clic / hover / fin de hover)
/// - afficher les details en mode preview lors du hover
/// - restaurer un etat de details stable apres le hover
///
/// Important :
/// - aucune logique metier ici
/// - aucune lecture directe de RunSessionState ici
/// - aucune dependance au ModulesHubController ici
/// - aucun equip / desequip / swap ici
/// </summary>
public class ShipSystemsSlotsPanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform itemsRoot;
    [SerializeField] private ShipSystemsSlotItemUI slotItemPrefab;
    [SerializeField] private ModuleDetailsPanelUI detailsPanel;

    [Header("Default Details")]
    [SerializeField] private string defaultDetailsText = "Selectionnez un emplacement ou un module";

    private readonly List<ShipSystemsSlotItemUI> spawnedItems = new List<ShipSystemsSlotItemUI>();

    private int selectedSlotIndex = -1;
    private bool isHoveringSlot;

    /// <summary>
    /// Remonte un clic sur slot au controller.
    /// Le controller decide ensuite quoi faire :
    /// - simple selection
    /// - desequipement
    /// - refresh complet
    /// </summary>
    public event Action<ShipSystemSlotViewData> OnSlotClicked;

    /// <summary>
    /// Remonte le debut de hover si le controller veut reagir.
    /// Optionnel, mais utile pour garder une architecture symetrique.
    /// </summary>
    public event Action<ShipSystemSlotViewData> OnSlotHoverStarted;

    /// <summary>
    /// Remonte la fin de hover si le controller veut reagir.
    /// </summary>
    public event Action OnSlotHoverEnded;

    private void Awake()
    {
        if (itemsRoot == null)
        {
            Debug.LogError("[ShipSystemsSlotsPanelUI] itemsRoot non assigne.");
            enabled = false;
            return;
        }

        if (slotItemPrefab == null)
        {
            Debug.LogError("[ShipSystemsSlotsPanelUI] slotItemPrefab non assigne.");
            enabled = false;
            return;
        }
    }

    /// <summary>
    /// Vide completement le panneau.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            ShipSystemsSlotItemUI item = spawnedItems[i];
            if (item == null)
                continue;

            item.OnSlotClicked -= HandleSlotClicked;
            item.OnSlotHoverStarted -= HandleSlotHoverStarted;
            item.OnSlotHoverEnded -= HandleSlotHoverEnded;

            Destroy(item.gameObject);
        }

        spawnedItems.Clear();
        selectedSlotIndex = -1;
        isHoveringSlot = false;

        if (detailsPanel != null)
        {
            detailsPanel.Clear();
            detailsPanel.SetDefaultText(defaultDetailsText);
            detailsPanel.ShowDefault();
        }
    }

    /// <summary>
    /// Affiche une nouvelle liste de slots.
    /// </summary>
    public void ShowSlots(List<ShipSystemSlotViewData> slots)
    {
        Clear();

        if (slots == null || slots.Count == 0)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            ShipSystemSlotViewData slotData = slots[i];
            ShipSystemsSlotItemUI item = Instantiate(slotItemPrefab, itemsRoot);

            item.Bind(slotData);
            item.OnSlotClicked += HandleSlotClicked;
            item.OnSlotHoverStarted += HandleSlotHoverStarted;
            item.OnSlotHoverEnded += HandleSlotHoverEnded;

            spawnedItems.Add(item);
        }

        RefreshSelectionVisuals();
        RefreshStableDetails();
    }

    /// <summary>
    /// Definit quel slot est considere comme selectionne visuellement.
    /// Cette methode ne declenche aucune logique metier.
    /// </summary>
    public void SetSelectedSlotIndex(int slotIndex)
    {
        selectedSlotIndex = slotIndex;
        RefreshSelectionVisuals();

        if (!isHoveringSlot)
            RefreshStableDetails();
    }

    /// <summary>
    /// Efface la selection visuelle actuelle.
    /// </summary>
    public void ClearSelection()
    {
        selectedSlotIndex = -1;
        RefreshSelectionVisuals();

        if (!isHoveringSlot)
            RefreshStableDetails();
    }

    /// <summary>
    /// Retourne la data du slot actuellement selectionne.
    /// Peut etre utile au controller si besoin.
    /// </summary>
    public ShipSystemSlotViewData GetSelectedSlotData()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            ShipSystemsSlotItemUI item = spawnedItems[i];
            if (item == null || item.CurrentData == null)
                continue;

            if (item.CurrentData.slotIndex == selectedSlotIndex)
                return item.CurrentData;
        }

        return null;
    }

    private void HandleSlotClicked(ShipSystemSlotViewData data)
    {
        if (data == null)
            return;

        // Important :
        // Ici, le panneau UI ne fait QUE gerer la selection visuelle locale.
        // Le controller decide ensuite si ce clic signifie :
        // - selection
        // - desequipement
        // - autre action metier
        selectedSlotIndex = data.slotIndex;
        RefreshSelectionVisuals();

        // Le clic ne pilote pas les details.
        // Les details restent geres par le hover ou par l etat stable.
        OnSlotClicked?.Invoke(data);
    }

    private void HandleSlotHoverStarted(ShipSystemSlotViewData data)
    {
        isHoveringSlot = true;

        RefreshHoverDetails(data);
        OnSlotHoverStarted?.Invoke(data);
    }

    private void HandleSlotHoverEnded()
    {
        isHoveringSlot = false;

        RefreshStableDetails();
        OnSlotHoverEnded?.Invoke();
    }

    /// <summary>
    /// Met a jour l etat visuel "selected" sur tous les items.
    /// </summary>
    private void RefreshSelectionVisuals()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            ShipSystemsSlotItemUI item = spawnedItems[i];
            if (item == null || item.CurrentData == null)
                continue;

            bool isSelected = item.CurrentData.slotIndex == selectedSlotIndex;
            item.SetSelected(isSelected);
        }
    }

    /// <summary>
    /// Affiche les details temporaires pendant le hover.
    /// </summary>
    private void RefreshHoverDetails(ShipSystemSlotViewData data)
    {
        if (detailsPanel == null)
            return;

        if (data == null)
        {
            detailsPanel.ShowDefault();
            return;
        }

        if (data.isLocked)
        {
            detailsPanel.ShowDefault();
            return;
        }

        if (data.moduleDefinition != null)
        {
            detailsPanel.ShowModule(data.moduleDefinition);
            return;
        }

        // Slot vide
        detailsPanel.ShowDefault();
    }

    /// <summary>
    /// Affiche l etat de details "stable" quand on ne survole aucun slot.
    ///
    /// Regle choisie :
    /// - si un slot selectionne contient un module valide, on peut l afficher
    /// - sinon on revient au texte par defaut
    ///
    /// Si tu preferes un comportement plus neutre, tu peux facilement
    /// remplacer cette methode pour toujours faire ShowDefault().
    /// </summary>
    private void RefreshStableDetails()
    {
        if (detailsPanel == null)
            return;

        ShipSystemSlotViewData selectedData = GetSelectedSlotData();

        if (selectedData == null)
        {
            detailsPanel.SetDefaultText(defaultDetailsText);
            detailsPanel.ShowDefault();
            return;
        }

        if (selectedData.isLocked || selectedData.moduleDefinition == null)
        {
            detailsPanel.SetDefaultText(defaultDetailsText);
            detailsPanel.ShowDefault();
            return;
        }

        detailsPanel.ShowModule(selectedData.moduleDefinition);
    }
}