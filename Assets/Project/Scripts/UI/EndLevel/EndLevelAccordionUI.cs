using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère l'accordéon Goals / Combos :
/// - Expand/Collapse par animation de preferredHeight (LayoutElement)
/// - Rotation des icônes de flèche
/// - Anti-spam tap (cooldown)
/// - Règle accordéon : si on ouvre l'un, on ferme l'autre avant (évite double resize)
///
/// IMPORTANT :
/// - Ne décide pas du "quand" (cérémonie) : c'est EndLevelUI.
/// - Les boutons doivent être bind dans l'Inspector sur OnGoalsTitleClicked / OnCombosTitleClicked.
/// </summary>
public class EndLevelAccordionUI : MonoBehaviour
{
    // ----------------------------------------------------------
    // LAYOUT REBUILD
    // ----------------------------------------------------------

    [Header("Layout Rebuild")]
    [SerializeField] private RectTransform panelContainer;

    // ----------------------------------------------------------
    // GOALS
    // ----------------------------------------------------------

    [Header("Goals")]
    [SerializeField] private Button goalsTitleButton;
    [SerializeField] private RectTransform goalsLinesBlock;
    [SerializeField] private LayoutElement goalsLinesLayout;
    [SerializeField] private RectTransform goalsArrowIcon;
    [SerializeField] private float goalsToggleDuration = 0.18f;

    // ----------------------------------------------------------
    // COMBOS
    // ----------------------------------------------------------

    [Header("Combos")]
    [SerializeField] private Button combosTitleButton;
    [SerializeField] private RectTransform combosLinesBlock;
    [SerializeField] private LayoutElement combosLinesLayout;
    [SerializeField] private RectTransform combosArrowIcon;
    [SerializeField] private float combosToggleDuration = 0.18f;

    // ----------------------------------------------------------
    // ICONES
    // ----------------------------------------------------------

    [Header("Arrow Icons")]
    [SerializeField] private float arrowRotateDuration = 0.12f;

    // ----------------------------------------------------------
    // ANTI-SPAM
    // ----------------------------------------------------------

    [Header("Anti-spam tap")]
    [SerializeField] private float toggleCooldownSec = 0.18f;

    private float nextGoalsToggleTime = 0f;
    private float nextCombosToggleTime = 0f;

    // ----------------------------------------------------------
    // STATE
    // ----------------------------------------------------------

    private bool togglesEnabled = false;

    private bool goalsExpanded = true;
    private bool combosExpanded = true;

    private bool goalsIsAnimating = false;
    private bool combosIsAnimating = false;

    private Coroutine goalsToggleRoutine;
    private Coroutine combosToggleRoutine;

    // Coroutine d'accordéon (fermer puis ouvrir). On la stoppe avant d'en relancer une.
    private Coroutine accordionRoutine;

    // Cache de hauteur (utile car LayoutUtility peut être instable si l'objet est désactivé)
    private float cachedGoalsHeight = -1f;
    private float cachedCombosHeight = -1f;

    // ----------------------------------------------------------
    // PROPERTIES
    // ----------------------------------------------------------

    public float GoalsToggleDurationSec
    {
        get { return goalsToggleDuration; }
    }

    public float CombosToggleDurationSec
    {
        get { return combosToggleDuration; }
    }

    // ----------------------------------------------------------
    // API
    // ----------------------------------------------------------

    public void SetInteractable(bool value)
    {
        togglesEnabled = value;

        if (goalsTitleButton != null)
            goalsTitleButton.interactable = value;

        if (combosTitleButton != null)
            combosTitleButton.interactable = value;
    }

    public bool AreTogglesEnabled()
    {
        return togglesEnabled;
    }

    public bool IsGoalsExpanded()
    {
        return goalsExpanded;
    }

    public bool IsCombosExpanded()
    {
        return combosExpanded;
    }

    public bool IsAnimating()
    {
        return goalsIsAnimating || combosIsAnimating;
    }

    /// <summary>
    /// Permet à EndLevelUI de forcer un état au début/fin de cérémonie.
    /// </summary>
    public void SetState(bool goalsExpandedValue, bool combosExpandedValue, bool instant)
    {
        SetGoalsExpanded(goalsExpandedValue, instant);
        SetCombosExpanded(combosExpandedValue, instant);
    }

