using UnityEngine;

public class CloseBinIconAnimator : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 45f; // degrés/seconde

    [Header("Pulse")]
    [SerializeField] private float pulseAmplitude = 0.05f; // 5% du scale
    [SerializeField] private float pulseSpeed = 2f;

    private SpriteRenderer sr;
    private Color baseColor;
    private Vector3 baseScale;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        // --- ROTATION ---
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // --- PULSE ---
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float scale = 1f + t * pulseAmplitude;
        transform.localScale = baseScale * scale;

        // Optionnel : légère variation alpha
        float alpha = Mathf.Lerp(0.7f, 1f, t);
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }
}
