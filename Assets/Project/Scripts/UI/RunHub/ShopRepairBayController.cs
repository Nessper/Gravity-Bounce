using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la zone de réparation du shop.
///
/// Responsabilités :
/// - affiche l'état de la coque
/// - calcule les coûts de réparation
/// - met à jour le visuel des options Repair One / Repair All
/// - exécute l'achat de réparation via RunSessionState
///
/// Règles UI :
/// - si le vaisseau est endommagé :
///   affiche "Dégâts reçus : X"
/// - si le vaisseau est intact :
///   affiche un message positif
///   applique un material TMP dédié (ex : vert)
/// - les boutons restent cliquables pour jouer un son d'erreur,
///   mais leur visuel indique clairement qu'ils sont indisponibles
/// </summary>
public class ShopRepairBayController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;
    [SerializeField] private HullUI hullUI;

    [Header("State Text")]
    [SerializeField] private TMP_Text missingHullText;

    [Tooltip("Material TMP normal pour l'état endommagé.")]
    [SerializeField] private Material missingHullNormalMaterial;

    [Tooltip("Material TMP alternatif pour l'état 'coque intacte' (ex : vert).")]
    [SerializeField] private Material missingHullFullMaterial;

    [Header("Repair One")]
    [SerializeField] private Button repairOneButton;
    [SerializeField] private TMP_Text repairOneMetaLeftText;
    [SerializeField] private TMP_Text repairOneMetaRightText;
    [SerializeField] private CanvasGroup repairOneVisual;

    [Header("Repair All")]
    [SerializeField] private Button repairAllButton;
    [SerializeField] private TMP_Text repairAllMetaLeftText;
    [SerializeField] private TMP_Text repairAllMetaRightText;
    [SerializeField] private CanvasGroup repairAllVisual;

    [Header("Costs")]
    [SerializeField] private int costPerHull = 2;

    [Header("SFX")]
    [SerializeField] private SfxId buySfx = SfxId.ShopBuy;
    [SerializeField] private SfxId errorSfx = SfxId.ShopError;

    [Header("TMP Sprites")]
    [SerializeField] private string hullSpriteName = "icon_hull";
    [SerializeField] private string moneySpriteName = "icon_money";

    [Header("Sprite Alignment")]
    [SerializeField] private int spriteYOffset = -6;

    [Header("Visual States")]
    [SerializeField] private float disabledOptionAlpha = 0.45f;

    private const string UiPackName = "ui";

    private bool canRepairOne;
    private bool canRepairAll;
    private int cachedMissing;
    private int cachedCostAll;

    private void Awake()
    {
        if (runSession == null)
        {
            Debug.LogError("[ShopRepairBayController] runSession n'est pas assigné.");
            enabled = false;
            return;
        }

        if (hullUI == null)
        {
            Debug.LogError("[ShopRepairBayController] hullUI n'est pas assigné.");
            enabled = false;
            return;
        }

        if (missingHullText != null && missingHullNormalMaterial == null)
        {
            missingHullNormalMaterial = missingHullText.fontSharedMaterial;
        }
    }

    private void OnEnable()
    {
        runSession.OnMoneyChanged.AddListener(HandleAnyChanged);
        runSession.OnHullChanged.AddListener(HandleAnyChanged);
        runSession.OnHullMaxChanged.AddListener(HandleAnyChanged);

        RefreshAll();
    }

    private void OnDisable()
    {
        runSession.OnMoneyChanged.RemoveListener(HandleAnyChanged);
        runSession.OnHullChanged.RemoveListener(HandleAnyChanged);
        runSession.OnHullMaxChanged.RemoveListener(HandleAnyChanged);
    }

    /// <summary>
    /// Callback unique pour tous les changements utiles.
    /// </summary>
    private void HandleAnyChanged(int _)
    {
        RefreshAll();
    }

    /// <summary>
    /// Recalcule tout l'état logique et visuel du panneau.
    /// </summary>
    private void RefreshAll()
    {
        int money = Mathf.Max(0, runSession.Money);
        int hull = Mathf.Max(0, runSession.Hull);
        int hullMax = Mathf.Max(1, runSession.HullMax);

        cachedMissing = Mathf.Max(0, hullMax - hull);

        int perHull = Mathf.Max(0, costPerHull);
        int costOne = perHull;
        cachedCostAll = cachedMissing * perHull;

        RefreshStateText();
        RefreshRepairTexts(costOne);
        RefreshButtonsState(money, costOne);
    }

    /// <summary>
    /// Met à jour le texte d'état de la coque.
    /// - état endommagé : texte standard
    /// - état full : message positif + material vert si assigné
    /// </summary>
    private void RefreshStateText()
    {
        if (missingHullText == null)
            return;

        bool isFull = cachedMissing <= 0;
        string text;

        if (!isFull)
        {
            text = $"Dégâts reçus : {cachedMissing}";

            if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
            {
                text = LocalizationManager.Instance.FormatText(
                    UiPackName,
                    "repair.missing_hull",
                    cachedMissing
                );
            }

            ApplyStateTextMaterial(missingHullNormalMaterial);
        }
        else
        {
            text = "Coque intacte. Votre vaisseau est prêt pour la prochaine mission.";

            if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
            {
                // Remplace cette clé par la tienne si besoin
                text = LocalizationManager.Instance.GetText(
                    UiPackName,
                    "repair.hull_full"
                );
            }

            ApplyStateTextMaterial(
                missingHullFullMaterial != null
                    ? missingHullFullMaterial
                    : missingHullNormalMaterial
            );
        }

        missingHullText.text = text;
    }

    /// <summary>
    /// Applique le material TMP voulu sur le texte d'état.
    /// </summary>
    private void ApplyStateTextMaterial(Material targetMaterial)
    {
        if (missingHullText == null || targetMaterial == null)
            return;

        missingHullText.fontSharedMaterial = targetMaterial;
    }

    /// <summary>
    /// Met à jour les textes des options de réparation.
    /// </summary>
    private void RefreshRepairTexts(int costOne)
    {
        if (repairOneMetaLeftText != null)
            repairOneMetaLeftText.text = FormatWithSprite(GetPlusHullText(1), hullSpriteName);

        if (repairOneMetaRightText != null)
            repairOneMetaRightText.text = FormatCost(costOne);

        if (repairAllMetaLeftText != null)
            repairAllMetaLeftText.text = FormatWithSprite(GetPlusHullText(cachedMissing), hullSpriteName);

        if (repairAllMetaRightText != null)
            repairAllMetaRightText.text = FormatCost(cachedCostAll);
    }

    /// <summary>
    /// Calcule les états disponibles et met à jour le rendu des options.
    /// </summary>
    private void RefreshButtonsState(int money, int costOne)
    {
        bool isFull = cachedMissing <= 0;

        canRepairOne = !isFull && money >= costOne;
        canRepairAll = !isFull && money >= cachedCostAll && cachedCostAll > 0;

        // On laisse les boutons interactables pour pouvoir jouer le son d'erreur.
        if (repairOneButton != null)
            repairOneButton.interactable = true;

        if (repairAllButton != null)
            repairAllButton.interactable = true;

        SetOptionVisualEnabled(repairOneVisual, canRepairOne);
        SetOptionVisualEnabled(repairAllVisual, canRepairAll);
    }

    /// <summary>
    /// Met à jour le visuel d'une option active / inactive.
    /// </summary>
    private void SetOptionVisualEnabled(CanvasGroup cg, bool isEnabled)
    {
        if (cg == null)
            return;

        cg.alpha = isEnabled ? 1f : disabledOptionAlpha;
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

    /// <summary>
    /// Action du bouton Repair One.
    /// </summary>
    public void OnRepairOnePressed()
    {
        if (!canRepairOne)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            return;
        }

        if (!runSession.TrySpendMoney(Mathf.Max(0, costPerHull)))
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            return;
        }

        BootRoot.Audio?.PlayUi(buySfx);
        runSession.RepairHull(1);
        hullUI.PlayRepairFeedback();

        RefreshAll();
    }

    /// <summary>
    /// Action du bouton Repair All.
    /// </summary>
    public void OnRepairAllPressed()
    {
        if (!canRepairAll)
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            return;
        }

        if (!runSession.TrySpendMoney(cachedCostAll))
        {
            BootRoot.Audio?.PlayUi(errorSfx);
            return;
        }

        BootRoot.Audio?.PlayUi(buySfx);
        runSession.RepairHull(cachedMissing);
        hullUI.PlayRepairFeedback();

        RefreshAll();
    }

    /// <summary>
    /// Formate le texte de gain de coque.
    /// </summary>
    private string GetPlusHullText(int value)
    {
        string text = "+" + value;

        if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
        {
            text = LocalizationManager.Instance.FormatText(
                UiPackName,
                "repair.plus_hull",
                value
            );
        }

        return text;
    }

    /// <summary>
    /// Ajoute un sprite TMP inline à droite d'une valeur.
    /// </summary>
    private string FormatWithSprite(string value, string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return value;

        return $"{value} <voffset={spriteYOffset}><sprite name=\"{spriteName}\"></voffset>";
    }

    /// <summary>
    /// Formate un coût avec sprite de monnaie.
    /// </summary>
    private string FormatCost(int cost)
    {
        if (string.IsNullOrEmpty(moneySpriteName))
            return cost.ToString();

        return $"<voffset={spriteYOffset}><sprite name=\"{moneySpriteName}\"></voffset> {cost}";
    }
}