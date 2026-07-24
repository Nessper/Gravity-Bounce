using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Vue UI compacte reutilisable pour un module.
///
/// Responsabilites :
/// - affiche icone + tier + prix
/// - hover => highlight + scale
/// - clic => remonte une selection
/// - expose un etat visuel selectionne persistent
///
/// Important :
/// - ne connait pas le shop
/// - ne connait pas le details panel
/// - ne connait pas la localisation
/// </summary>
public class ModuleItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text priceText;

    [Header("Focus")]
    [SerializeField] private GameObject highlightRoot;
    [SerializeField] private float focusedScale = 1.08f;
    [SerializeField] private float scaleSpeed = 12f;

    private const int PriceSpriteYOffset = -3;

    private Vector3 baseScale;
    private bool isHovered;
    private bool isSelected;
    private string moduleId;
    private ModuleDefinition boundDefinition;

    public string ModuleId => moduleId;
    public ModuleDefinition BoundDefinition => boundDefinition;
    public bool IsSelected => isSelected;

    public Action<ModuleItemUI> HoverEntered;
    public Action<ModuleItemUI> HoverExited;
    public Action<ModuleItemUI> Clicked;
    public Action<ModuleItemUI> DoubleClicked;

    private void Awake()
    {
        baseScale = transform.localScale;
        RefreshFocusVisual();
    }

    private void Update()
    {
        float target = ShouldLookFocused() ? focusedScale : 1f;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            baseScale * target,
            Time.unscaledDeltaTime * scaleSpeed
        );
    }

    /// <summary>
    /// Bind simple et reutilisable.
    /// </summary>
    public void Bind(ModuleDefinition def)
    {
        boundDefinition = def;
        ApplyDefinition(def);
    }

    /// <summary>
    /// Applique l etat visuel selectionne.
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshFocusVisual();
    }

    private void ApplyDefinition(ModuleDefinition def)
    {
        if (def == null)
            return;

        moduleId = def.id;

        if (tierText != null)
            tierText.text = "T" + Mathf.Max(1, def.tier);

        RefreshIcon(def);
        RefreshPrice(def);
    }

    private void RefreshIcon(ModuleDefinition def)
    {
        if (iconImage == null || def == null)
            return;

        Sprite s = null;

        if (!string.IsNullOrEmpty(def.iconPath))
            s = Resources.Load<Sprite>(def.iconPath);

        if (s == null)
            Debug.LogWarning("[ModuleItemUI] Sprite introuvable: " + def.iconPath);

        iconImage.sprite = s;
        iconImage.enabled = s != null;
        iconImage.preserveAspect = true;
    }

    private void RefreshPrice(ModuleDefinition def)
    {
        if (priceText == null || def == null)
            return;

        priceText.text = $"<voffset={PriceSpriteYOffset}><sprite name=\"icon_money\"></voffset> {Mathf.Max(0, def.cost)}";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshFocusVisual();
        HoverEntered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshFocusVisual();
        HoverExited?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(this);

        if (eventData != null && eventData.clickCount == 2)
            DoubleClicked?.Invoke(this);
    }

    private bool ShouldLookFocused()
    {
        return isHovered || isSelected;
    }

    private void RefreshFocusVisual()
    {
        if (highlightRoot != null)
            highlightRoot.SetActive(ShouldLookFocused());
    }
}
