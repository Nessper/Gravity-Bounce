using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class LogoPulse : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float pulseSpeed = 1.5f;  // vitesse de la pulsation
    [SerializeField] private float scaleAmplitude = 0.01f; // intensité du zoom
    [SerializeField] private float alphaAmplitude = 0.5f; // intensité de la variation d’opacité

    private CanvasGroup canvasGroup;
    private Vector3 baseScale;
    private float baseAlpha;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        baseScale = transform.localScale;
        baseAlpha = canvasGroup.alpha;
    }

    private void Update()
    {
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;

        // Variation de scale
        float scale = 1f + (pulse - 0.5f) * 2f * scaleAmplitude;
        transform.localScale = baseScale * scale;

        // Variation d’alpha
        canvasGroup.alpha = baseAlpha * (1f - alphaAmplitude + pulse * alphaAmplitude);
    }
}
