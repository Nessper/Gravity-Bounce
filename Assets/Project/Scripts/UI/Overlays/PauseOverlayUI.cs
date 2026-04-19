using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidScrappers.Briefing;

/// <summary>
/// Chemin recommandé : Scripts/UI/Overlays/PauseOverlayUI.cs
///
/// UI du menu Pause.
/// - Délègue le briefing commun à LevelBriefingPanelUI (prefab commun Intro/Pause).
/// - Gère uniquement l'affichage Run Status (run score, crédits, LEDs contrats).
///
/// IMPORTANT :
/// - Ne touche pas Time.timeScale (c'est PauseController).
/// - Ne gère pas de callbacks / events : tous les boutons passent par l'Inspector
///   vers LevelPauseFlowHandler (Resume/Menu/Retry).
/// </summary>
public class PauseOverlayUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject overlayRoot;

    [Header("Briefing (prefab commun)")]
    //[SerializeField] private LevelBriefingPanelUI briefingPanel;

    // --------------------------------------------------------------------
    // RUN STATUS
    // --------------------------------------------------------------------

    [Header("Run Status")]
    [SerializeField] private TMP_Text runScoreText;

    [SerializeField] private TMP_Text creditsText;

    [Tooltip("Nom EXACT du sprite money dans le TMP SpriteAsset (ex: 'icon_money').")]
    [SerializeField] private string moneySpriteName = "icon_money";

    [Tooltip("Offset vertical appliqué au sprite TMP (alignement visuel).")]
    [SerializeField] private int spriteYOffset = -6;

    [Tooltip("Images des LEDs de contrat (ordre gauche -> droite). Attendu: 3.")]
    [SerializeField] private Image[] contractLedImages;

    [SerializeField] private Sprite contractLedGreen;
    [SerializeField] private Sprite contractLedRed;

    private void Awake()
    {
        // L'overlay est caché par défaut.
        Hide();
    }

    // --------------------------------------------------------------------
    // VISIBILITY
    // --------------------------------------------------------------------

    public void Show()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(true);
    }

    public void Hide()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    // --------------------------------------------------------------------
    // RENDER
    // --------------------------------------------------------------------

    /// <summary>
    /// Remplit le briefing + run status.
    /// Appelé par LevelPauseFlowHandler lors de OnPauseOpening (avant freeze time).
    /// </summary>
    public void RenderAll(
        LevelData data,
        PhasePlanInfo[] phasePlans,
        string worldName,
        string title,
        BriefingTier tier,
        int runScore,
        int contractsLeft,
        int credits)
    {
        if (data == null)
            return;

        //if (briefingPanel != null)
        //    briefingPanel.Render(data, phasePlans, worldName, title, tier);

        FillRunStatus(runScore, contractsLeft, credits);
    }

    // --------------------------------------------------------------------
    // RUN STATUS
    // --------------------------------------------------------------------

    private void FillRunStatus(int runScore, int contractsLeft, int credits)
    {
        if (runScoreText != null)
            runScoreText.text = Mathf.Max(0, runScore).ToString();

        if (creditsText != null)
            creditsText.text = FormatMoney(credits);

        ApplyContractLeds(contractsLeft);
    }

    private string FormatMoney(int value)
    {
        int v = Mathf.Max(0, value);

        if (string.IsNullOrEmpty(moneySpriteName))
            return v.ToString();

        // NB: espace normal volontaire (pas besoin de NBSP ici, c'est un header court).
        return "<voffset=" + spriteYOffset + "><sprite name=\"" + moneySpriteName + "\"></voffset> " + v.ToString();
    }

    private void ApplyContractLeds(int contractsLeft)
    {
        if (contractLedImages == null || contractLedImages.Length < 3)
            return;

        if (contractLedGreen == null || contractLedRed == null)
            return;

        int c = Mathf.Clamp(contractsLeft, 0, 3);

        // Convention :
        // 3 -> G G G
        // 2 -> R G G
        // 1 -> R R G
        // 0 -> R R R
        contractLedImages[0].sprite = (c >= 3) ? contractLedGreen : contractLedRed;
        contractLedImages[1].sprite = (c >= 2) ? contractLedGreen : contractLedRed;
        contractLedImages[2].sprite = (c >= 1) ? contractLedGreen : contractLedRed;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        // Auto-wire simple si tu poses le script sur un root d'overlay.
        if (overlayRoot == null)
            overlayRoot = gameObject;
    }
#endif
}
