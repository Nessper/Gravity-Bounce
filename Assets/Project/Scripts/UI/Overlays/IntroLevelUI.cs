using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidScrappers.Briefing;

/// <summary>
/// Chemin recommande : Scripts/UI/Overlays/IntroLevelUI.cs
///
/// Ecran d intro de niveau (briefing).
/// - Délègue le bloc briefing (header + phases + objectifs + score) à LevelBriefingPanelUI.
/// - Gère le bloc Ship (image, nom, hull, shield).
/// - Gère les boutons Start/Menu.
///
/// IMPORTANT:
/// - Hull et Shield affiches ici sont des VALEURS RUNTIME injectees (source de verite).
/// - Le ShipCatalog sert uniquement a l affichage statique (nom + image).
/// - Le tier de briefing effectif peut etre derive de la run (modules SCAN).
/// </summary>
public class IntroLevelUI : MonoBehaviour
{
    [Header("Runtime (source de vérité)")]
    [SerializeField] private RunSessionState runSession;

    [Header("Root")]
    [SerializeField] private GameObject overlayIntro;

    [Header("Briefing Panel (factorisé)")]
    [Tooltip("Référence vers l'instance LevelBriefingPanelUI sous LevelBriefingPanel (prefab).")]
    [SerializeField] private LevelBriefingPanelUI briefingPanel;

    [Header("Ship Info (spécifique Intro)")]
    [SerializeField] private Image shipImage;
    [SerializeField] private TMP_Text shipNameText;

    [Tooltip("Fallback texte si HullUI non assigné.")]
    [SerializeField] private TMP_Text shipHullText;

    [SerializeField] private TMP_Text shipShieldText;
    [SerializeField] private HullUI shipHullUI;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button menuButton;

    [Header("Briefing Tier (Debug)")]
    [Tooltip("Fallback uniquement. En runtime, le tier effectif vient des modules SCAN si RunSessionState est present.")]
    [SerializeField] private BriefingTier briefingTierFallback = BriefingTier.T3;

    private System.Action onStartCallback;
    private System.Action onMenuCallback;

    // Valeurs runtime (source de verite) injectees par l orchestrateur.
    private int runtimeHull = -1;
    private int runtimeMaxHull = -1;
    private float runtimeShieldSeconds = -1f;

