using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère l'animation visuelle du marqueur d'objectif sur la ProgressBar :
/// - au départ, affiche l'icône par défaut (état "pas encore atteint")
/// - lorsqu'on appelle PlayReached(), bascule sur l'icône "reached"
///   et joue une petite animation de scale (punch).
///
/// IMPORTANT :
/// - Ce script ne modifie JAMAIS la position (anchoredPosition) du marker,
///   uniquement le scale local du GameObject qui le porte.
/// - Une fois l'animation jouée, l'icône "reached" reste affichée.
/// </summary>
public class ThresholdPulseUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Image defaultIcon;
    [SerializeField] private Image reachedIcon;

    [Header("Animation")]
    [SerializeField] private float scaleUpDuration = 0.15f;
    [SerializeField] private float scaleDownDuration = 0.12f;
    [SerializeField] private float punchScale = 1.3f;

    private bool hasPlayed = false;
    private Vector3 baseScale;
    private Coroutine currentCo;

    private void Awake()
    {
        // On mémorise le scale de départ (tel que tu l'as posé dans l'UI)
        baseScale = transform.localScale;

        // État initial : icône "default" visible, "reached" cachée
        if (defaultIcon != null) defaultIcon.enabled = true;
        if (reachedIcon != null) reachedIcon.enabled = false;
    }

    /// <summary>
    /// Appelé lorsqu'on atteint pour la première fois le seuil d'objectif.
    /// Bascule en mode "reached" et joue un petit punch de scale.
    /// </summary>
    public void PlayReached()
    {
        if (hasPlayed)
            return;

        hasPlayed = true;

        // On s'assure de stopper une éventuelle animation précédente (par sécurité)
        if (currentCo != null)
        {
            StopCoroutine(currentCo);
            currentCo = null;
        }

        // Swap d'icônes : on ne reviendra plus jamais sur la version default
        if (defaultIcon != null) defaultIcon.enabled = false;
        if (reachedIcon != null) reachedIcon.enabled = true;

        // On relance le scale depuis l'échelle de base
        transform.localScale = baseScale;

        currentCo = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        // Phase 1 : scale-up
        float t = 0f;
        while (t < scaleUpDuration)
        {
            t += Time.unscaledDeltaTime; // on utilise le temps "réel", pas affecté par la timeScale
            float k = Mathf.Clamp01(t / scaleUpDuration);
            float s = Mathf.Lerp(1f, punchScale, k);
            transform.localScale = baseScale * s;
            yield return null;
        }

        // Phase 2 : scale-down
        t = 0f;
        while (t < scaleDownDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / scaleDownDuration);
            float s = Mathf.Lerp(punchScale, 1f, k);
            transform.localScale = baseScale * s;
            yield return null;
        }

        // On s'assure de terminer précisément à l'échelle de base
        transform.localScale = baseScale;
        currentCo = null;
    }
}
