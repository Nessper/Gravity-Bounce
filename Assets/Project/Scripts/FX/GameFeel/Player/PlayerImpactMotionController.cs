using UnityEngine;

/// <summary>
/// Donne une sensation physique au paddle lors des impacts.
/// 
/// Effets :
/// - squash/stretch
/// - enfoncement vertical amorti
/// - retour plus organique via SmoothDamp
///
/// À placer sur PlayerRoot.
/// visualRoot doit être un enfant visuel, pas le GameObject avec Rigidbody/Collider.
/// </summary>
public class PlayerImpactMotionController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform visualRoot;

    [Header("Squash")]
    [SerializeField] private float impactScaleX = 0.08f;
    [SerializeField] private float impactScaleY = 0.13f;
    [SerializeField] private float maxStretchX = 0.16f;
    [SerializeField] private float maxSquashY = 0.22f;

    [Header("Vertical Absorption")]
    [SerializeField] private float impactOffsetY = 0.18f;
    [SerializeField] private float maxOffsetY = 0.35f;
    [SerializeField] private float positionSmoothTime = 0.12f;

    [Header("Scale Return")]
    [SerializeField] private float scaleReturnSpeed = 22f;

    private Vector3 baseScale;
    private Vector3 basePosition;

    private float currentStretchX;
    private float currentSquashY;

    private float targetOffsetY;
    private float displayedOffsetY;
    private float offsetVelocityY;

    private void Awake()
    {
        if (visualRoot == null)
        {
            Debug.LogWarning("[PlayerImpactMotionController] visualRoot non assigné.");
            enabled = false;
            return;
        }

        baseScale = visualRoot.localScale;
        basePosition = visualRoot.localPosition;
    }

    private void Update()
    {
        UpdateScale();
        UpdatePosition();
    }

    private void UpdateScale()
    {
        currentStretchX = Mathf.Lerp(currentStretchX, 0f, scaleReturnSpeed * Time.deltaTime);
        currentSquashY = Mathf.Lerp(currentSquashY, 0f, scaleReturnSpeed * Time.deltaTime);

        Vector3 scale = baseScale;
        scale.x *= 1f + currentStretchX;
        scale.y *= 1f - currentSquashY;

        visualRoot.localScale = scale;
    }

    private void UpdatePosition()
    {
        targetOffsetY = Mathf.Lerp(targetOffsetY, 0f, 14f * Time.deltaTime);

        displayedOffsetY = Mathf.SmoothDamp(
            displayedOffsetY,
            targetOffsetY,
            ref offsetVelocityY,
            positionSmoothTime
        );

        Vector3 pos = basePosition;
        pos.y -= displayedOffsetY;
        visualRoot.localPosition = pos;
    }

    public void TriggerImpact(float strength01 = 1f)
    {
        strength01 = Mathf.Clamp01(strength01);

        currentStretchX += impactScaleX * strength01;
        currentSquashY += impactScaleY * strength01;

        currentStretchX = Mathf.Clamp(currentStretchX, 0f, maxStretchX);
        currentSquashY = Mathf.Clamp(currentSquashY, 0f, maxSquashY);

        targetOffsetY += impactOffsetY * strength01;
        targetOffsetY = Mathf.Clamp(targetOffsetY, 0f, maxOffsetY);
    }
}