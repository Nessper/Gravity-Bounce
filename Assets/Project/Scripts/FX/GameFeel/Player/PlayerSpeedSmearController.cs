using UnityEngine;

/// <summary>
/// Effet de vitesse du paddle.
/// 
/// À placer sur Player1.
/// Le script observe le déplacement du playerRoot,
/// puis affiche un smear décalé derrière le sens du mouvement.
/// 
/// Important :
/// - ne scale pas dynamiquement le smear
/// - le smear peut être légèrement plus large à la main dans l'Inspector
/// - seul l'alpha et le décalage X changent selon la vitesse
/// </summary>
public class PlayerSpeedSmearController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform smearTransform;
    [SerializeField] private SpriteRenderer smearRenderer;

    [Header("Alpha")]
    [SerializeField] private float minAlpha = 0f;
    [SerializeField] private float maxAlpha = 0.9f;

    [Header("Back Offset")]
    [SerializeField] private float maxBackOffsetX = 0.22f;

    [Header("Speed")]
    [SerializeField] private float speedForMaxEffect = 10f;
    [SerializeField] private float minSpeedForEffect = 3f;

    [Header("Smoothing")]
    [SerializeField] private float smoothing = 18f;

    private Vector3 lastPosition;
    private Vector3 baseSmearPosition;
    private Vector3 baseSmearScale;

    private float displayedAlpha;
    private float displayedBackOffsetX;
    private float lastDirection;

    private void Start()
    {
        if (playerRoot == null)
            playerRoot = transform;

        if (smearTransform == null && smearRenderer != null)
            smearTransform = smearRenderer.transform;

        if (smearTransform == null)
        {
            Debug.LogWarning("[PlayerSpeedSmearController] smearTransform non assigné.");
            enabled = false;
            return;
        }

        lastPosition = playerRoot.position;
        baseSmearPosition = smearTransform.localPosition;
        baseSmearScale = smearTransform.localScale;

        SetAlpha(0f);
    }

    private void Update()
    {
        float deltaX = playerRoot.position.x - lastPosition.x;
        float speed = Mathf.Abs(deltaX) / Mathf.Max(Time.deltaTime, 0.0001f);
        float speed01 = Mathf.InverseLerp(minSpeedForEffect, speedForMaxEffect, speed);

        if (Mathf.Abs(deltaX) > 0.0001f)
            lastDirection = Mathf.Sign(deltaX);

        float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, speed01);
        float targetBackOffsetX = -lastDirection * maxBackOffsetX * speed01;

        displayedAlpha = Mathf.Lerp(
            displayedAlpha,
            targetAlpha,
            smoothing * Time.deltaTime
        );

        displayedBackOffsetX = Mathf.Lerp(
            displayedBackOffsetX,
            targetBackOffsetX,
            smoothing * Time.deltaTime
        );

        ApplyVisuals();

        lastPosition = playerRoot.position;
    }

    private void ApplyVisuals()
    {
        SetAlpha(displayedAlpha);

        smearTransform.localScale = baseSmearScale;

        Vector3 pos = baseSmearPosition;
        pos.x += displayedBackOffsetX;
        smearTransform.localPosition = pos;
    }

    private void SetAlpha(float alpha)
    {
        if (smearRenderer == null)
            return;

        Color c = smearRenderer.color;
        c.a = alpha;
        smearRenderer.color = c;
    }
}