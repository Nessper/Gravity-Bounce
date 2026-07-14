using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Presentation-only view for one completed HUD impact session.
/// It owns only the rise, slowdown, fade and destruction of its text.
/// </summary>
public sealed class ScoreSessionTotalUI : MonoBehaviour
{
    private RectTransform rect;
    private CanvasGroup group;
    private float riseDuration;
    private float riseDistance;
    private float floatDuration;
    private float fadeDuration;
    private Action<ScoreSessionTotalUI> onDestroyed;

    public void Play(
        TMP_Text label,
        CanvasGroup canvasGroup,
        float duration,
        float distance,
        float hold,
        float fade,
        Action<ScoreSessionTotalUI> destroyedCallback)
    {
        rect = label != null ? label.rectTransform : transform as RectTransform;
        group = canvasGroup;
        riseDuration = Mathf.Max(0f, duration);
        riseDistance = distance;
        floatDuration = Mathf.Max(0f, hold);
        fadeDuration = Mathf.Max(0f, fade);
        onDestroyed = destroyedCallback;

        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (rect == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (group != null)
            group.alpha = 1f;

        Vector2 startPosition = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;

            float t = riseDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / riseDuration);

            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition =
                startPosition + Vector2.up * riseDistance * eased;

            yield return null;
        }

        rect.anchoredPosition = startPosition + Vector2.up * riseDistance;

        elapsed = 0f;

        while (elapsed < floatDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = fadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / fadeDuration);

            if (group != null)
                group.alpha = 1f - t;

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Action<ScoreSessionTotalUI> callback = onDestroyed;
        onDestroyed = null;
        callback?.Invoke(this);
    }
}
