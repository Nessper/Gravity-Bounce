using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Vue UI d un slot dans Ship Systems.
///
/// Etats supportes :
/// - verrouille
/// - ouvert vide
/// - ouvert avec module
///
/// Interactions supportees :
/// - clic
/// - hover entree
/// - hover sortie
///
/// Important :
/// - aucune logique metier ici
/// - aucune logique d equipement ici
/// - cette vue ne fait qu afficher l etat d un slot
/// </summary>
public class ShipSystemsSlotItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Roots")]
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private GameObject slotBaseRoot;
    [SerializeField] private GameObject moduleRoot;

    [Header("Module UI")]
    [SerializeField] private Image moduleIconImage;
    [SerializeField] private TMP_Text tierText;

    [Header("Selection / Focus")]
    [SerializeField] private GameObject selectedHighlightRoot;

    private ShipSystemSlotViewData currentData;

    public event Action<ShipSystemSlotViewData> OnSlotClicked;
    public event Action<ShipSystemSlotViewData> OnSlotHoverStarted;
    public event Action OnSlotHoverEnded;

    public ShipSystemSlotViewData CurrentData => currentData;

    /// <summary>
    /// Bind complet du slot.
    /// </summary>
    public void Bind(ShipSystemSlotViewData data)
    {
        currentData = data;

        bool isLocked = data != null && data.isLocked;
        bool hasModule = data != null && data.HasModule;

        SetRootVisible(lockedRoot, isLocked);
        SetRootVisible(slotBaseRoot, !isLocked);
        SetRootVisible(moduleRoot, !isLocked && hasModule);

        if (moduleIconImage != null)
        {
            moduleIconImage.sprite = data != null ? data.moduleIcon : null;
            moduleIconImage.enabled =
                data != null &&
                data.moduleIcon != null &&
                !isLocked &&
                hasModule;
        }

        if (tierText != null)
        {
            if (!isLocked && hasModule && data.moduleDefinition != null)
                tierText.text = BuildTierLabel(data.moduleDefinition.tier);
            else
                tierText.text = string.Empty;
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        SetRootVisible(selectedHighlightRoot, selected);
    }

    /// <summary>
    /// A brancher sur le Button de l item.
    /// </summary>
    public void OnPressed()
    {
        if (currentData == null)
            return;

        if (currentData.isLocked)
            return;

        OnSlotClicked?.Invoke(currentData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentData == null)
            return;

        if (currentData.isLocked)
            return;

        OnSlotHoverStarted?.Invoke(currentData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentData == null)
            return;

        if (currentData.isLocked)
            return;

        OnSlotHoverEnded?.Invoke();
    }

    private void SetRootVisible(GameObject go, bool visible)
    {
        if (go != null)
            go.SetActive(visible);
    }

    private string BuildTierLabel(int tier)
    {
        if (tier <= 0)
            return string.Empty;

        return "T" + tier;
    }
}