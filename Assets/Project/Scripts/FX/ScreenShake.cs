using System.Collections;
using UnityEngine;

/// <summary>
/// Applique un tremblement temporaire sur la position locale du Transform.
/// A mettre sur la caméra de gameplay (ou un parent).
/// </summary>
public class ScreenShake : MonoBehaviour
{
    [Header("Paramètres par défaut")]
    [SerializeField] private float defaultDuration = 0.15f;
    [SerializeField] private float defaultAmplitude = 0.15f;
    [SerializeField] private float defaultFrequency = 35f;

    private Vector3 originalLocalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
    }

    /// <summary>
    /// Lance un tremblement avec les paramètres par défaut.
    /// </summary>
    public void Shake()
    {
        Shake(defaultDuration, defaultAmplitude, defaultFrequency);
    }

    /// <summary>
    /// Lance un tremblement avec des paramètres spécifiques.
    /// </summary>
    public void Shake(float duration, float amplitude, float frequency)
    {
        if (duration <= 0f || amplitude <= 0f)
            return;

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            transform.localPosition = originalLocalPosition;
        }

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, amplitude, frequency));
    }

    private IEnumerator ShakeRoutine(float duration, float amplitude, float frequency)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Atténuation progressive (courbe ease-out)
            float damper = 1f - (t * t);

            float time = Time.time * frequency;

            // Perlin noise pour éviter les saccades
            float offsetX = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * amplitude * damper;
            float offsetY = (Mathf.PerlinNoise(0f, time) * 2f - 1f) * amplitude * damper;

            transform.localPosition = originalLocalPosition + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        shakeRoutine = null;
    }
}
