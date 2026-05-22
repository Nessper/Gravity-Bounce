using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HUDGlassLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform glassRect;
    [SerializeField] private Image glassImage;

    [Header("Movement")]
    [SerializeField] private float startX = -800f;
    [SerializeField] private float endX = 800f;

    [Header("Durations")]
    [SerializeField] private float minDuration = 1f;
    [SerializeField] private float maxDuration = 1.8f;

    [Header("Timing")]
    [SerializeField] private float delayMin = 6f;
    [SerializeField] private float delayMax = 12f;

    [Header("Alpha")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.7f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.9f;

    private Coroutine routine;

    private void OnEnable()
    {
        SetAlpha(0f);

        routine =
            StartCoroutine(RunRoutine());
    }

    private void OnDisable()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = null;

        SetAlpha(0f);
    }

    private IEnumerator RunRoutine()
    {
        while (true)
        {
            float delay =
                Random.Range(delayMin, delayMax);

            yield return
                new WaitForSecondsRealtime(delay);

            yield return PlayPass();
        }
    }

    private IEnumerator PlayPass()
    {
        float duration =
            Random.Range(minDuration, maxDuration);

        duration =
            Mathf.Max(0.001f, duration);

        float alpha =
            Random.Range(minAlpha, maxAlpha);

        SetAlpha(alpha);

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float k =
                Mathf.Clamp01(t / duration);

            Vector2 pos =
                glassRect.anchoredPosition;

            pos.x =
                Mathf.Lerp(startX, endX, k);

            glassRect.anchoredPosition = pos;

            yield return null;
        }

        SetAlpha(0f);
    }

    private void SetAlpha(float value)
    {
        if (glassImage == null)
            return;

        Color c = glassImage.color;

        c.a =
            Mathf.Clamp01(value);

        glassImage.color = c;
    }
}