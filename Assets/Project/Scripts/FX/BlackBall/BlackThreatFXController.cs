using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pilote les effets visuels de menace noire
/// a partir du nombre de billes noires actives.
///
/// V1 :
/// - vignette
/// - desaturation
/// - chromatic aberration
/// </summary>
public class BlackThreatFXController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BlackThreatTracker tracker;
    [SerializeField] private Volume volume;

    [Header("Threat Mapping")]
    [SerializeField] private int maxBlackForFullThreat = 4;

    [Header("Vignette")]
    [SerializeField] private float vignetteMaxIntensity = 0.32f;

    [Header("Saturation")]
    [SerializeField] private float saturationAtMaxThreat = -35f;

    [Header("Chromatic Aberration")]
    [SerializeField] private float chromaticAtMaxThreat = 0.18f;

    [Header("Smoothing")]
    [SerializeField] private float lerpSpeed = 4f;

    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private ChromaticAberration chromatic;

    private float currentThreat01;

    private void Awake()
    {
        if (volume == null)
        {
            Debug.LogWarning("[BlackThreatFXController] Volume manquant.");
            enabled = false;
            return;
        }

        if (volume.profile == null)
        {
            Debug.LogWarning("[BlackThreatFXController] VolumeProfile manquant.");
            enabled = false;
            return;
        }

        volume.profile.TryGet(out vignette);
        volume.profile.TryGet(out colorAdjustments);
        volume.profile.TryGet(out chromatic);
    }

    private void Update()
    {
        int blackCount = 0;

        if (tracker != null)
            blackCount = tracker.ActiveBlackCount;

        float targetThreat01 = 0f;

        if (maxBlackForFullThreat > 0)
        {
            targetThreat01 =
                Mathf.Clamp01((float)blackCount / maxBlackForFullThreat);
        }

        currentThreat01 = Mathf.Lerp(
            currentThreat01,
            targetThreat01,
            Time.deltaTime * lerpSpeed);

        ApplyEffects(currentThreat01);
    }

    private void ApplyEffects(float t)
    {
        if (vignette != null)
        {
            vignette.intensity.value =
                Mathf.Lerp(0f, vignetteMaxIntensity, t);
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value =
                Mathf.Lerp(0f, saturationAtMaxThreat, t);
        }

        if (chromatic != null)
        {
            chromatic.intensity.value =
                Mathf.Lerp(0f, chromaticAtMaxThreat, t);
        }
    }
}