    /// <summary>
    /// A appeler si le contenu Goals a changé (ajout/suppression de lignes),
    /// afin de recalculer la hauteur ouverte.
    /// </summary>
    public void RefreshGoalsCachedHeight()
    {
        if (goalsLinesBlock == null)
            return;

        goalsLinesBlock.gameObject.SetActive(true);
        ForceLayoutRebuild();
        cachedGoalsHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(goalsLinesBlock));
    }

    /// <summary>
    /// A appeler si le contenu Combos a changé (reveal),
    /// afin de recalculer la hauteur ouverte.
    /// </summary>
    public void RefreshCombosCachedHeight()
    {
        if (combosLinesBlock == null)
            return;

        combosLinesBlock.gameObject.SetActive(true);
        ForceLayoutRebuild();
        cachedCombosHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(combosLinesBlock));
    }

    // ----------------------------------------------------------
    // BOUTONS (INSPECTOR)
    // ----------------------------------------------------------

    public void OnGoalsTitleClicked()
    {
        if (!togglesEnabled)
            return;

        if (Time.unscaledTime < nextGoalsToggleTime)
            return;
        nextGoalsToggleTime = Time.unscaledTime + toggleCooldownSec;

        if (goalsIsAnimating || combosIsAnimating)
            return;

        bool targetExpandGoals = !goalsExpanded;

        // Si on veut ouvrir Goals alors que Combos est ouvert, on fait l'accordéon.
        if (targetExpandGoals && combosExpanded)
        {
            if (accordionRoutine != null)
                StopCoroutine(accordionRoutine);

            accordionRoutine = StartCoroutine(CloseCombosThenOpenGoals());
            return;
        }

        SetGoalsExpanded(targetExpandGoals, instant: false);
    }

    public void OnCombosTitleClicked()
    {
        if (!togglesEnabled)
            return;

        if (Time.unscaledTime < nextCombosToggleTime)
            return;
        nextCombosToggleTime = Time.unscaledTime + toggleCooldownSec;

        if (goalsIsAnimating || combosIsAnimating)
            return;

        bool targetExpandCombos = !combosExpanded;

        // Si on veut ouvrir Combos alors que Goals est ouvert, on fait l'accordéon.
        if (targetExpandCombos && goalsExpanded)
        {
            if (accordionRoutine != null)
                StopCoroutine(accordionRoutine);

            accordionRoutine = StartCoroutine(CloseGoalsThenOpenCombos());
            return;
        }

        SetCombosExpanded(targetExpandCombos, instant: false);
    }

    // ----------------------------------------------------------
    // ACCORDEON ROUTINES
    // ----------------------------------------------------------

    private IEnumerator CloseGoalsThenOpenCombos()
    {
        SetGoalsExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(goalsToggleDuration);

        SetCombosExpanded(true, instant: false);

        accordionRoutine = null;
    }

    private IEnumerator CloseCombosThenOpenGoals()
    {
        SetCombosExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(combosToggleDuration);

        SetGoalsExpanded(true, instant: false);

        accordionRoutine = null;
    }

    // ----------------------------------------------------------
    // EXPAND / COLLAPSE GOALS
    // ----------------------------------------------------------

    public void SetGoalsExpanded(bool expanded, bool instant)
    {
        if (accordionRoutine != null)
        {
            StopCoroutine(accordionRoutine);
            accordionRoutine = null;
        }


        if (goalsLinesBlock == null || goalsLinesLayout == null)
            return;

        goalsExpanded = expanded;
        UpdateArrowIcon(goalsArrowIcon, goalsExpanded, instant);

        if (goalsToggleRoutine != null)
            StopCoroutine(goalsToggleRoutine);

        if (expanded)
        {
            goalsLinesBlock.gameObject.SetActive(true);
            ForceLayoutRebuild();
            cachedGoalsHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(goalsLinesBlock));
        }

        float from = goalsLinesLayout.preferredHeight;
        if (from <= 0f && goalsLinesBlock.gameObject.activeSelf)
            from = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(goalsLinesBlock));

        float openHeight = (cachedGoalsHeight > 0f) ? cachedGoalsHeight : GetPreferredHeightSafe(goalsLinesBlock);
        float to = expanded ? openHeight : 0f;

        if (instant)
        {
            goalsIsAnimating = false;

            goalsLinesLayout.preferredHeight = to;
            if (!expanded)
                goalsLinesBlock.gameObject.SetActive(false);

            ForceLayoutRebuild();
            return;
        }

        goalsIsAnimating = true;

        goalsToggleRoutine = StartCoroutine(AnimateSectionHeight(
            layout: goalsLinesLayout,
            block: goalsLinesBlock,
            from: from,
            to: to,
            duration: goalsToggleDuration,
            disableAtEnd: !expanded,
            onDone: () => goalsIsAnimating = false
        ));
    }

    // ----------------------------------------------------------
    // EXPAND / COLLAPSE COMBOS
    // ----------------------------------------------------------

    public void SetCombosExpanded(bool expanded, bool instant)
    {
        if (accordionRoutine != null)
        {
            StopCoroutine(accordionRoutine);
            accordionRoutine = null;
        }

        if (combosLinesBlock == null || combosLinesLayout == null)
            return;

        combosExpanded = expanded;
        UpdateArrowIcon(combosArrowIcon, combosExpanded, instant);

        if (combosToggleRoutine != null)
            StopCoroutine(combosToggleRoutine);

        if (expanded)
        {
            combosLinesBlock.gameObject.SetActive(true);
            ForceLayoutRebuild();
            cachedCombosHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(combosLinesBlock));
        }

        float from = combosLinesLayout.preferredHeight;
        if (from <= 0f && combosLinesBlock.gameObject.activeSelf)
            from = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(combosLinesBlock));

        float openHeight = (cachedCombosHeight > 0f) ? cachedCombosHeight : GetPreferredHeightSafe(combosLinesBlock);
        float to = expanded ? openHeight : 0f;

        if (instant)
        {
            combosIsAnimating = false;

            combosLinesLayout.preferredHeight = to;
            if (!expanded)
                combosLinesBlock.gameObject.SetActive(false);

            ForceLayoutRebuild();
            return;
        }

        combosIsAnimating = true;

        combosToggleRoutine = StartCoroutine(AnimateSectionHeight(
            layout: combosLinesLayout,
            block: combosLinesBlock,
            from: from,
            to: to,
            duration: combosToggleDuration,
            disableAtEnd: !expanded,
            onDone: () => combosIsAnimating = false
        ));
    }

    // ----------------------------------------------------------
    // ICONES
    // ----------------------------------------------------------

    private void UpdateArrowIcon(RectTransform arrow, bool expanded, bool instant)
    {
        if (arrow == null)
            return;

        float targetZ = expanded ? 180f : 0f;

        if (instant)
        {
            arrow.localRotation = Quaternion.Euler(0f, 0f, targetZ);
            return;
        }

        StartCoroutine(RotateArrowRoutine(arrow, targetZ));
    }

    private IEnumerator RotateArrowRoutine(RectTransform arrow, float targetZ)
    {
        float startZ = arrow.localEulerAngles.z;
        float t = 0f;

        while (t < arrowRotateDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / arrowRotateDuration);

            float z = Mathf.LerpAngle(startZ, targetZ, a);
            arrow.localRotation = Quaternion.Euler(0f, 0f, z);

            yield return null;
        }

        arrow.localRotation = Quaternion.Euler(0f, 0f, targetZ);
    }

    // ----------------------------------------------------------
    // ANIM HEIGHT
    // ----------------------------------------------------------

    private float GetPreferredHeightSafe(RectTransform rt)
    {
        if (rt == null)
            return 0f;

        ForceLayoutRebuild();
        return Mathf.Max(0f, LayoutUtility.GetPreferredHeight(rt));
    }

    private IEnumerator AnimateSectionHeight(
        LayoutElement layout,
        RectTransform block,
        float from,
        float to,
        float duration,
        bool disableAtEnd,
        System.Action onDone
    )
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);

            layout.preferredHeight = Mathf.Lerp(from, to, a);
            ForceLayoutRebuild();
            yield return null;
        }

        layout.preferredHeight = to;
        ForceLayoutRebuild();

        if (disableAtEnd)
        {
            block.gameObject.SetActive(false);
            ForceLayoutRebuild();
        }

        onDone?.Invoke();
    }

    // ----------------------------------------------------------
    // LAYOUT
    // ----------------------------------------------------------

    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (panelContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelContainer);
    }
}
