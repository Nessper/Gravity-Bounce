using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gere l accordéon Goals / Bonus :
/// - Expand / Collapse via preferredHeight (LayoutElement)
/// - Rotation des fleches
/// - Anti-spam
/// - Regle d accordéon : si on ouvre l un, on ferme l autre avant
///
/// IMPORTANT :
/// - Ce composant ne decide pas du timing de ceremonie.
/// - Il expose juste des operations d UI coherentes.
/// - Les boutons doivent etre bindes dans l Inspector.
/// </summary>
public class EndLevelAccordionUI : MonoBehaviour
{
    [Header("Layout Rebuild")]
    [SerializeField] private RectTransform panelContainer;

    [Header("Goals")]
    [SerializeField] private Button goalsTitleButton;
    [SerializeField] private RectTransform goalsLinesBlock;
    [SerializeField] private LayoutElement goalsLinesLayout;
    [SerializeField] private RectTransform goalsArrowIcon;
    [SerializeField] private float goalsToggleDuration = 0.18f;

    [Header("Bonus")]
    [SerializeField] private Button bonusTitleButton;
    [SerializeField] private RectTransform bonusLinesBlock;
    [SerializeField] private LayoutElement bonusLinesLayout;
    [SerializeField] private RectTransform bonusArrowIcon;
    [SerializeField] private float bonusToggleDuration = 0.18f;

    [Header("Arrow Icons")]
    [SerializeField] private float arrowRotateDuration = 0.12f;

    [Header("Anti-spam tap")]
    [SerializeField] private float toggleCooldownSec = 0.18f;

    private float nextGoalsToggleTime = 0f;
    private float nextBonusToggleTime = 0f;

    private bool togglesEnabled = false;

    private bool goalsExpanded = true;
    private bool bonusExpanded = false;

    private bool goalsIsAnimating = false;
    private bool bonusIsAnimating = false;

    private Coroutine goalsToggleRoutine;
    private Coroutine bonusToggleRoutine;
    private Coroutine accordionRoutine;

    private float cachedGoalsHeight = -1f;
    private float cachedBonusHeight = -1f;

    public float GoalsToggleDurationSec => goalsToggleDuration;
    public float BonusToggleDurationSec => bonusToggleDuration;

    public void SetInteractable(bool value)
    {
        togglesEnabled = value;

        if (goalsTitleButton != null)
            goalsTitleButton.interactable = value;

        if (bonusTitleButton != null)
            bonusTitleButton.interactable = value;
    }

    public bool AreTogglesEnabled() => togglesEnabled;
    public bool IsGoalsExpanded() => goalsExpanded;
    public bool IsBonusExpanded() => bonusExpanded;
    public bool IsAnimating() => goalsIsAnimating || bonusIsAnimating;

    public void SetState(bool goalsExpandedValue, bool bonusExpandedValue, bool instant)
    {
        SetGoalsExpanded(goalsExpandedValue, instant);
        SetBonusExpanded(bonusExpandedValue, instant);
    }

    /// <summary>
    /// Etat de depart normal de la ceremonie :
    /// - Goals ouvert
    /// - Bonus ferme
    ///
    /// Methode atomique : ne passe pas par deux setters separes.
    /// </summary>
    public void ForceCeremonyStartStateInstant()
    {
        StopAllAccordionRoutines();

        goalsExpanded = true;
        bonusExpanded = false;

        bool prevGoalsActive = goalsLinesBlock != null && goalsLinesBlock.gameObject.activeSelf;
        bool prevBonusActive = bonusLinesBlock != null && bonusLinesBlock.gameObject.activeSelf;

        if (goalsLinesBlock != null)
            goalsLinesBlock.gameObject.SetActive(true);

        if (bonusLinesBlock != null)
            bonusLinesBlock.gameObject.SetActive(true);

        ForceLayoutRebuild();

        cachedGoalsHeight = GetPreferredHeightSafe(goalsLinesBlock);
        cachedBonusHeight = GetPreferredHeightSafe(bonusLinesBlock);

        if (goalsLinesLayout != null)
            goalsLinesLayout.preferredHeight = cachedGoalsHeight;

        if (bonusLinesLayout != null)
            bonusLinesLayout.preferredHeight = 0f;

        if (goalsLinesBlock != null)
            goalsLinesBlock.gameObject.SetActive(true);

        if (bonusLinesBlock != null)
            bonusLinesBlock.gameObject.SetActive(false);

        UpdateArrowIcon(goalsArrowIcon, true, true);
        UpdateArrowIcon(bonusArrowIcon, false, true);

        ForceLayoutRebuild();
    }

