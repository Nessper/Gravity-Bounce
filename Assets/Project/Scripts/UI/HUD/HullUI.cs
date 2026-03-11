using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Affichage du Hull : current / max.
/// - Couleur dynamique selon le pourcentage de Hull.
/// - Feedback visuel optionnel en cas de dégâts (flash rouge + punch-scale).
/// - Support optionnel d'un préfixe TMP RichText (ex: icône via SpriteAsset).
///
/// Utilisation recommandée :
/// - Gameplay : enableDamageFeedback = true, prefixRichText = ""
/// - Shop / Briefing : enableDamageFeedback = false, prefixRichText = "<voffset=-6><sprite name=\"icon_hull\"></voffset> "
/// </summary>
public class HullUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private string separator = "/";

    [Header("Format (TMP Rich Text)")]
    [Tooltip("Préfixe optionnel ajouté avant la valeur (ex: icône TMP). Exemple: <voffset=-6><sprite name=\"icon_hull\"></voffset> ")]
    [SerializeField] private string prefixRichText = "";

    [Header("Mode")]
    [Tooltip("Si false, aucun feedback dégâts (pas de flash/punch). Recommandé hors gameplay.")]
    [SerializeField] private bool enableDamageFeedback = true;

    [Header("Couleurs d'état")]
    [Tooltip("Couleur normale (> 50%).")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("Couleur warning (<= 50%).")]
    [SerializeField] private Color warningColor = new Color(1f, 0.6f, 0.1f); // orange

    [Tooltip("Couleur critique (<= 20%).")]
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Seuils (en %)")]
    [Range(0f, 1f)]
    [SerializeField] private float warningThreshold = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float criticalThreshold = 0.2f;

    [Header("Feedback dégâts")]
    [SerializeField] private float damageFeedbackDuration = 0.25f;

    [Header("Feedback scale")]
    [Tooltip("Facteur de punch (0.3 = +30% de scale au pic).")]
    [SerializeField] private float punchScaleAmount = 0.35f;

    private int currentHull = 0;
    private int maxHull = 1;

    private Coroutine feedbackRoutine;
    private Vector3 baseScale;

    private void Awake()
    {
        if (hullText == null)
            Debug.LogError("[HullUI] hullText non assigné.", this);

        baseScale = hullText != null
            ? hullText.rectTransform.localScale
            : Vector3.one;

        RefreshText();
        RefreshStateColor(force: true);
    }

    // --------------------------------------------------------------------
    // API PUBLIQUE
    // --------------------------------------------------------------------

    public void SetDamageFeedbackEnabled(bool enabled)
    {
        enableDamageFeedback = enabled;

        if (!enableDamageFeedback && feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;

            if (hullText != null)
            {
                hullText.fontStyle = FontStyles.Normal;
                hullText.rectTransform.localScale = baseScale;
            }

            RefreshStateColor(force: true);
        }
    }

    /// <summary>
    /// Définit le préfixe TMP RichText (ex: icône via SpriteAsset).
    /// Exemple: "<voffset=-6><sprite name=\"icon_hull\"></voffset> "
    /// </summary>
    public void SetPrefixRichText(string prefix)
    {
        prefixRichText = prefix ?? "";
        RefreshText();
    }

    public void SetMaxHull(int max)
    {
        int newMax = Mathf.Max(1, max);

        if (newMax == maxHull)
        {
            RefreshText();
            RefreshStateColor(force: false);
            return;
        }

        maxHull = newMax;
        currentHull = Mathf.Clamp(currentHull, 0, maxHull);

        RefreshText();
        RefreshStateColor(force: true);
    }

    public void SetCurrentHull(int newHull)
    {
        int clamped = Mathf.Clamp(Mathf.Max(0, newHull), 0, maxHull);
        bool tookDamage = clamped < currentHull;

        currentHull = clamped;

        RefreshText();
        RefreshStateColor(force: false);

        if (tookDamage && enableDamageFeedback)
            PlayDamageFeedback();
    }

    /// <summary>
    /// Compatibilité avec l'ancien naming.
    /// </summary>
    public void SetHull(int value)
    {
        SetCurrentHull(value);
    }

    // --------------------------------------------------------------------
    // AFFICHAGE
    // --------------------------------------------------------------------

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

    // --------------------------------------------------------------------
    // FEEDBACK DÉGÂTS
    // --------------------------------------------------------------------

    private void PlayDamageFeedback()
    {
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(DamageFeedbackRoutine());
    }

    private IEnumerator DamageFeedbackRoutine()
    {
        if (hullText == null)
            yield break;

        hullText.color = criticalColor;
        hullText.fontStyle = FontStyles.Bold;

        RectTransform rt = hullText.rectTransform;
        rt.localScale = baseScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, damageFeedbackDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float punchT = Mathf.Sin(t * Mathf.PI); // 0 -> 1 -> 0
            float damper = 1f - t;
            float scaleFactor = 1f + punchScaleAmount * punchT * damper;

            rt.localScale = baseScale * scaleFactor;

            yield return null;
        }

        hullText.fontStyle = FontStyles.Normal;
        rt.localScale = baseScale;

        feedbackRoutine = null;
        RefreshStateColor(force: true);
    }
}
