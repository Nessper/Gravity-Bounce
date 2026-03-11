using UnityEngine;

public class PaddleGuideHint : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private PlayerInputTouch inputSource;
    [SerializeField] private RectTransform pulseRect;      // PaddleGuidePulse
    [SerializeField] private CanvasGroup pulseCanvasGroup; // optionnel pour l'alpha

    [Header("Animation")]
    [SerializeField] private float minScaleX = 0.0f;
    [SerializeField] private float maxScaleX = 1.0f;
    [SerializeField] private float pulseDuration = 1.2f; // aller-retour complet
    [SerializeField] private float maxAlpha = 0.9f;

    private float time;

    private void Start()
    {
        if (pulseRect != null)
        {
            Vector3 s = pulseRect.localScale;
            s.x = minScaleX;
            pulseRect.localScale = s;
        }

        if (pulseCanvasGroup != null)
        {
            pulseCanvasGroup.alpha = 0f;
        }
    }

    private void Update()
    {
        if (inputSource == null || pulseRect == null)
            return;

        // Si le pouce est dans la zone : on cache l'animation
        if (inputSource.HasActivePointer)
        {
            if (pulseCanvasGroup != null)
                pulseCanvasGroup.alpha = 0f;

            Vector3 s = pulseRect.localScale;
            s.x = minScaleX;
            pulseRect.localScale = s;

            // On peut garder le time qui avance, ce n'est pas grave
            return;
        }

        // Sinon : on fait notre pulse (part du centre, remplit, revient)
        time += Time.deltaTime;
        float t = Mathf.PingPong(time / pulseDuration, 1f);

        float scaleX = Mathf.Lerp(minScaleX, maxScaleX, t);
        Vector3 scale = pulseRect.localScale;
        scale.x = scaleX;
        pulseRect.localScale = scale;

        if (pulseCanvasGroup != null)
        {
            pulseCanvasGroup.alpha = Mathf.Lerp(0f, maxAlpha, t);
        }
    }
}
