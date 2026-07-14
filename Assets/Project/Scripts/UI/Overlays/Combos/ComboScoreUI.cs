using System.Collections;
using TMPro;
using UnityEngine;

public class ComboScoreUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private CanvasGroup group;

    [Header("Motion")]
    [SerializeField] private float damping = 1.25f;

    [Header("Label Fade")]
    [SerializeField] private float labelFadeDuration = 0.18f;

    private RectTransform rect;
    private Vector2 velocity;
    private bool labelFaded;
    private bool motionEnabled;

    public int Points { get; private set; }
    public Color CurrentScoreColor { get; private set; }
    public float LabelFadeDuration => labelFadeDuration;

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
        string comboName,
        int points,
        Color color,
        Vector2 offset,
        Vector2 initialVelocity)
    {
        string sign = points >= 0 ? "+" : "";

        Points = points;
        CurrentScoreColor = color;

        if (labelText != null)
        {
            labelText.text = comboName;
            labelText.color = color;
        }

        if (scoreText != null)
        {
            scoreText.text = sign + points;
            scoreText.color = CurrentScoreColor;
        }

        if (group != null)
            group.alpha = 1f;

        SetLabelAlpha(1f);
        SetScoreAlpha(1f);

        labelFaded = false;

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

    public void FadeLabelOnly()
    {
        if (labelFaded)
            return;

        StartCoroutine(FadeLabelOnlyRoutine());
    }

    public void HideLabelForAttraction()
    {
        labelFaded = true;
        SetLabelAlpha(0f);
        SetScoreAlpha(1f);
    }

    public void FadeAndDestroy(float duration)
    {
        StartCoroutine(FadeAndDestroyRoutine(duration));
    }

    private IEnumerator FadeLabelOnlyRoutine()
    {
        labelFaded = true;

        float elapsed = 0f;

        while (elapsed < labelFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / labelFadeDuration);
            SetLabelAlpha(1f - t);

            yield return null;
        }

        SetLabelAlpha(0f);
        SetScoreAlpha(1f);
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

    private void SetLabelAlpha(float alpha)
    {
        if (labelText == null)
            return;

        Color c = labelText.color;
        c.a = alpha;
        labelText.color = c;
    }

    private void SetScoreAlpha(float alpha)
    {
        if (scoreText == null)
            return;

        Color c = scoreText.color;
        c.a = alpha;
        scoreText.color = c;
    }
}
