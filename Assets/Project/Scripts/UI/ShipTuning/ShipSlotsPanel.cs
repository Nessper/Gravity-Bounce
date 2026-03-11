using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel qui pilote les slots modules du vaisseau (index-safe).
/// - Ecoute la selection de module
/// - Calcule les slots valides (via hub)
/// - Au clic: inspect / equip / swap / desequip
/// - Rafraichit l'affichage (locked/unlocked + icone + tier)
///
/// Regle UX actuelle:
/// - Pas de "slot invalide" en V1.
/// - Locked: ferme.
/// - Unlocked + module selectionne ET equipable: slots ouverts surlignes.
/// - Aucun module selectionne: pas de surlignage.
/// </summary>
public class ShipSlotsPanel : MonoBehaviour
{
    [Header("Deps")]
    [SerializeField] private ModulesHubController hub;
    [SerializeField] private ModulesSelectionController selection;

    [Header("Slots (6)")]
    [SerializeField] private ShipModuleSlotButtonView[] slots = new ShipModuleSlotButtonView[6];

    // Slots valides pour la selection courante (par slotIndex)
    private readonly HashSet<int> validSlotIndices = new HashSet<int>();

    private void OnEnable()
    {
        if (selection != null)
            selection.OnSelectionChanged += HandleSelectionChanged;

        if (hub != null)
        {
            hub.OnInventoryChanged += RefreshAll;
            hub.OnEquipmentChanged += RefreshAll;
        }

        WireSlotClicks();
        RefreshAll();

        // Sync immediat sur la selection actuelle
        HandleSelectionChanged(selection != null ? selection.SelectedModuleId : null);
    }

    private void OnDisable()
    {
        if (selection != null)
            selection.OnSelectionChanged -= HandleSelectionChanged;

        if (hub != null)
        {
            hub.OnInventoryChanged -= RefreshAll;
            hub.OnEquipmentChanged -= RefreshAll;
        }

        UnwireSlotClicks();
    }

    private void WireSlotClicks()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].OnClicked += OnSlotClicked;
        }
    }

    private void UnwireSlotClicks()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].OnClicked -= OnSlotClicked;
        }
    }

    private void HandleSelectionChanged(string moduleId)
    {
        validSlotIndices.Clear();

        // Aucun module selectionne: pas de slots valides, on clear le visuel.
        if (hub == null || string.IsNullOrEmpty(moduleId))
        {
            ApplyValidVisuals(hasSelection: false);
            return;
        }

        // Demande au hub les slots valides (dans V1: tous les slots ouverts si le module est equipable)
        List<int> valid = hub.GetOpenSlots();
        if (valid != null)
        {
            for (int i = 0; i < valid.Count; i++)
                validSlotIndices.Add(valid[i]);
        }

        ApplyValidVisuals(hasSelection: true);
    }

    private void ApplyValidVisuals(bool hasSelection)
    {
        if (hub == null || slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            ShipModuleSlotButtonView slot = slots[i];
            if (slot == null) continue;

            int slotIndex = slot.SlotIndex;

            bool locked = hub.IsSlotLocked(slotIndex);
            slot.SetLocked(locked);

            if (!hasSelection)
            {
                // Aucun module selectionne: on retire tout feedback UX
                slot.ClearSelectionVisual(locked);
                continue;
            }

            // Module selectionne: slot "valide" = present dans validSlotIndices
            bool isValid = validSlotIndices.Contains(slotIndex);
            slot.SetValidForSelection(isValid, locked);
        }
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (hub == null || selection == null)
            return;

        string selected = selection.SelectedModuleId;
        string equipped = hub.GetEquippedModuleIdInSlot(slotIndex);

        // 1) Si un module est selectionne: action equip/remplacer/desequip
        if (!string.IsNullOrEmpty(selected))
        {
            // Re-clic sur le meme module dans ce slot => desequip
            if (!string.IsNullOrEmpty(equipped) && equipped == selected)
            {
                bool unequipped = hub.TryUnequipSlot(slotIndex);
                if (unequipped)
                    selection.Clear();

                return;
            }

            // Tentative equip (slot vide OU occupe = remplacement)
            if (!validSlotIndices.Contains(slotIndex))
                return;

            bool ok = hub.TryEquipModuleInSlot(selected, slotIndex);
            if (!ok)
            {
                selection.Refresh(); // force ModulesDescription_Panel.Show(selected) à rerun
                return;
            }


            selection.Clear();
            return;

        }

        // 2) Sinon: mode inspect
        if (!string.IsNullOrEmpty(equipped))
        {
            selection.Select(equipped);
            return;
        }

        // Slot vide + aucune selection: rien
    }

    private void RefreshAll()
    {
        if (hub == null || slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            ShipModuleSlotButtonView slot = slots[i];
            if (slot == null) continue;

            int slotIndex = slot.SlotIndex;

            bool locked = hub.IsSlotLocked(slotIndex);

            string equippedId = hub.GetEquippedModuleIdInSlot(slotIndex);

            int tier = 0;
            if (!string.IsNullOrEmpty(equippedId) &&
                hub.TryGetModuleById(equippedId, out ModuleDefinition def) &&
                def != null)
            {
                tier = Mathf.Max(1, def.tier);
            }

            slot.SetTier(tier);
            slot.SetLocked(locked);

            Sprite sprite = hub.GetModuleIconSprite(equippedId);
            slot.SetEquippedSprite(sprite);
        }

        HandleSelectionChanged(selection != null ? selection.SelectedModuleId : null);
    }
}
