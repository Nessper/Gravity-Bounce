using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Version minimale pour ModuleItemCompact.
/// - affiche icone + tier
/// - hover => highlight + scale
/// - remonte hover au controller si besoin
/// </summary>
public class ModuleItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text tierText;

    [Header("Focus")]
    [SerializeField] private GameObject highlightRoot;
    [SerializeField] private float focusedScale = 1.08f;
    [SerializeField] private float scaleSpeed = 12f;

    private Vector3 baseScale;
    private bool isHovered;
    private string moduleId;

    public string ModuleId => moduleId;

    public Action<ModuleItemUI> HoverEntered;
    public Action<ModuleItemUI> HoverExited;

    private void Awake()
    {
        baseScale = transform.localScale;

        if (highlightRoot != null)
            highlightRoot.SetActive(false);
    }

    private void Update()
    {
        float target = isHovered ? focusedScale : 1f;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            baseScale * target,
            Time.unscaledDeltaTime * scaleSpeed
        );
    }

    public void Bind(ModuleDefinition def)
    {
        if (def == null)
            return;

        moduleId = def.id;

        if (tierText != null)
            tierText.text = "T" + Mathf.Max(1, def.tier);

        if (iconImage != null)
        {
            Sprite s = null;

            if (!string.IsNullOrEmpty(def.iconPath))
                s = Resources.Load<Sprite>(def.iconPath);

            if (s == null)
                Debug.LogWarning("[ModuleItemUI] Sprite introuvable: " + def.iconPath);

            iconImage.sprite = s;
            iconImage.enabled = s != null;
            iconImage.preserveAspect = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        if (highlightRoot != null)
            highlightRoot.SetActive(true);

        HoverEntered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (highlightRoot != null)
            highlightRoot.SetActive(false);

        HoverExited?.Invoke(this);
    }
}