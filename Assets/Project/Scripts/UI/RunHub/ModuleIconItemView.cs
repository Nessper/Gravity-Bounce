using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Vue d'un module dans la grille d'icônes :
/// - icône
/// - tier (TMP)
/// - prix (TMP avec sprite money inline)
/// - clic (remonte l'id)
/// </summary>
public class ModuleIconItemView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button button;

    [Header("Selection Visual")]
    [SerializeField] private Image selectionHighlight;
    [SerializeField] private float selectedAlpha = 1f;
    [SerializeField] private float unselectedAlpha = 0.35f;

    [Header("TMP Sprites")]
    [Tooltip("Décalage vertical appliqué aux sprites TMP pour alignement optique.")]
    [SerializeField] private int spriteYOffset = -6;


    private string moduleId;
    public string ModuleId => moduleId;
    public UnityEvent<string> OnClicked = new UnityEvent<string>();

    public void Bind(ModuleDefinition def)
    {
        if (def == null)
            return;

        moduleId = def.id;

        if (tierText != null)
            tierText.text = "T" + Mathf.Max(1, def.tier);

        if (costText != null)
        {
            int cost = Mathf.Max(0, def.cost);
            costText.text = FormatMoney(cost);
        }

        if (iconImage != null)
        {
            Sprite s = null;
            if (!string.IsNullOrEmpty(def.iconPath))
                s = Resources.Load<Sprite>(def.iconPath);

            if (s == null)
                Debug.LogWarning("[ModuleIconItemView] Sprite introuvable: " + def.iconPath);

            iconImage.sprite = s;
            iconImage.preserveAspect = true;
        }

        // Le bouton doit être sur le root (recommandé)
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked.Invoke(moduleId));
        }
        else
        {
            Debug.LogWarning("[ModuleIconItemView] Button manquant sur le prefab (clic impossible).");
        }
    }

    private string FormatMoney(int amount)
    {
        // TMP Sprite: <sprite name="icon_money">
        // Align: <voffset=-6> ... </voffset>
        return $"<voffset={spriteYOffset}><sprite name=\"icon_money\"></voffset> {amount}";
    }

    /// <summary>
    /// Definit l'etat visuel de selection.
    /// On ne change pas les raycasts ici, uniquement l'apparence.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.enabled = isSelected;
        }

        // Option sans nouvel asset: dim l'icone quand non selectionne
        if (iconImage != null)
        {
            var c = iconImage.color;
            c.a = isSelected ? selectedAlpha : unselectedAlpha;
            iconImage.color = c;
        }
    }

}
