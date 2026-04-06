using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Affichage du Hull : current / max.
/// Responsabilites :
/// - afficher la valeur courante et max,
/// - colorer selon l'etat,
/// - jouer des feedbacks visuels uniquement sur demande explicite.
///
/// Regle importante :
/// - SetCurrentHull / SetMaxHull ne declenchent aucun feedback automatiquement.
/// - Les feedbacks doivent etre demandes explicitement par le systeme appelant.
/// </summary>
public class HullUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private string separator = "/";

    [Header("Format (TMP Rich Text)")]
    [SerializeField] private string prefixRichText = "";

    [Header("Mode")]
    [SerializeField] private bool enableDamageFeedback = true;

    [Header("State Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Positive Feedback Colors")]
    [SerializeField] private Color repairFlashColor = new Color(0.35f, 1f, 0.45f);
    [SerializeField] private Color maxHullFlashColor = new Color(0.72f, 0.95f, 0.35f);

    [Header("Attention Flash (Tutorial / Focus)")]
    [SerializeField] private Color attentionFlashColor = Color.white;
    [SerializeField] private float attentionFlashDuration = 1.2f;
    [SerializeField] private float attentionFlashSpeed = 8f;
    [SerializeField] private bool attentionFlashBold = true;

    [Header("Thresholds")]
    [Range(0f, 1f)]
    [SerializeField] private float warningThreshold = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float criticalThreshold = 0.2f;

    [Header("Durations")]
    [SerializeField] private float damageFeedbackDuration = 0.25f;
    [SerializeField] private float repairFeedbackDuration = 0.25f;
    [SerializeField] private float maxHullFeedbackDuration = 0.35f;

    [Header("Scale")]
    [SerializeField] private float damagePunchScaleAmount = 0.35f;
    [SerializeField] private float repairPunchScaleAmount = 0.2f;
    [SerializeField] private float maxHullPunchScaleAmount = 0.28f;

    [Header("SFX")]
    [SerializeField] private SfxId addHullSfx = SfxId.AddHull;
    [SerializeField] private SfxId addMaxHullSfx = SfxId.AddMaxHull;

    private int currentHull = 0;
    private int maxHull = 1;

    private Coroutine feedbackRoutine;
    private Vector3 baseScale = Vector3.one;
    private bool attentionFlashLoopRequested;

    private void Awake()
    {
        if (hullText == null)
        {
            Debug.LogError("[HullUI] hullText is not assigned.", this);
            return;
        }

        RectTransform rt = hullText.rectTransform;

        if (Mathf.Approximately(rt.localScale.x, 0f) ||
            Mathf.Approximately(rt.localScale.y, 0f))
        {
            rt.localScale = Vector3.one;
        }

        baseScale = rt.localScale;

        RefreshText();
        RefreshStateColor(force: true);
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    // ------------------------------------------------------------
    // Public API kept for compatibility
    // ------------------------------------------------------------

    public void SetDamageFeedbackEnabled(bool enabled)
    {
        enableDamageFeedback = enabled;

        if (!enableDamageFeedback && feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
            attentionFlashLoopRequested = false;
            RestoreBaseVisualState();
            RefreshStateColor(force: true);
        }
    }

    public void SetPrefixRichText(string prefix)
    {
        prefixRichText = prefix ?? string.Empty;
        RefreshText();
    }

    public void SetHull(int value)
    {
        SetCurrentHull(value);
    }

    // ------------------------------------------------------------
    // Display only
    // ------------------------------------------------------------

    public void SetMaxHull(int max)
    {
        maxHull = Mathf.Max(1, max);
        currentHull = Mathf.Clamp(currentHull, 0, maxHull);

        RefreshText();
        RefreshStateColor(force: true);
    }

    public void SetCurrentHull(int newHull)
    {
        currentHull = Mathf.Clamp(Mathf.Max(0, newHull), 0, maxHull);

        RefreshText();
        RefreshStateColor(force: false);
    }

    public void ResetVisualState()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        attentionFlashLoopRequested = false;
        RestoreBaseVisualState();
        RefreshStateColor(force: true);
    }

    // ------------------------------------------------------------
    // Explicit feedbacks only
    // ------------------------------------------------------------

    public void PlayDamageFeedback()
    {
        if (hullText == null || !gameObject.activeInHierarchy)
            return;

        if (!enableDamageFeedback)
            return;

        attentionFlashLoopRequested = false;
        StartFeedbackRoutine(DamageFeedbackRoutine());
    }

    public void PlayRepairFeedback()
    {
        if (hullText == null || !gameObject.activeInHierarchy)
            return;

        attentionFlashLoopRequested = false;
        BootRoot.Audio?.PlayUi(addHullSfx);
        StartFeedbackRoutine(RepairFeedbackRoutine());
    }

    public void PlayMaxHullFeedback()
    {
        if (hullText == null || !gameObject.activeInHierarchy)
            return;

        attentionFlashLoopRequested = false;
        BootRoot.Audio?.PlayUi(addMaxHullSfx);
        StartFeedbackRoutine(MaxHullFeedbackRoutine());
    }

    public void PlayAttentionFlash()
    {
        if (hullText == null || !gameObject.activeInHierarchy)
            return;

        attentionFlashLoopRequested = false;
        StartFeedbackRoutine(AttentionFlashRoutine());
    }

    public void StartAttentionFlashLoop()
    {
        if (hullText == null || !gameObject.activeInHierarchy)
            return;

        attentionFlashLoopRequested = true;
        StartFeedbackRoutine(AttentionFlashLoopRoutine());
    }

    public void StopAttentionFlash()
    {
        attentionFlashLoopRequested = false;

        if (feedbackRoutine == null)
            return;

        StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        RestoreBaseVisualState();
        RefreshStateColor(force: true);
    }

    // ------------------------------------------------------------
    // Internal display helpers
    // ------------------------------------------------------------

    private void RefreshText()
    {
        if (hullText == null)
            return;

        string value = currentHull + separator + maxHull;

        if (!string.IsNullOrEmpty(prefixRichText))
            hullText.text = prefixRichText + value;
        else
            hullText.text = value;
    }

    private void RefreshStateColor(bool force)
    {
        if (hullText == null)
            return;

        if (!force && feedbackRoutine != null)
            return;

        hullText.color = GetStateColor();
    }

    private Color GetStateColor()
    {
        float ratio = (float)currentHull / maxHull;

        if (ratio <= criticalThreshold)
            return criticalColor;

        if (ratio <= warningThreshold)
            return warningColor;

        return normalColor;
    }

    // ------------------------------------------------------------
    // Feedback internals
    // ------------------------------------------------------------

    private void StartFeedbackRoutine(IEnumerator routine)
    {
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        RestoreBaseScaleOnly();
        feedbackRoutine = StartCoroutine(routine);
    }

    private void RestoreBaseVisualState()
    {
        if (hullText == null)
            return;

        hullText.fontStyle = FontStyles.Normal;
        RestoreBaseScaleOnly();
    }

    private void RestoreBaseScaleOnly()
    {
        if (hullText == null)
            return;

        RectTransform rt = hullText.rectTransform;

        if (Mathf.Approximately(baseScale.x, 0f) || Mathf.Approximately(baseScale.y, 0f))
            baseScale = Vector3.one;

        rt.localScale = baseScale;
    }

    private IEnumerator DamageFeedbackRoutine()
    {
        if (hullText == null)
            yield break;

        hullText.color = criticalColor;
        hullText.fontStyle = FontStyles.Bold;

        RectTransform rt = hullText.rectTransform;
        RestoreBaseScaleOnly();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, damageFeedbackDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float punchT = Mathf.Sin(t * Mathf.PI);
            float damper = 1f - t;
            float scaleFactor = 1f + damagePunchScaleAmount * punchT * damper;

            rt.localScale = baseScale * scaleFactor;
            yield return null;
        }

        RestoreBaseVisualState();
        feedbackRoutine = null;
        RefreshStateColor(force: true);
    }

    private IEnumerator RepairFeedbackRoutine()
    {
        if (hullText == null)
            yield break;

        hullText.color = repairFlashColor;
        hullText.fontStyle = FontStyles.Bold;

        RectTransform rt = hullText.rectTransform;
        RestoreBaseScaleOnly();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, repairFeedbackDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float punchT = Mathf.Sin(t * Mathf.PI);
            float damper = 1f - t;
            float scaleFactor = 1f + repairPunchScaleAmount * punchT * damper;

            rt.localScale = baseScale * scaleFactor;
            yield return null;
        }

        RestoreBaseVisualState();
        feedbackRoutine = null;
        RefreshStateColor(force: true);
    }

    private IEnumerator MaxHullFeedbackRoutine()
    {
        if (hullText == null)
            yield break;

        hullText.color = maxHullFlashColor;
        hullText.fontStyle = FontStyles.Bold;

        RectTransform rt = hullText.rectTransform;
        RestoreBaseScaleOnly();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, maxHullFeedbackDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float punchT = Mathf.Sin(t * Mathf.PI);
            float damper = 1f - t;
            float scaleFactor = 1f + maxHullPunchScaleAmount * punchT * damper;

            rt.localScale = baseScale * scaleFactor;
            yield return null;
        }

        RestoreBaseVisualState();
        feedbackRoutine = null;
        RefreshStateColor(force: true);
    }

    private IEnumerator AttentionFlashRoutine()
    {
        if (hullText == null)
            yield break;

        RectTransform rt = hullText.rectTransform;
        RestoreBaseScaleOnly();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, attentionFlashDuration);

        Color baseColor = GetStateColor();
        hullText.fontStyle = attentionFlashBold ? FontStyles.Bold : FontStyles.Normal;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.PingPong(elapsed * attentionFlashSpeed, 1f);
            hullText.color = Color.Lerp(baseColor, attentionFlashColor, t);

            yield return null;
        }

        RestoreBaseVisualState();
        feedbackRoutine = null;
        RefreshStateColor(force: true);
    }

    private IEnumerator AttentionFlashLoopRoutine()
    {
        if (hullText == null)
            yield break;

        RectTransform rt = hullText.rectTransform;
        RestoreBaseScaleOnly();

        Color baseColor = GetStateColor();
        hullText.fontStyle = attentionFlashBold ? FontStyles.Bold : FontStyles.Normal;

        while (attentionFlashLoopRequested)
        {
            float t = Mathf.PingPong(Time.unscaledTime * attentionFlashSpeed, 1f);
            hullText.color = Color.Lerp(baseColor, attentionFlashColor, t);
            yield return null;
        }

        RestoreBaseVisualState();
        feedbackRoutine = null;
        RefreshStateColor(force: true);
    }
}