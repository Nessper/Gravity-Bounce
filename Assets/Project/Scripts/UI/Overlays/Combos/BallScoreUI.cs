using System.Collections;
using TMPro;
using UnityEngine;

public class BallScoreUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;

    [Header("Animation")]
    [SerializeField] private float lifetime = 0.35f;
    [SerializeField] private float riseDistance = 18f;

    private RectTransform rect;

    private void Awake()
    {
        rect = transform as RectTransform;
    }

    public void Play(
        int points,
        Color color,
        Vector2 offset)
    {
        if (label != null)
        {
            string sign = points >= 0 ? "+" : "";
            label.text = sign + points;
            label.color = color;
        }

        if (rect != null)
            rect.anchoredPosition += offset;

        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        Vector2 start = rect.anchoredPosition;
        Vector2 end = start + Vector2.up * riseDistance;

        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / lifetime);

            rect.anchoredPosition =
                Vector2.Lerp(start, end, t);

            if (group != null)
                group.alpha = 1f - t;

            yield return null;
        }

        Destroy(gameObject);
    }
}