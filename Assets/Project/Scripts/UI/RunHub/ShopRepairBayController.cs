using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopRepairBayController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Missing Hull")]
    [SerializeField] private TMP_Text missingHullText;

    [Header("Repair One")]
    [SerializeField] private Button repairOneButton;
    [SerializeField] private TMP_Text repairOneMetaLeftText;
    [SerializeField] private TMP_Text repairOneMetaRightText;
    [Tooltip("Optionnel: CanvasGroup pour griser visuellement Repair One")]
    [SerializeField] private CanvasGroup repairOneVisual;

    [Header("Repair All")]
    [SerializeField] private Button repairAllButton;
    [SerializeField] private TMP_Text repairAllMetaLeftText;
    [SerializeField] private TMP_Text repairAllMetaRightText;
    [Tooltip("Optionnel: CanvasGroup pour griser visuellement Repair All")]
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

    // Etat calculé au refresh (source de vérité pour les clicks)
    private bool canRepairOne;
    private bool canRepairAll;
    private int cachedMissing;
    private int cachedCostAll;

    private void OnEnable()
    {
        if (runSession != null)
        {
            runSession.OnMoneyChanged.AddListener(HandleAnyChanged);
            runSession.OnHullChanged.AddListener(HandleAnyChanged);
            runSession.OnHullMaxChanged.AddListener(HandleAnyChanged);
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (runSession != null)
        {
            runSession.OnMoneyChanged.RemoveListener(HandleAnyChanged);
            runSession.OnHullChanged.RemoveListener(HandleAnyChanged);
            runSession.OnHullMaxChanged.RemoveListener(HandleAnyChanged);
        }
    }

    private void HandleAnyChanged(int _)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (runSession == null)
            return;

        int money = Mathf.Max(0, runSession.Money);
        int hull = Mathf.Max(0, runSession.Hull);
        int hullMax = Mathf.Max(1, runSession.HullMax);

        cachedMissing = Mathf.Max(0, hullMax - hull);

        int perHull = Mathf.Max(0, costPerHull);
        int costOne = perHull;
        cachedCostAll = cachedMissing * perHull;

        // Missing hull text
        if (missingHullText != null)
            missingHullText.text = "MISSING HULL : " + cachedMissing;

        // Meta texts
        if (repairOneMetaLeftText != null)
            repairOneMetaLeftText.text = FormatWithSprite("+1", hullSpriteName);

        if (repairOneMetaRightText != null)
            repairOneMetaRightText.text = FormatCost(costOne);

        if (repairAllMetaLeftText != null)
            repairAllMetaLeftText.text = FormatWithSprite("+" + cachedMissing, hullSpriteName);

        if (repairAllMetaRightText != null)
            repairAllMetaRightText.text = FormatCost(cachedCostAll);

        bool isFull = cachedMissing <= 0;

        canRepairOne = !isFull && money >= costOne;
        canRepairAll = !isFull && money >= cachedCostAll && cachedCostAll > 0;

        // IMPORTANT: on laisse les boutons cliquables
        // (sinon pas de log, pas de son, pas de feedback)
        if (repairOneButton != null)
            repairOneButton.interactable = true;

        if (repairAllButton != null)
            repairAllButton.interactable = true;

        // Visuel "gris" (optionnel)
        SetVisualEnabled(repairOneVisual, canRepairOne);
        SetVisualEnabled(repairAllVisual, canRepairAll);
    }

    private void SetVisualEnabled(CanvasGroup cg, bool enabled)
    {
        if (cg == null) return;

        cg.alpha = enabled ? 1f : 0.45f;
        // On ne coupe PAS les raycasts, sinon plus cliquable.
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

    // ------------------------------------------------------------
    // OnClick
    // ------------------------------------------------------------

    public void OnRepairOnePressed()
    {
        if (runSession == null)
            return;

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
        RefreshAll();
    }

    public void OnRepairAllPressed()
    {
        if (runSession == null)
            return;

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
        RefreshAll();
    }

    // ------------------------------------------------------------
    // TMP helpers
    // ------------------------------------------------------------

    private string FormatWithSprite(string value, string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return value;

        return $"{value} <voffset={spriteYOffset}><sprite name=\"{spriteName}\"></voffset>";
    }

    private string FormatCost(int cost)
    {
        if (string.IsNullOrEmpty(moneySpriteName))
            return "COST : " + cost;

        return $"COST : {cost} <voffset={spriteYOffset}><sprite name=\"{moneySpriteName}\"></voffset>";
    }
}