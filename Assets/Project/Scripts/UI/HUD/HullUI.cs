using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Affichage du Hull : current / max, avec feedback visuel en cas de dégâts
/// (couleur rouge, gras, punch-scale).
/// </summary>
public class HullUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private string separator = "/";

    [Header("Feedback couleur / style")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color damageColor = Color.red;
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

        // On mémorise le scale de base une fois pour toutes
        baseScale = hullText != null ? hullText.rectTransform.localScale : Vector3.one;
    }

    /// <summary>
    /// Définit la valeur maximale de Hull (maxHull) et met à jour l'affichage.
    /// </summary>
    public void SetMaxHull(int max)
    {
        maxHull = Mathf.Max(0, max);
        RefreshText();
    }

    /// <summary>
    /// Met à jour la valeur courante de Hull (currentHull) et déclenche le
    /// feedback visuel si la valeur diminue (prise de dégâts).
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

    /// <summary>
    /// Met à jour le texte affiché : current/max ou juste current si max <= 0.
    /// </summary>
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
    /// Lance la routine de feedback visuel en cas de dégâts.
    /// </summary>
    private void PlayDamageFeedback()
    {
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(DamageFeedbackRoutine());
    }

    /// <summary>
    /// Routine qui gère :
    /// - passage en rouge + gras,
    /// - punch-scale du texte,
    /// - retour progressif au style et scale normaux.
    /// </summary>
    private IEnumerator DamageFeedbackRoutine()
    {
        if (hullText == null)
            yield break;

        // Style dégâts immédiat
        hullText.color = damageColor;
        hullText.fontStyle = FontStyles.Bold;

        RectTransform rt = hullText.rectTransform;
        rt.localScale = baseScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, damageFeedbackDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Curve type "punch" rapide :
            // - fort au début,
            // - retour progressif vers 1.
            // On utilise un sin pour le punch (0 -> pi) et un damper pour calmer la fin.
            float punchT = Mathf.Sin(t * Mathf.PI);        // 0 -> 1 -> 0
            float damper = 1f - t;                         // décroissance linéaire
            float scaleFactor = 1f + punchScaleAmount * punchT * damper;

            rt.localScale = baseScale * scaleFactor;

            yield return null;
        }

        // Retour au style normal
        hullText.color = normalColor;
        hullText.fontStyle = FontStyles.Normal;
        rt.localScale = baseScale;

        feedbackRoutine = null;
    }
}