    private void Awake()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);
    }

    private void OnEnable()
    {
        // On écoute l équipement: si un module SCAN/HULL change,
        // le briefing (tier) et/ou les stats (hull max) doivent se rafraîchir.
        if (runSession != null)
            runSession.OnEquipmentChanged.AddListener(HandleEquipmentChanged);
    }

    private void OnDisable()
    {
        if (runSession != null)
            runSession.OnEquipmentChanged.RemoveListener(HandleEquipmentChanged);
    }

    /// <summary>
    /// Appelé quand un module est équipé/déséquipé.
    /// - Si l intro est ouverte, on refresh immédiatement.
    /// - Sinon, rien: l état sera correct au prochain Show().
    /// </summary>
    private void HandleEquipmentChanged()
    {
        if (overlayIntro == null || !overlayIntro.activeInHierarchy)
            return;

        // 1) Refresh Ship stats
        RefreshShipStatsTexts();

        // 2) Refresh Briefing tier (SCAN)
        if (briefingPanel != null)
            briefingPanel.RefreshWithTier(GetEffectiveBriefingTier());
    }

    // ------------------------------------------------------------
    // RUNTIME INJECTION (source de verite)
    // ------------------------------------------------------------

    public void SetShipRuntimeHull(int currentHull, int maxHull)
    {
        runtimeHull = Mathf.Max(-1, currentHull);
        runtimeMaxHull = Mathf.Max(-1, maxHull);

        if (overlayIntro != null && overlayIntro.activeInHierarchy)
            RefreshShipStatsTexts();
    }

    public void SetShipRuntimeShield(float shieldSeconds)
    {
        runtimeShieldSeconds = Mathf.Max(-1f, shieldSeconds);

        if (overlayIntro != null && overlayIntro.activeInHierarchy)
            RefreshShipStatsTexts();
    }

    // ------------------------------------------------------------
    // SHOW / HIDE
    // ------------------------------------------------------------

    public void Show(
        LevelData data,
        PhasePlanInfo[] phasePlans,
        string worldName,
        string title,
        System.Action onStart,
        System.Action onMenu)
    {
        if (data == null)
            return;

        onStartCallback = onStart;
        onMenuCallback = onMenu;

        // 1) Briefing (factorisé)
        if (briefingPanel != null)
            briefingPanel.Render(data, phasePlans, worldName, title, GetEffectiveBriefingTier());
        else
            Debug.LogWarning("[IntroLevelUI] briefingPanel est null. Le briefing ne sera pas affiché.");

        // 2) Ship static (nom + image)
        FillShipStaticInfo();

        // 3) Ship runtime (hull/shield)
        RefreshShipStatsTexts();

        if (overlayIntro != null)
            overlayIntro.SetActive(true);
    }

    public void Hide()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);
    }

    // A cabler dans l Inspector (Button OnClick)
    public void OnStartClicked()
    {
        Debug.Log("[IntroLevelUI] OnStartClicked");
        onStartCallback?.Invoke();
    }

    // A cabler dans l Inspector (Button OnClick)
    public void OnMenuClicked()
    {
        Debug.Log("[IntroLevelUI] OnMenuClicked");
        onMenuCallback?.Invoke();
    }

    // ------------------------------------------------------------
    // Tier effectif (SCAN)
    // ------------------------------------------------------------

    private BriefingTier GetEffectiveBriefingTier()
    {
        // Par défaut: fallback inspector (debug)
        BriefingTier tier = briefingTierFallback;

        // Si runSession présent: on dérive depuis les modules SCAN équipés
        if (runSession != null)
            tier = runSession.GetEffectiveBriefingTier();

        return tier;
    }

    // ------------------------------------------------------------
    // SHIP DISPLAY (Intro uniquement)
    // ------------------------------------------------------------

    private void FillShipStaticInfo()
    {
        if (RunConfig.Instance == null || ShipCatalogService.Catalog == null)
            return;

        var catalog = ShipCatalogService.Catalog;
        string selectedId = RunConfig.Instance.SelectedShipId;
        var ship = catalog.ships.Find(s => s.id == selectedId);

        if (ship == null)
        {
            Debug.LogWarning("[IntroLevelUI] Ship not found: " + selectedId);
            return;
        }

        if (shipNameText != null)
            shipNameText.text = ship.displayName;

        if (shipImage != null)
        {
            string key = StripExtension(ship.imageFile);
            Sprite sprite = Resources.Load<Sprite>("Ships/Images/" + key);

            if (sprite == null)
                Debug.LogWarning("[IntroLevelUI] Sprite introuvable dans Resources: Ships/Images/" + key);
            else
                shipImage.sprite = sprite;

            shipImage.preserveAspect = true;
        }
    }

    private void RefreshShipStatsTexts()
    {
        // Hull (via HullUI si dispo)
        if (shipHullUI != null)
        {
            if (runtimeHull >= 0 && runtimeMaxHull > 0)
            {
                shipHullUI.SetDamageFeedbackEnabled(false); // briefing = pas de feedback degats
                shipHullUI.SetMaxHull(runtimeMaxHull);
                shipHullUI.SetCurrentHull(runtimeHull);
            }
            else
            {
                // Valeurs inconnues -> affichage neutre
                shipHullUI.SetDamageFeedbackEnabled(false);
                shipHullUI.SetMaxHull(1);
                shipHullUI.SetCurrentHull(0);
            }
        }
        else
        {
            // Fallback texte si HullUI pas assigné
            if (shipHullText != null)
            {
                if (runtimeHull >= 0 && runtimeMaxHull > 0)
                    shipHullText.text = runtimeHull + " / " + runtimeMaxHull;
                else
                    shipHullText.text = "--";
            }
        }

        // Shield
        if (shipShieldText != null)
        {
            if (runtimeShieldSeconds >= 0f)
            {
                int rounded = Mathf.RoundToInt(runtimeShieldSeconds);
                shipShieldText.text = rounded.ToString() + "s";
            }
            else
            {
                shipShieldText.text = "--";
            }
        }
    }

    private string StripExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        int dot = fileName.LastIndexOf('.');
        if (dot <= 0)
            return fileName;

        return fileName.Substring(0, dot);
    }
}