    /// <summary>
    /// Etat final normal vise en fin de ceremonie :
    /// - Goals replie
    /// - Bonus ouvert
    ///
    /// Methode atomique reservee au skip de ceremonie.
    /// </summary>
    public void ForceCeremonyEndStateInstant()
    {
        StopAllAccordionRoutines();

        goalsExpanded = false;
        bonusExpanded = true;

        if (goalsLinesBlock != null)
            goalsLinesBlock.gameObject.SetActive(true);

        if (bonusLinesBlock != null)
            bonusLinesBlock.gameObject.SetActive(true);

        ForceLayoutRebuild();

        cachedGoalsHeight = GetPreferredHeightSafe(goalsLinesBlock);
        cachedBonusHeight = GetPreferredHeightSafe(bonusLinesBlock);

        if (goalsLinesLayout != null)
            goalsLinesLayout.preferredHeight = 0f;

        if (bonusLinesLayout != null)
            bonusLinesLayout.preferredHeight = cachedBonusHeight;

        if (goalsLinesBlock != null)
            goalsLinesBlock.gameObject.SetActive(false);

        if (bonusLinesBlock != null)
            bonusLinesBlock.gameObject.SetActive(true);

        UpdateArrowIcon(goalsArrowIcon, false, true);
        UpdateArrowIcon(bonusArrowIcon, true, true);

        ForceLayoutRebuild();
    }

    /// <summary>
    /// Recalcule la hauteur ouverte de Goals sans casser son etat actif/inactif.
    /// </summary>
    public void RefreshGoalsCachedHeight()
    {
        if (goalsLinesBlock == null)
            return;

        bool wasActive = goalsLinesBlock.gameObject.activeSelf;

        goalsLinesBlock.gameObject.SetActive(true);
        ForceLayoutRebuild();
        cachedGoalsHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(goalsLinesBlock));

        goalsLinesBlock.gameObject.SetActive(wasActive);
        ForceLayoutRebuild();
    }

    /// <summary>
    /// Recalcule la hauteur ouverte de Bonus sans casser son etat actif/inactif.
    /// </summary>
    public void RefreshBonusCachedHeight()
    {
        if (bonusLinesBlock == null)
            return;

        bool wasActive = bonusLinesBlock.gameObject.activeSelf;

        bonusLinesBlock.gameObject.SetActive(true);
        ForceLayoutRebuild();
        cachedBonusHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(bonusLinesBlock));

        bonusLinesBlock.gameObject.SetActive(wasActive);
        ForceLayoutRebuild();
    }

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

        if (targetExpandBonus && goalsExpanded)
        {
            if (accordionRoutine != null)
                StopCoroutine(accordionRoutine);

            accordionRoutine = StartCoroutine(CloseGoalsThenOpenBonus());
            return;
        }

        SetBonusExpanded(targetExpandBonus, instant: false);
    }

    private IEnumerator CloseBonusThenOpenGoals()
    {
        SetBonusExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(bonusToggleDuration);

        SetGoalsExpanded(true, instant: false);

        accordionRoutine = null;
    }

    private IEnumerator CloseGoalsThenOpenBonus()
    {
        SetGoalsExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(goalsToggleDuration);

        SetBonusExpanded(true, instant: false);

        accordionRoutine = null;
    }

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
            else
                goalsLinesBlock.gameObject.SetActive(true);

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
            else
                bonusLinesBlock.gameObject.SetActive(true);

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
        System.Action onDone)
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

    private void StopAllAccordionRoutines()
    {
        if (accordionRoutine != null)
        {
            StopCoroutine(accordionRoutine);
            accordionRoutine = null;
        }

        if (goalsToggleRoutine != null)
        {
            StopCoroutine(goalsToggleRoutine);
            goalsToggleRoutine = null;
        }

        if (bonusToggleRoutine != null)
        {
            StopCoroutine(bonusToggleRoutine);
            bonusToggleRoutine = null;
        }

        goalsIsAnimating = false;
        bonusIsAnimating = false;
    }

    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (panelContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelContainer);
    }
}