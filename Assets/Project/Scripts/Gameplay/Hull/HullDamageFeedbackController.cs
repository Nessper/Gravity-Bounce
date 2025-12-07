using UnityEngine;

/// <summary>
/// Centralise les feedbacks visuels liés aux dégâts de Hull :
/// - tremblement de caméra,
/// - flash rouge plein écran.
/// </summary>
public class HullDamageFeedbackController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScreenShake screenShake;
    [SerializeField] private DamageFlashUI damageFlash;

    [Header("Intensité")]
    [Tooltip("Amplitude de shake pour une seule bille noire.")]
    [SerializeField] private float baseShakeAmplitude = 0.12f;

    [Tooltip("Amplitude max du shake, même avec beaucoup de billes noires.")]
    [SerializeField] private float maxShakeAmplitude = 0.25f;

    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeFrequency = 35f;

    [Tooltip("Nombre de billes noires à partir duquel on considère l'intensité max (pour le flash et le shake).")]
    [SerializeField] private int maxBlackCountForIntensity = 3;

    /// <summary>
    /// Appelé par HullSystem quand on prend des dégâts de coque.
    /// blackCount = nombre de billes noires dans le flush.
    /// </summary>
    public void PlayHullDamageFeedback(int blackCount)
    {
        if (blackCount <= 0)
            return;

        // Intensité 0..1 en fonction du nombre de billes noires
        float intensity = 1f;
        if (maxBlackCountForIntensity > 0)
        {
            intensity = Mathf.Clamp01(blackCount / (float)maxBlackCountForIntensity);
        }

        // Tremblement de caméra
        if (screenShake != null)
        {
            float amp = Mathf.Lerp(baseShakeAmplitude, maxShakeAmplitude, intensity);
            screenShake.Shake(shakeDuration, amp, shakeFrequency);
        }

        // Flash rouge
        if (damageFlash != null)
        {
            damageFlash.PlayFlash(intensity);
        }
    }
}
