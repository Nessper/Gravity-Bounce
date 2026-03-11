using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

/// <summary>
/// Vue d'un slot module (1 bouton).
/// Responsabilités :
/// - Afficher l'etat : Locked / Unlocked / Equipped
/// - Afficher le tier du module equipe
/// - Afficher un feedback de selection (slots valides) via highlight
/// - Exposer un evenement de clic avec l'index du slot
///
/// Convention UI :
/// - Desactive_Img : slot verrouille (ferme)
/// - Active_Img : slot ouvert (disponible)
/// - Module_Img : icone du module equipe (si present)
/// - SelectionHighlight (optionnel) : surbrillance quand slot valide pour la selection
/// </summary>
public class ShipModuleSlotButtonView : MonoBehaviour
{
    [Header("Slot")]
    [SerializeField] private int slotIndex = 0;

    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image desactiveImg;
    [SerializeField] private Image activeImg;
    [SerializeField] private Image moduleImg;
    [SerializeField] private TMP_Text tierText;

    [Header("Selection Visual")]
    [Tooltip("Optionnel: image de surbrillance (halo/outline). Doit avoir Raycast Target OFF.")]
    [SerializeField] private Image selectionHighlight;

    public int SlotIndex => slotIndex;

    /// <summary>
    /// Event emis quand le slot est clique.
    /// </summary>
    public event Action<int> OnClicked;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClick);
        else
            Debug.LogError("[ShipModuleSlotButtonView] Button manquant.");

        // Securite: le highlight ne doit jamais bloquer les clics
        if (selectionHighlight != null)
            selectionHighlight.raycastTarget = false;

        // Par defaut : pas de module affiche + pas de highlight
        SetEquippedSprite(null);
        ClearSelectionVisual(isLocked: false);
        SetTier(0);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        OnClicked?.Invoke(slotIndex);
    }

    /// <summary>
    /// Locked => Desactive visible, Active cache, bouton non cliquable.
    /// Unlocked => Active visible, Desactive cache, bouton cliquable.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (button != null)
            button.interactable = !locked;

        if (desactiveImg != null)
            desactiveImg.gameObject.SetActive(locked);

        if (activeImg != null)
            activeImg.gameObject.SetActive(!locked);

        // Si locked, on coupe le highlight (evite un etat incoherent)
        if (locked)
            ClearSelectionVisual(isLocked: true);
    }

    /// <summary>
    /// Affiche/masque l'icone du module equipe.
    /// sprite null => Module_Img cache.
    /// </summary>
    public void SetEquippedSprite(Sprite sprite)
    {
        if (moduleImg == null) return;

        if (sprite == null)
        {
            moduleImg.sprite = null;
            moduleImg.enabled = false;
            return;
        }

        moduleImg.sprite = sprite;
        moduleImg.enabled = true;
    }

    /// <summary>
    /// Indique si ce slot est un emplacement valide pour le module selectionne.
    /// Regle UX actuelle:
    /// - Pas de "slot invalide" en V1.
    /// - isValid => highlight ON
    /// - sinon => highlight OFF
    /// </summary>
    public void SetValidForSelection(bool isValid, bool isLocked)
    {
        if (isLocked)
            return;

        if (selectionHighlight != null)
            selectionHighlight.enabled = isValid;
    }

    /// <summary>
    /// Remet le visuel de selection a l'etat neutre.
    /// </summary>
    public void ClearSelectionVisual(bool isLocked)
    {
        if (isLocked)
            return;

        if (selectionHighlight != null)
            selectionHighlight.enabled = false;
    }

    /// <summary>
    /// Affiche le tier du module equipe.
    /// tier <= 0 => masque le texte.
    /// </summary>
    public void SetTier(int tier)
    {
        if (tierText == null)
            return;

        if (tier <= 0)
        {
            tierText.text = "";
            tierText.enabled = false;
            return;
        }

        tierText.text = "T" + tier;
        tierText.enabled = true;
    }
}
