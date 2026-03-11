using System.Collections;
using UnityEngine;

/// <summary>
/// Centralise les feedbacks visuels liés aux dégâts de Hull :
/// - tremblement de caméra,
/// - flash rouge plein écran,
/// - séquence "Hull détruit" (plus longue).
/// </summary>
public class HullDamageFeedbackController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScreenShake screenShake;
    [SerializeField] private DamageFlashUI damageFlash;

    // ============================================================
    // DÉGÂTS (flush de noires)
    // ============================================================

    [Header("Intensité (dégâts)")]
    [SerializeField] private float baseShakeAmplitude = 0.12f;
    [SerializeField] private float maxShakeAmplitude = 0.25f;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeFrequency = 35f;
    [SerializeField] private int maxBlackCountForIntensity = 3;

    // ============================================================
    // HULL DÉTRUIT (GameOver)
    // ============================================================

    [Header("Hull détruit (GameOver) - Timing")]
    [Tooltip("Freeze initial (unscaled) pour marquer le choc.")]
    [SerializeField] private float destroyedFreezeSec = 0.28f;

    [Tooltip("Pause (unscaled) juste après le freeze, avant de lancer le gros shake. Donne un 'vide' dramatique.")]
    [SerializeField] private float destroyedPreImpactPauseSec = 0.08f;

    [Tooltip("Durée du shake principal (unscaled).")]
    [SerializeField] private float destroyedImpactShakeDuration = 0.85f;

    [Tooltip("Après-shock (unscaled) : shake secondaire plus lent, plus lourd.")]
    [SerializeField] private float destroyedAftershockDuration = 1.05f;

    [Tooltip("Pause finale (unscaled) sans FX : le cerveau comprend que c'est terminé.")]
    [SerializeField] private float destroyedSilencePauseSec = 0.55f;

    [Header("Hull détruit (GameOver) - Shake")]
    [SerializeField] private float destroyedImpactShakeAmplitude = 0.38f;
    [SerializeField] private float destroyedImpactShakeFrequency = 20f;

    [SerializeField] private float destroyedAftershockAmplitudeMultiplier = 0.35f;
    [SerializeField] private float destroyedAftershockFrequencyMultiplier = 0.55f;

    [Header("Hull détruit (GameOver) - Flash")]
    [Tooltip("Intensité du flash principal (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float destroyedImpactFlashIntensity = 1f;

    [Tooltip("Petit flash secondaire (0..1) au début de l'aftershock. Optionnel.")]
    [Range(0f, 1f)]
    [SerializeField] private float destroyedAftershockFlashIntensity = 0.25f;

    [Tooltip("Active le flash secondaire.")]
    [SerializeField] private bool playAftershockFlash = true;

    [Header("Hull détruit (GameOver) - Post")]
    [Tooltip("Pause finale avant affichage du panel GameOver.")]
    [SerializeField] private float destroyedPostDelaySec = 3.0f;


    [Header("Hull détruit (GameOver) - Option")]
    [Tooltip("Si true, on empêche les feedbacks dégâts normaux pendant la séquence Hull détruit.")]
    [SerializeField] private bool lockDamageFeedbackDuringDestroyed = true;

    private Coroutine runningDestroyedRoutine;
    private bool destroyedRunning;

    // ============================================================
    // DÉGÂTS
    // ============================================================

    public void PlayHullDamageFeedback(int blackCount)
    {
        if (blackCount <= 0)
            return;

        if (lockDamageFeedbackDuringDestroyed && destroyedRunning)
            return;

        float intensity = 1f;
        if (maxBlackCountForIntensity > 0)
            intensity = Mathf.Clamp01(blackCount / (float)maxBlackCountForIntensity);

        if (screenShake != null)
        {
            float amp = Mathf.Lerp(baseShakeAmplitude, maxShakeAmplitude, intensity);
            screenShake.Shake(shakeDuration, amp, shakeFrequency);
        }

        if (damageFlash != null)
        {
            damageFlash.PlayFlash(intensity);
        }
    }

    // ============================================================
    // HULL DÉTRUIT
    // ============================================================

    /// <summary>
    /// Séquence plus longue pour un GameOver "Hull détruit".
    /// Appelle onComplete à la fin (ex: déclencher le panel final).
    /// </summary>
    public void PlayHullDestroyedFeedback(System.Action onComplete)
    {
        if (runningDestroyedRoutine != null)
            StopCoroutine(runningDestroyedRoutine);

        runningDestroyedRoutine = StartCoroutine(HullDestroyedRoutine(onComplete));
    }

    private IEnumerator HullDestroyedRoutine(System.Action onComplete)
    {
        destroyedRunning = true;

        // --------------------------------------------------
        // 1) FREEZE (choc)
        // --------------------------------------------------
        if (destroyedFreezeSec > 0f)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(destroyedFreezeSec);
            Time.timeScale = 1f;
        }

        if (destroyedPreImpactPauseSec > 0f)
            yield return new WaitForSecondsRealtime(destroyedPreImpactPauseSec);

        // --------------------------------------------------
        // 2) IMPACT (shake principal + flash fort)
        // --------------------------------------------------
        if (screenShake != null && destroyedImpactShakeDuration > 0f)
        {
            screenShake.Shake(
                destroyedImpactShakeDuration,
                destroyedImpactShakeAmplitude,
                destroyedImpactShakeFrequency
            );
        }

        if (damageFlash != null && destroyedImpactFlashIntensity > 0f)
        {
            damageFlash.PlayFlash(Mathf.Clamp01(destroyedImpactFlashIntensity));
        }

        if (destroyedImpactShakeDuration > 0f)
            yield return new WaitForSecondsRealtime(destroyedImpactShakeDuration);

        // --------------------------------------------------
        // 3) AFTERSHOCK (shake secondaire plus lent + option flash léger)
        // --------------------------------------------------
        if (screenShake != null && destroyedAftershockDuration > 0f)
        {
            float amp = destroyedImpactShakeAmplitude * Mathf.Max(0f, destroyedAftershockAmplitudeMultiplier);
            float freq = destroyedImpactShakeFrequency * Mathf.Max(0f, destroyedAftershockFrequencyMultiplier);

            screenShake.Shake(destroyedAftershockDuration, amp, freq);
        }

        if (playAftershockFlash && damageFlash != null && destroyedAftershockFlashIntensity > 0f)
        {
            damageFlash.PlayFlash(Mathf.Clamp01(destroyedAftershockFlashIntensity));
        }

        if (destroyedAftershockDuration > 0f)
            yield return new WaitForSecondsRealtime(destroyedAftershockDuration);

        // --------------------------------------------------
        // 4) SILENCE (pause dramatique sans FX)
        // --------------------------------------------------
        if (destroyedSilencePauseSec > 0f)
            yield return new WaitForSecondsRealtime(destroyedSilencePauseSec);

        destroyedRunning = false;
        runningDestroyedRoutine = null;

        // --------------------------------------------------
        // 5) PAUSE AVANT PANEL FINAL
        // --------------------------------------------------
        if (destroyedPostDelaySec > 0f)
            yield return new WaitForSecondsRealtime(destroyedPostDelaySec);

        destroyedRunning = false;
        runningDestroyedRoutine = null;

        onComplete?.Invoke();
    }
}
