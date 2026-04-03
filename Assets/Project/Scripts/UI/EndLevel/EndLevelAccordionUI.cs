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
    private bool bonusExpanded = false;

    private bool goalsIsAnimating = false;
    private bool bonusIsAnimating = false;

    private Coroutine goalsToggleRoutine;
    private Coroutine bonusToggleRoutine;

    // Coroutine d accordéon : fermer puis ouvrir.
    private Coroutine accordionRoutine;

    // Cache de hauteur pour eviter les surprises si l objet est desactive.
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

    /// <summary>
    /// Active ou desactive l interaction utilisateur sur les titres.
    /// </summary>
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
    /// Helper simple.
    /// Pour un etat final de ceremonie robuste, preferer
    /// ForceCeremonyStartStateInstant ou ForceCeremonyEndStateInstant.
    /// </summary>
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
    /// A appeler si le contenu Goals a change.
    /// Recalcule la hauteur ouverte cachee.
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
    /// A appeler si le contenu Bonus a change.
    /// Recalcule la hauteur ouverte cachee.
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

    /// <summary>
    /// Clique sur le titre Goals.
    /// Respecte la regle d accordéon :
    /// si Bonus est ouvert et qu on veut ouvrir Goals,
    /// on ferme Bonus avant d ouvrir Goals.
    /// </summary>
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

    /// <summary>
    /// Clique sur le titre Bonus.
    /// Respecte la regle d accordéon :
    /// si Goals est ouvert et qu on veut ouvrir Bonus,
    /// on ferme Goals avant d ouvrir Bonus.
    /// </summary>
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

    // ----------------------------------------------------------
    // ACCORDEON ROUTINES
    // ----------------------------------------------------------

    /// <summary>
    /// Ferme Bonus puis ouvre Goals.
    /// </summary>
    private IEnumerator CloseBonusThenOpenGoals()
    {
        SetBonusExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(bonusToggleDuration);

        SetGoalsExpanded(true, instant: false);

        accordionRoutine = null;
    }

    /// <summary>
    /// Ferme Goals puis ouvre Bonus.
    /// </summary>
    private IEnumerator CloseGoalsThenOpenBonus()
    {
        SetGoalsExpanded(false, instant: false);
        yield return new WaitForSecondsRealtime(goalsToggleDuration);

        SetBonusExpanded(true, instant: false);

        accordionRoutine = null;
    }

    // ----------------------------------------------------------
    // EXPAND / COLLAPSE GOALS
    // ----------------------------------------------------------

    /// <summary>
    /// Ouvre ou ferme Goals.
    /// </summary>
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

    /// <summary>
    /// Ouvre ou ferme Bonus.
    /// </summary>
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

    /// <summary>
    /// Met a jour la rotation de la fleche.
    /// </summary>
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

    /// <summary>
    /// Anime la rotation d une fleche.
    /// </summary>
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

    /// <summary>
    /// Renvoie la preferredHeight d un block avec rebuild force.
    /// </summary>
    private float GetPreferredHeightSafe(RectTransform rt)
    {
        if (rt == null)
            return 0f;

        ForceLayoutRebuild();
        return Mathf.Max(0f, LayoutUtility.GetPreferredHeight(rt));
    }

    /// <summary>
    /// Anime la preferredHeight d une section.
    /// </summary>
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
    // INTERNAL HELPERS
    // ----------------------------------------------------------

    /// <summary>
    /// Stoppe proprement toutes les routines de l accordéon.
    /// </summary>
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

    // ----------------------------------------------------------
    // LAYOUT
    // ----------------------------------------------------------

    /// <summary>
    /// Force le rebuild du layout parent.
    /// </summary>
    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (panelContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelContainer);
    }
}