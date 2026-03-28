using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère l'accordéon Goals / Bonus :
/// - Expand/Collapse par animation de preferredHeight (LayoutElement)
/// - Rotation des icônes de flèche
/// - Anti-spam tap (cooldown)
/// - Règle accordéon : si on ouvre l'un, on ferme l'autre avant (évite double resize)
///
/// IMPORTANT :
/// - Ne décide pas du "quand" (cérémonie) : c'est EndLevelUI.
/// - Les boutons doivent être bind dans l'Inspector sur OnGoalsTitleClicked / OnBonusTitleClicked.
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
    // BONUS
    // ----------------------------------------------------------

    [Header("Bonus")]
    [SerializeField] private Button bonusTitleButton;
    [SerializeField] private RectTransform bonusLinesBlock;
    [SerializeField] private LayoutElement bonusLinesLayout;
    [SerializeField] private RectTransform bonusArrowIcon;
    [SerializeField] private float bonusToggleDuration = 0.18f;

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
    private float nextBonusToggleTime = 0f;

    // ----------------------------------------------------------
    // STATE
    // ----------------------------------------------------------

    private bool togglesEnabled = false;

    private bool goalsExpanded = true;
    private bool bonusExpanded = true;

    private bool goalsIsAnimating = false;
    private bool bonusIsAnimating = false;

    private Coroutine goalsToggleRoutine;
    private Coroutine bonusToggleRoutine;

    // Coroutine d'accordéon (fermer puis ouvrir). On la stoppe avant d'en relancer une.
    private Coroutine accordionRoutine;

    // Cache de hauteur (utile car LayoutUtility peut être instable si l'objet est désactivé)
    private float cachedGoalsHeight = -1f;
    private float cachedBonusHeight = -1f;

    // ----------------------------------------------------------
    // PROPERTIES
    // ----------------------------------------------------------

    public float GoalsToggleDurationSec
    {
        get { return goalsToggleDuration; }
    }

    public float BonusToggleDurationSec
    {
        get { return bonusToggleDuration; }
    }

    // ----------------------------------------------------------
    // API
    // ----------------------------------------------------------

    public void SetInteractable(bool value)
    {
        togglesEnabled = value;

        if (goalsTitleButton != null)
            goalsTitleButton.interactable = value;

        if (bonusTitleButton != null)
            bonusTitleButton.interactable = value;
    }

    public bool AreTogglesEnabled()
    {
        return togglesEnabled;
    }

    public bool IsGoalsExpanded()
    {
        return goalsExpanded;
    }

    public bool IsBonusExpanded()
    {
        return bonusExpanded;
    }

    public bool IsAnimating()
    {
        return goalsIsAnimating || bonusIsAnimating;
    }

    /// <summary>
    /// Permet à EndLevelUI de forcer un état au début/fin de cérémonie.
    /// </summary>
    public void SetState(bool goalsExpandedValue, bool bonusExpandedValue, bool instant)
    {
        SetGoalsExpanded(goalsExpandedValue, instant);
        SetBonusExpanded(bonusExpandedValue, instant);
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
    /// A appeler si le contenu Bonus a changé (reveal),
    /// afin de recalculer la hauteur ouverte.
    /// </summary>
    public void RefreshBonusCachedHeight()
    {
        if (bonusLinesBlock == null)
            return;

        bonusLinesBlock.gameObject.SetActive(true);
        ForceLayoutRebuild();
        cachedBonusHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(bonusLinesBlock));
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

        if (goalsIsAnimating || bonusIsAnimating)
            return;

        bool targetExpandGoals = !goalsExpanded;

        // Si on veut ouvrir Goals alors que Bonus est ouvert, on fait l'accordéon.
        if (targetExpandGoals && bonusExpanded)
        {
            if (accordionRoutine != null)
                StopCoroutine(accordionRoutine);

            accordionRoutine = StartCoroutine(CloseBonusThenOpenGoals());
            return;
        }

        SetGoalsExpanded(targetExpandGoals, instant: false);
    }

    public void OnBonusTitleClicked()
    {
        if (!togglesEnabled)
            return;

        if (Time.unscaledTime < nextBonusToggleTime)
            return;
        nextBonusToggleTime = Time.unscaledTime + toggleCooldownSec;

        if (goalsIsAnimating || bonusIsAnimating)
            return;

        bool targetExpandBonus = !bonusExpanded;

        // Si on veut ouvrir Bonus alors que Goals est ouvert, on fait l'accordéon.
        if (targetExpandBonus && goalsExpanded)
        {
            if (accordionRoutine != null)
                StopCoroutine(accordionRoutine);

            accordionRoutine = StartCoroutine(CloseGoalsThenOpenBonus());
            return;
        }

        SetBonusExpanded(targetExpandBonus, instant: false);
    }

    // ----------------------------------------------------------
    // ACCORDEON ROUTINES
    // ----------------------------------------------------------

    private IEnumerator CloseGoalsThenOpenBonus()
    {
        SetGoalsExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(goalsToggleDuration);

        SetBonusExpanded(true, instant: false);

        accordionRoutine = null;
    }

    private IEnumerator CloseBonusThenOpenGoals()
    {
        SetBonusExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(bonusToggleDuration);

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
    // EXPAND / COLLAPSE BONUS
    // ----------------------------------------------------------

    public void SetBonusExpanded(bool expanded, bool instant)
    {
        if (accordionRoutine != null)
        {
            StopCoroutine(accordionRoutine);
            accordionRoutine = null;
        }

        if (bonusLinesBlock == null || bonusLinesLayout == null)
            return;

        bonusExpanded = expanded;
        UpdateArrowIcon(bonusArrowIcon, bonusExpanded, instant);

        if (bonusToggleRoutine != null)
            StopCoroutine(bonusToggleRoutine);

        if (expanded)
        {
            bonusLinesBlock.gameObject.SetActive(true);
            ForceLayoutRebuild();
            cachedBonusHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(bonusLinesBlock));
        }

        float from = bonusLinesLayout.preferredHeight;
        if (from <= 0f && bonusLinesBlock.gameObject.activeSelf)
            from = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(bonusLinesBlock));

        float openHeight = (cachedBonusHeight > 0f) ? cachedBonusHeight : GetPreferredHeightSafe(bonusLinesBlock);
        float to = expanded ? openHeight : 0f;

        if (instant)
        {
            bonusIsAnimating = false;

            bonusLinesLayout.preferredHeight = to;
            if (!expanded)
                bonusLinesBlock.gameObject.SetActive(false);

            ForceLayoutRebuild();
            return;
        }

        bonusIsAnimating = true;

        bonusToggleRoutine = StartCoroutine(AnimateSectionHeight(
            layout: bonusLinesLayout,
            block: bonusLinesBlock,
            from: from,
            to: to,
            duration: bonusToggleDuration,
            disableAtEnd: !expanded,
            onDone: () => bonusIsAnimating = false
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