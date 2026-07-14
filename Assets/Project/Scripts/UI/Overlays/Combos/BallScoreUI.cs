using System.Collections;
using TMPro;
using UnityEngine;

public class BallScoreUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;

    [Header("Motion")]
    [SerializeField] private float damping = 1.6f;

    private RectTransform rect;
    private Vector2 velocity;
    private bool motionEnabled;

    public int Points { get; private set; }
    public Color CurrentColor { get; private set; }

    private void Awake()
    {
        rect = transform as RectTransform;
    }

    private void Update()
    {
        if (rect == null || !motionEnabled)
            return;

        rect.anchoredPosition += velocity * Time.deltaTime;
        velocity = Vector2.Lerp(
            velocity,
            Vector2.zero,
            damping * Time.deltaTime
        );
    }

    public void Play(
        int points,
        Color color,
        Vector2 offset,
        Vector2 initialVelocity)
    {
        string sign = points >= 0 ? "+" : "";

        Points = points;
        CurrentColor = color;

        if (label != null)
        {
            label.text = sign + points;
            label.color = CurrentColor;
        }

        if (group != null)
            group.alpha = 1f;

        if (rect != null)
            rect.anchoredPosition += offset;

        velocity = initialVelocity;
        motionEnabled = true;
    }

    public void StopMotion()
    {
        motionEnabled = false;
        velocity = Vector2.zero;
    }

    public void FadeAndDestroy(float duration)
    {
        StartCoroutine(FadeAndDestroyRoutine(duration));
    }

    private IEnumerator FadeAndDestroyRoutine(float duration)
    {
        float elapsed = 0f;
        float startAlpha = group != null ? group.alpha : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            if (group != null)
                group.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}
