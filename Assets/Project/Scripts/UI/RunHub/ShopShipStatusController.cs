using TMPro;
using UnityEngine;

/// <summary>
/// Affiche dans "SHIP STATUS" :
/// - Money (icône + valeur) via TMP RichText
/// - Hull via HullUI (même comportement couleurs que gameplay, feedback OFF),
///   avec icône inline grâce à HullUI.prefixRichText.
/// </summary>
public class ShopShipStatusController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Money")]
    [SerializeField] private TMP_Text moneyText;

    [Header("Hull (via HullUI)")]
    [SerializeField] private HullUI hullUI;

    [Header("TMP Sprites")]
    [Tooltip("Nom EXACT du sprite dans le TMP SpriteAsset (ex: 'icon_money').")]
    [SerializeField] private string moneySpriteName = "icon_money";

    [Tooltip("Nom EXACT du sprite dans le TMP SpriteAsset (ex: 'icon_hull').")]
    [SerializeField] private string hullSpriteName = "icon_hull";

    [Header("Alignement Sprites")]
    [Tooltip("Décalage vertical appliqué aux sprites TMP pour alignement optique.")]
    [SerializeField] private int spriteYOffset = -6;

    private bool hullPassiveModeApplied = false;

    private void OnEnable()
    {
        if (runSession != null)
        {
            runSession.OnMoneyChanged.AddListener(HandleMoneyChanged);
            runSession.OnHullChanged.AddListener(HandleHullChanged);
            runSession.OnHullMaxChanged.AddListener(HandleHullMaxChanged);
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (runSession != null)
        {
            runSession.OnMoneyChanged.RemoveListener(HandleMoneyChanged);
            runSession.OnHullChanged.RemoveListener(HandleHullChanged);
            runSession.OnHullMaxChanged.RemoveListener(HandleHullMaxChanged);
        }
    }

    private void RefreshAll()
    {
        // Money
        int money = 0;
        if (runSession != null) money = runSession.Money;
        else if (SaveManager.Instance != null) money = SaveManager.Instance.GetMoney();
        RefreshMoney(money);

        // Hull
        if (runSession != null)
            RefreshHull(runSession.Hull, runSession.HullMax);
    }

    // ------------------------------------------------------------
    // Money
    // ------------------------------------------------------------

    private void HandleMoneyChanged(int newValue)
    {
        RefreshMoney(newValue);
    }

    private void RefreshMoney(int value)
    {
        if (moneyText == null)
            return;

        int v = Mathf.Max(0, value);

        if (!string.IsNullOrEmpty(moneySpriteName))
            moneyText.text = FormatIconText(moneySpriteName, v.ToString());
        else
            moneyText.text = v.ToString();
    }

    // ------------------------------------------------------------
    // Hull
    // ------------------------------------------------------------

    private void HandleHullChanged(int newValue)
    {
        if (runSession == null)
            return;

        RefreshHull(newValue, runSession.HullMax);
    }

    private void HandleHullMaxChanged(int newValue)
    {
        if (runSession == null)
            return;

        RefreshHull(runSession.Hull, newValue);
    }

    private void RefreshHull(int current, int max)
    {
        if (hullUI == null)
            return;

        int c = Mathf.Max(0, current);
        int m = Mathf.Max(1, max);

        // Shop = mode passif (pas de punch/flash) (appliqué une fois)
        if (!hullPassiveModeApplied)
        {
            hullUI.SetDamageFeedbackEnabled(false);
            hullPassiveModeApplied = true;
        }

        // Icône inline via SpriteAsset
        if (!string.IsNullOrEmpty(hullSpriteName))
            hullUI.SetPrefixRichText($"<voffset={spriteYOffset}><sprite name=\"{hullSpriteName}\"></voffset> ");
        else
            hullUI.SetPrefixRichText("");

        hullUI.SetMaxHull(m);
        hullUI.SetCurrentHull(c);
    }

    // ------------------------------------------------------------
    // Utils TMP
    // ------------------------------------------------------------

    private string FormatIconText(string spriteName, string content)
    {
        return $"<voffset={spriteYOffset}><sprite name=\"{spriteName}\"></voffset> {content}";
    }
}
