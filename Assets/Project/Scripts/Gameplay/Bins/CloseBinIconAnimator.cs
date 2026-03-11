using UnityEngine;
using UnityEngine.UI;

public class CloseBinIconAnimator : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 45f;

    [Header("Pulse")]
    [SerializeField] private float pulseAmplitude = 0.05f;
    [SerializeField] private float pulseSpeed = 2f;

    private Image img;
    private Color baseColor;
    private Vector3 baseScale;

    private void Awake()
    {
        img = GetComponent<Image>();
        baseColor = img.color;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        // Rotation
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // Pulse
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float scale = 1f + t * pulseAmplitude;
        transform.localScale = baseScale * scale;

        // Optionnel : variation d’alpha
        float alpha = Mathf.Lerp(0.7f, 1f, t);
        img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }
}
