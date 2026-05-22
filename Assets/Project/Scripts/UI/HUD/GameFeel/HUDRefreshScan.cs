using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HUDRefreshScanController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform scanRect;
    [SerializeField] private Image scanImage;

    [Header("Movement")]
    [SerializeField] private float startX = -800f;
    [SerializeField] private float endX = 800f;

    [Header("Speed Variants")]
    [SerializeField] private float fastMoveDuration = 0.18f;
    [SerializeField] private float slowMoveDuration = 0.45f;
    [SerializeField, Range(0f, 1f)] private float fastChance = 0.5f;

    [Header("Timing")]
    [SerializeField] private float delayMin = 2f;
    [SerializeField] private float delayMax = 5f;

    [Header("Visual")]
    [SerializeField, Range(0f, 1f)] private float alpha = 0.015f;

    private Coroutine routine;

    private void OnEnable()
    {
        SetAlpha(0f);
        routine = StartCoroutine(RunRoutine());
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
            float delay = Random.Range(delayMin, delayMax);
            yield return new WaitForSecondsRealtime(delay);

            yield return PlayScan();
        }
    }

    private IEnumerator PlayScan()
    {
        float duration = Random.value < fastChance
            ? fastMoveDuration
            : slowMoveDuration;

        duration = Mathf.Max(0.01f, duration);

        SetAlpha(alpha);

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / duration);

            Vector2 pos = scanRect.anchoredPosition;
            pos.x = Mathf.Lerp(startX, endX, k);
            scanRect.anchoredPosition = pos;

            yield return null;
        }

        SetAlpha(0f);
    }

    private void SetAlpha(float value)
    {
        if (scanImage == null)
            return;

        Color c = scanImage.color;
        c.a = Mathf.Clamp01(value);
        scanImage.color = c;
    }
}