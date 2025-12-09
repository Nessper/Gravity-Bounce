using System.Collections;
using UnityEngine;

/// <summary>
/// Gère l'affichage et l'animation des 3 médailles de fin de niveau.
/// Un seul script placé sur Medals_Panel, avec 3 CanvasGroup assignés.
/// 
/// Utilisation typique :
/// - ResetInstant() au début de la séquence de fin.
/// - ShowBronze / ShowSilver / ShowGold appelés par des UnityEvents
///   (par exemple depuis SegmentedFinalScoreBarUI lorsque les segments thresholds s'allument).
/// </summary>
public class EndLevelMedalsUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private CanvasGroup bronzeCG;
    [SerializeField] private CanvasGroup silverCG;
    [SerializeField] private CanvasGroup goldCG;

    [Header("Animation - Fade")]
    [SerializeField] private float fadeDuration = 0.25f;
    // Durée du fade-in de la médaille.

    [Header("Animation - Pulse")]
    [SerializeField] private float pulseScale = 1.25f;
    [SerializeField] private float pulseDuration = 0.18f;
    // pulseScale : facteur de scale au pic de l'animation.
    // pulseDuration : durée totale du pulse (aller-retour).

    // Coroutines en cours pour chaque médaille (pour éviter les doublons).
    private Coroutine bronzeRoutine;
    private Coroutine silverRoutine;
    private Coroutine goldRoutine;

    private void Awake()
    {
        ResetInstant();
    }

    /// <summary>
    /// Réinitialise immédiatement l'état visuel des médailles :
    /// alpha = 0, scale = 1, aucune coroutine en cours.
    /// </summary>
    public void ResetInstant()
    {
        if (bronzeRoutine != null)
        {
            StopCoroutine(bronzeRoutine);
            bronzeRoutine = null;
        }

        if (silverRoutine != null)
        {
            StopCoroutine(silverRoutine);
            silverRoutine = null;
        }

        if (goldRoutine != null)
        {
            StopCoroutine(goldRoutine);
            goldRoutine = null;
        }

        ResetMedal(bronzeCG);
        ResetMedal(silverCG);
        ResetMedal(goldCG);
    }

    public void ShowBronze()
    {
        if (bronzeCG == null)
            return;

        // Si déjà visible (appel multiple), on ignore.
        if (bronzeCG.alpha >= 0.99f)
            return;

        if (bronzeRoutine != null)
            StopCoroutine(bronzeRoutine);

        bronzeRoutine = StartCoroutine(ShowMedalRoutine(bronzeCG));
    }

    public void ShowSilver()
    {
        if (silverCG == null)
            return;

        if (silverCG.alpha >= 0.99f)
            return;

        if (silverRoutine != null)
            StopCoroutine(silverRoutine);

        silverRoutine = StartCoroutine(ShowMedalRoutine(silverCG));
    }

    public void ShowGold()
    {
        if (goldCG == null)
            return;

        if (goldCG.alpha >= 0.99f)
            return;

        if (goldRoutine != null)
            StopCoroutine(goldRoutine);

        goldRoutine = StartCoroutine(ShowMedalRoutine(goldCG));
    }

    // --------------------------------------------------------------------
    // Internes
    // --------------------------------------------------------------------

    private void ResetMedal(CanvasGroup cg)
    {
        if (cg == null)
            return;

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Transform t = cg.transform;
        t.localScale = Vector3.one;
    }

    private IEnumerator ShowMedalRoutine(CanvasGroup cg)
    {
        if (cg == null)
            yield break;

        Transform t = cg.transform;

        // Part de alpha 0 et scale 1 (au cas où).
        cg.alpha = 0f;
        t.localScale = Vector3.one;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // Fade-in
        float tFade = 0f;
        float durationFade = Mathf.Max(0.01f, fadeDuration);

        while (tFade < durationFade)
        {
            tFade += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tFade / durationFade);
            cg.alpha = k;
            yield return null;
        }

        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // Pulse visuel
        yield return PulseRoutine(t);
    }

    private IEnumerator PulseRoutine(Transform t)
    {
        if (t == null)
            yield break;

        Vector3 baseScale = Vector3.one;
        Vector3 peakScale = baseScale * Mathf.Max(1f, pulseScale);

        float total = Mathf.Max(0.01f, pulseDuration);
        float half = total * 0.5f;

        float timer = 0f;

        // Phase montée
        while (timer < half)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / half);
            float eased = SmoothStep01(k);
            t.localScale = Vector3.Lerp(baseScale, peakScale, eased);
            yield return null;
        }

        t.localScale = peakScale;

        // Phase descente
        timer = 0f;
        while (timer < half)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / half);
            float eased = SmoothStep01(k);
            t.localScale = Vector3.Lerp(peakScale, baseScale, eased);
            yield return null;
        }

        t.localScale = baseScale;
    }

    // Petit helper d'easing (équivalent approximatif de SmoothStep)
    private float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
