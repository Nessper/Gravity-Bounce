// Chemin recommandé : Scripts/UI/Overlays/IntroLevelUI.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidScrappers.Briefing;

/// <summary>
/// Ecran d intro de niveau (briefing).
///
/// Responsabilites :
/// - Déléguer le bloc briefing (header + phases + objectifs + score) à LevelBriefingPanelUI.
/// - Gérer le bloc Ship (image, nom, hull, shield).
/// - Gérer les boutons Start / Menu.
/// - Réagir aux changements runtime utiles (équipement modules, stats ship).
///
/// Important :
/// - Hull et Shield affichés ici sont des VALEURS RUNTIME injectées (source de vérité).
/// - Le ShipCatalog sert uniquement à l affichage statique (nom + image).
/// - Le tier de briefing effectif vient désormais de ModuleRuntimeStats.
/// - Le ship affiché vient de la run courante (RunSessionState), pas de RunConfig.
/// </summary>
public class IntroLevelUI : MonoBehaviour
{
    [Header("Runtime (source de vérité)")]
    [SerializeField] private RunSessionState runSession;

    private ModuleRuntimeStats moduleRuntimeStats => ModuleRuntimeStats.Instance;

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
    [Tooltip("Fallback uniquement si ModuleRuntimeStats n'est pas assigné.")]
    [SerializeField] private BriefingTier briefingTierFallback = BriefingTier.T3;

    private System.Action onStartCallback;
    private System.Action onMenuCallback;

    // Valeurs runtime injectées par l orchestrateur.
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
        // Si l'équipement change, le briefing (SCAN) et potentiellement le Hull max
        // doivent pouvoir se rafraîchir immédiatement si l'overlay est visible.
        if (runSession != null)
            runSession.OnEquipmentChanged.AddListener(HandleEquipmentChanged);

        // Si les stats agrégées modules se rebuildent, on refresh aussi le briefing.
        if (moduleRuntimeStats != null)
            moduleRuntimeStats.OnStatsRebuilt.AddListener(HandleModuleStatsRebuilt);

        // Si le ship courant change pendant que l'overlay est ouvert.
        if (runSession != null)
            runSession.OnShipChanged.AddListener(HandleShipChanged);
    }

    private void OnDisable()
    {
        if (runSession != null)
            runSession.OnEquipmentChanged.RemoveListener(HandleEquipmentChanged);

        if (moduleRuntimeStats != null)
            moduleRuntimeStats.OnStatsRebuilt.RemoveListener(HandleModuleStatsRebuilt);

        if (runSession != null)
            runSession.OnShipChanged.RemoveListener(HandleShipChanged);
    }

    /// <summary>
    /// Injecte le Hull runtime à afficher.
    /// </summary>
    public void SetShipRuntimeHull(int currentHull, int maxHull)
    {
        runtimeHull = Mathf.Max(-1, currentHull);
        runtimeMaxHull = Mathf.Max(-1, maxHull);

        if (overlayIntro != null && overlayIntro.activeInHierarchy)
            RefreshShipStatsTexts();
    }

    /// <summary>
    /// Injecte la valeur runtime du shield à afficher.
    /// </summary>
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

        // 1) Briefing factorisé
        if (briefingPanel != null)
        {
            briefingPanel.Render(
                data,
                phasePlans,
                worldName,
                title,
                GetEffectiveBriefingTier());
        }
        else
        {
            Debug.LogWarning("[IntroLevelUI] briefingPanel est null. Le briefing ne sera pas affiché.");
        }

        // 2) Ship statique (nom + image)
        FillShipStaticInfo();

        // 3) Ship runtime (hull / shield)
        RefreshShipStatsTexts();

        if (overlayIntro != null)
            overlayIntro.SetActive(true);
    }

    public void Hide()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);
    }

    // À câbler dans l Inspector (Button OnClick)
    public void OnStartClicked()
    {
        Debug.Log("[IntroLevelUI] OnStartClicked");
        onStartCallback?.Invoke();
    }

    // À câbler dans l Inspector (Button OnClick)
    public void OnMenuClicked()
    {
        Debug.Log("[IntroLevelUI] OnMenuClicked");
        onMenuCallback?.Invoke();
    }

    // ------------------------------------------------------------
    // REFRESH RUNTIME
    // ------------------------------------------------------------

    /// <summary>
    /// Appelé quand l'équipement change.
    /// - Si l'intro est visible, on refresh immédiatement.
    /// - Sinon, rien : l'état sera correct au prochain Show().
    /// </summary>
    private void HandleEquipmentChanged()
    {
        if (overlayIntro == null || !overlayIntro.activeInHierarchy)
            return;

        RefreshShipStatsTexts();
        RefreshBriefingTier();
    }

    /// <summary>
    /// Appelé quand les stats modules runtime sont recalculées.
    /// </summary>
    private void HandleModuleStatsRebuilt()
    {
        if (overlayIntro == null || !overlayIntro.activeInHierarchy)
            return;

        RefreshBriefingTier();
    }

    /// <summary>
    /// Appelé quand le ship courant de la run change.
    /// </summary>
    private void HandleShipChanged(string newShipId)
    {
        if (overlayIntro == null || !overlayIntro.activeInHierarchy)
            return;

        FillShipStaticInfo();
        RefreshShipStatsTexts();
    }

    private void RefreshBriefingTier()
    {
        if (briefingPanel == null)
            return;

        briefingPanel.RefreshWithTier(GetEffectiveBriefingTier());
    }

    // ------------------------------------------------------------
    // TIER EFFECTIF (SCAN)
    // ------------------------------------------------------------

    /// <summary>
    /// Retourne le tier de briefing effectif.
    /// Source principale : ModuleRuntimeStats.
    /// Fallback : valeur inspector.
    /// </summary>
    private BriefingTier GetEffectiveBriefingTier()
    {
        if (ModuleRuntimeStats.Instance != null)
            return ModuleRuntimeStats.Instance.GetEffectiveBriefingTier();

        return briefingTierFallback;
    }

    // ------------------------------------------------------------
    // SHIP DISPLAY (Intro uniquement)
    // ------------------------------------------------------------

    /// <summary>
    /// Remplit le nom et l'image du ship courant à partir du ShipCatalog.
    /// Source du ship courant : RunSessionState.
    /// </summary>
    private void FillShipStaticInfo()
    {
        if (runSession == null || ShipCatalogService.Catalog == null)
            return;

        var catalog = ShipCatalogService.Catalog;
        string currentShipId = runSession.ShipId;

        var ship = catalog.ships.Find(s => s.id == currentShipId);
        if (ship == null)
        {
            Debug.LogWarning("[IntroLevelUI] Ship not found: " + currentShipId);
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

    /// <summary>
    /// Rafraîchit les textes / UI du Hull et du Shield.
    /// </summary>
    private void RefreshShipStatsTexts()
    {
        // Hull via HullUI si dispo
        if (shipHullUI != null)
        {
            if (runtimeHull >= 0 && runtimeMaxHull > 0)
            {
                shipHullUI.SetDamageFeedbackEnabled(false); // briefing = pas de feedback dégâts
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
            // Fallback texte si HullUI n'est pas assigné
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