using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopRepairBayController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;
    [SerializeField] private HullUI hullUI;

    [Header("Missing Hull")]
    [SerializeField] private TMP_Text missingHullText;

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

    [Header("Alignement Sprites")]
    [SerializeField] private int spriteYOffset = -6;

    private const string UiPackName = "ui";

    private bool canRepairOne;
    private bool canRepairAll;
    private int cachedMissing;
    private int cachedCostAll;

    private void Awake()
    {
        if (runSession == null)
        {
            Debug.LogError("[ShopRepairBayController] runSession n est pas assigne.");
            enabled = false;
            return;
        }

        if (hullUI == null)
        {
            Debug.LogError("[ShopRepairBayController] hullUI n est pas assigne.");
            enabled = false;
            return;
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

    private void HandleAnyChanged(int _)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        int money = Mathf.Max(0, runSession.Money);
        int hull = Mathf.Max(0, runSession.Hull);
        int hullMax = Mathf.Max(1, runSession.HullMax);

        cachedMissing = Mathf.Max(0, hullMax - hull);

        int perHull = Mathf.Max(0, costPerHull);
        int costOne = perHull;
        cachedCostAll = cachedMissing * perHull;

        if (missingHullText != null)
        {
            string text = $"Dégâts reçus : {cachedMissing}";

            if (LocalizationManager.Instance != null && LocalizationManager.Instance.IsReady)
            {
                text = LocalizationManager.Instance.FormatText(
                    UiPackName,
                    "repair.missing_hull",
                    cachedMissing
                );
            }

            missingHullText.text = text;
        }

        if (repairOneMetaLeftText != null)
            repairOneMetaLeftText.text = FormatWithSprite(GetPlusHullText(1), hullSpriteName);

        if (repairOneMetaRightText != null)
            repairOneMetaRightText.text = FormatCost(costOne);

        if (repairAllMetaLeftText != null)
            repairAllMetaLeftText.text = FormatWithSprite(GetPlusHullText(cachedMissing), hullSpriteName);

        if (repairAllMetaRightText != null)
            repairAllMetaRightText.text = FormatCost(cachedCostAll);

        bool isFull = cachedMissing <= 0;

        canRepairOne = !isFull && money >= costOne;
        canRepairAll = !isFull && money >= cachedCostAll && cachedCostAll > 0;

        if (repairOneButton != null)
            repairOneButton.interactable = true;

        if (repairAllButton != null)
            repairAllButton.interactable = true;

        SetVisualEnabled(repairOneVisual, canRepairOne);
        SetVisualEnabled(repairAllVisual, canRepairAll);
    }

    private void SetVisualEnabled(CanvasGroup cg, bool enabled)
    {
        if (cg == null)
            return;

        cg.alpha = enabled ? 1f : 0.45f;
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

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

    private string FormatWithSprite(string value, string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return value;

        return $"{value} <voffset={spriteYOffset}><sprite name=\"{spriteName}\"></voffset>";
    }

    private string FormatCost(int cost)
    {
        if (string.IsNullOrEmpty(moneySpriteName))
            return cost.ToString();

        return $"<voffset={spriteYOffset}><sprite name=\"{moneySpriteName}\"></voffset> {cost}";
    }
}