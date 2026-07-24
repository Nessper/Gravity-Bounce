using System.Collections;
using UnityEngine;

public class DamageFlashUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timings")]
    [SerializeField] private float fadeInDuration = 0.05f;
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Header("Intensité")]
    [SerializeField] private float maxAlpha = 0.6f;

    [Header("Alerte continue")]
    [SerializeField] private float pulseHoldDuration = 0.10f;

    private Coroutine flashRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

       
    }

    public void PlayFlash(float intensity = 1f)
    {
       

        if (canvasGroup == null)
        {
            
            return;
        }

        intensity = Mathf.Clamp01(intensity);

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(intensity));
    }

    public void StartPulse(float intensity = 1f)
    {
        if (canvasGroup == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(PulseRoutine(Mathf.Clamp01(intensity)));
    }

    public void StopPulse()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private IEnumerator FlashRoutine(float intensity)
    {
        float targetAlpha = maxAlpha * intensity;
       

        // Fade in
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeInDuration);
            canvasGroup.alpha = Mathf.Lerp(0f, targetAlpha, k);
            yield return null;
        }

        // Fade out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeOutDuration);
            canvasGroup.alpha = Mathf.Lerp(targetAlpha, 0f, k);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        flashRoutine = null;
    }

    private IEnumerator PulseRoutine(float intensity)
    {
        float targetAlpha = maxAlpha * intensity;

        while (true)
        {
            yield return FadeTo(targetAlpha, fadeInDuration);

            if (pulseHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(pulseHoldDuration);

            yield return FadeTo(0f, fadeOutDuration);
        }
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
