using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Affichage du Hull : current / max.
/// - Couleur dynamique selon le pourcentage de Hull.
/// - Feedback visuel en cas de dégâts (flash rouge + punch-scale).
/// </summary>
public class HullUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private string separator = "/";

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
    private int maxHull = 0;

    private Coroutine feedbackRoutine;
    private Vector3 baseScale;

    private void Awake()
    {
        if (hullText == null)
            Debug.LogError("[HullUI] hullText non assigné.", this);

        baseScale = hullText != null
            ? hullText.rectTransform.localScale
            : Vector3.one;
    }

    // --------------------------------------------------------------------
    // API PUBLIQUE
    // --------------------------------------------------------------------

    /// <summary>
    /// Définit la valeur maximale de Hull.
    /// </summary>
    public void SetMaxHull(int max)
    {
        maxHull = Mathf.Max(0, max);
        RefreshText();
        RefreshStateColor();
    }

    /// <summary>
    /// Met à jour la valeur courante de Hull.
    /// Déclenche le feedback visuel si dégâts.
    /// </summary>
    public void SetCurrentHull(int newHull)
    {
        if (maxHull > 0)
            newHull = Mathf.Clamp(newHull, 0, maxHull);
        else
            newHull = Mathf.Max(0, newHull);

        bool tookDamage = newHull < currentHull;

        currentHull = newHull;
        RefreshText();
        RefreshStateColor();

        if (tookDamage)
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

        if (maxHull > 0)
            hullText.text = currentHull + separator + maxHull;
        else
            hullText.text = currentHull.ToString();
    }

    /// <summary>
    /// Applique la couleur d'état en fonction du pourcentage de Hull.
    /// Ignoré si un feedback dégâts est en cours.
    /// </summary>
    private void RefreshStateColor()
    {
        if (hullText == null)
            return;

        // Si un feedback dégâts est actif, on ne touche pas à la couleur
        if (feedbackRoutine != null)
            return;

        hullText.color = GetStateColor();
        var c = GetStateColor();
        hullText.color = c;

    }

    private Color GetStateColor()
    {
        if (maxHull <= 0)
            return normalColor;

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

        // Style dégâts immédiat
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

        // Fin du feedback : retour au style normal + couleur d'état
        hullText.fontStyle = FontStyles.Normal;
        rt.localScale = baseScale;

        feedbackRoutine = null;
        RefreshStateColor();
    }
}
