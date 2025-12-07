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
}
