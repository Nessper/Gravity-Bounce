using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vue reutilisable pour le bloc Ship Status.
///
/// Cette classe ne contient aucune logique metier.
/// Elle expose uniquement les references UI du panneau
/// et quelques helpers simples pour les controllers de contexte.
///
/// Objectif :
/// - mutualiser le layout / prefab
/// - garder la logique actuelle dans ShipSelectController, RunHub, LevelBriefing
/// - eviter un gros refactor brutal
/// </summary>
public class ShipStatusPanelUI : MonoBehaviour
{
    private const string TuningButtonName = "Button_Tuning";

    [Header("Optional Roots")]
    [SerializeField] private GameObject descriptionRoot;
    [SerializeField] private GameObject tuningButtonRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text openSlotsText;
    [SerializeField] private TMP_Text noModulesText;

    [Header("Modules")]
    [SerializeField] private Transform equippedModulesRoot;
    [SerializeField] private ModuleDetailsPanelUI moduleDetailsPanel;

    [Header("Tuning Attention")]
    [SerializeField] private Color tuningAttentionTextColor = new Color(0.65f, 1f, 0.08f, 1f);
    [SerializeField] private float tuningAttentionPulseSpeed = 2.4f;

    private Image tuningButtonFrame;
    private Color tuningButtonFrameBaseColor;
    private TMP_Text tuningButtonText;
    private Color tuningButtonTextBaseColor;
    private FontStyles tuningButtonTextBaseStyle;
    private bool tuningButtonTextUsedGradient;
    private bool hasCachedTuningButtonVisual;
    private bool isTuningAttentionActive;

    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text HullText => hullText;
    public TMP_Text DurationText => durationText;
    public TMP_Text MoneyText => moneyText;
    public TMP_Text OpenSlotsText => openSlotsText;
    public TMP_Text NoModulesText => noModulesText;

    public Transform EquippedModulesRoot => equippedModulesRoot;
    public ModuleDetailsPanelUI ModuleDetailsPanel => moduleDetailsPanel;

    public void SetDescriptionVisible(bool visible)
    {
        if (descriptionRoot != null)
            descriptionRoot.SetActive(visible);
    }

    public void SetTuningVisible(bool visible)
    {
        if (tuningButtonRoot != null)
            tuningButtonRoot.SetActive(visible);
    }

    private void OnEnable()
    {
        RunHubModulesBuyController.ModulePurchased += StartTuningAttention;
        ShipSystemsOverlayTransitionController.SourceUiHidden += StopTuningAttention;
    }

    private void OnDisable()
    {
        RunHubModulesBuyController.ModulePurchased -= StartTuningAttention;
        ShipSystemsOverlayTransitionController.SourceUiHidden -= StopTuningAttention;
        StopTuningAttention();
    }

    private void Update()
    {
        if (hasCachedTuningButtonVisual && !isTuningAttentionActive)
        {
            if (tuningButtonFrame != null)
                tuningButtonFrame.color = tuningButtonFrameBaseColor;

            return;
        }

        if (!isTuningAttentionActive)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * tuningAttentionPulseSpeed * Mathf.PI * 2f);

        if (tuningButtonFrame != null)
            tuningButtonFrame.color = Color.Lerp(tuningButtonFrameBaseColor, tuningAttentionTextColor, pulse);

        if (tuningButtonText != null)
            tuningButtonText.color = Color.Lerp(tuningButtonTextBaseColor, tuningAttentionTextColor, pulse);
    }

    /// <summary>
    /// Signale au joueur qu un module achete peut etre equipe dans Ship Systems.
    /// </summary>
    public void StartTuningAttention()
    {
        EnsureTuningAttentionVisual();
        if (tuningButtonFrame == null)
            return;

        isTuningAttentionActive = true;

        if (tuningButtonText != null)
        {
            tuningButtonText.enableVertexGradient = false;
            tuningButtonText.fontStyle = tuningButtonTextBaseStyle | FontStyles.Bold;
        }
    }

    /// <summary>
    /// Restaure le bouton apres l ouverture de Ship Systems.
    /// </summary>
    public void StopTuningAttention()
    {
        isTuningAttentionActive = false;

        if (tuningButtonFrame != null)
            tuningButtonFrame.color = tuningButtonFrameBaseColor;

        if (tuningButtonText != null)
        {
            tuningButtonText.color = tuningButtonTextBaseColor;
            tuningButtonText.fontStyle = tuningButtonTextBaseStyle;
            tuningButtonText.enableVertexGradient = tuningButtonTextUsedGradient;
        }
    }

    private void EnsureTuningAttentionVisual()
    {
        if (tuningButtonFrame != null)
            return;

        Transform tuningButton = FindTuningButton();
        if (tuningButton == null)
            return;

        Transform frame = tuningButton.Find("Frame");
        if (frame == null)
        {
            Debug.LogWarning("[ShipStatusPanelUI] Frame du bouton Tuning introuvable.");
            return;
        }

        tuningButtonFrame = frame.GetComponent<Image>();
        if (tuningButtonFrame == null)
        {
            Debug.LogWarning("[ShipStatusPanelUI] Image du frame Tuning introuvable.");
            return;
        }

        tuningButtonFrameBaseColor = tuningButtonFrame.color;

        tuningButtonText = tuningButton.GetComponentInChildren<TMP_Text>(true);
        if (tuningButtonText != null)
        {
            tuningButtonTextBaseColor = tuningButtonText.color;
            tuningButtonTextBaseStyle = tuningButtonText.fontStyle;
            tuningButtonTextUsedGradient = tuningButtonText.enableVertexGradient;
        }

        hasCachedTuningButtonVisual = true;

    }

    private Transform FindTuningButton()
    {
        if (tuningButtonRoot == null)
            return null;

        Transform[] transforms = tuningButtonRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == TuningButtonName)
                return transforms[i];
        }

        return null;
    }

    public void SetDescriptionText(string value)
    {
        if (descriptionText != null)
            descriptionText.text = value ?? string.Empty;
    }

    public void SetHullText(string value)
    {
        if (hullText != null)
            hullText.text = value ?? string.Empty;
    }

    public void SetDurationText(string value)
    {
        if (durationText != null)
            durationText.text = value ?? string.Empty;
    }

    public void SetMoneyText(string value)
    {
        if (moneyText != null)
            moneyText.text = value ?? string.Empty;
    }

    public void SetOpenSlotsText(string value)
    {
        if (openSlotsText != null)
            openSlotsText.text = value ?? string.Empty;
    }

    public void SetNoModulesVisible(bool visible, string text = null)
    {
        if (noModulesText == null)
            return;

        noModulesText.gameObject.SetActive(visible);

        if (visible && text != null)
            noModulesText.text = text;
    }
}
