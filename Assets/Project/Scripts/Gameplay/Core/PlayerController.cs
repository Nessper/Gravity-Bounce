using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Limites de déplacement en X")]
    [SerializeField] private float xRange = 1.7f;

    [Header("Etat du contrôle")]
    [SerializeField] private bool canControl = true;

    [Header("Mode de contrôle")]
    [SerializeField] private bool useMouseDelta = true;
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Header("Feedback visuel")]
    [SerializeField] private PlayerImpactMotionController impactMotion;
    [SerializeField] private PlayerShaderFlashFeedback shaderFlashFeedback;

    [Header("Rebond custom")]
    [SerializeField] private bool useCustomBounce = true;

    [Tooltip("Multiplicateur léger de vitesse au rebond du paddle.")]
    [SerializeField] private float bounceSpeedMultiplier = 1.04f;

    [Tooltip("Angle max de déviation par rapport à la verticale.")]
    [SerializeField] private float maxBounceAngleDeg = 55f;

    [Tooltip("Largeur centrale qui renvoie presque droit.")]
    [SerializeField] private float centerDeadZone = 0.12f;

    [Tooltip("Limite l'influence des impacts aux extrémités.")]
    [SerializeField] private float maxInfluence = 0.85f;

    [Tooltip("Courbe de réponse : 1 = linéaire, >1 = plus doux au centre.")]
    [SerializeField] private float bounceCurvePower = 1.35f;

    [Header("Plancher de relance dynamique")]
    [Tooltip("Plancher de vitesse au centre du paddle, pour éviter les billes collées.")]
    [SerializeField] private float centerSpeedFloor = 4f;

    [Tooltip("Plancher de vitesse sur les bords du paddle, pour préserver les coups précis.")]
    [SerializeField] private float edgeSpeedFloor = 0f;

    public event Action<Collision> OnBallCollision;

    private Rigidbody playerRb;
    private BoxCollider boxCollider;
    private float targetX;

    public float XRange => xRange;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        if (playerRb != null)
        {
            playerRb.isKinematic = true;
            playerRb.interpolation = RigidbodyInterpolation.None;
        }

        targetX = transform.position.x;
    }

    private void Update()
    {
        if (!canControl)
            return;

        Vector3 currentPos = transform.position;
        float nextX;

        if (useMouseDelta)
        {
            float mouseDelta = Input.GetAxis("Mouse X");
            nextX = currentPos.x + mouseDelta * mouseSensitivity;
        }
        else
        {
            nextX = targetX;
        }

        nextX = Mathf.Clamp(nextX, -xRange, xRange);
        transform.position = new Vector3(nextX, currentPos.y, currentPos.z);
    }

    public void SetTargetXWorld(float worldX)
    {
        targetX = Mathf.Clamp(worldX, -xRange, xRange);
    }

    public void SetActiveControl(bool state)
    {
        canControl = state;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Ball"))
            return;

        impactMotion?.TriggerImpact();
        shaderFlashFeedback?.TriggerFlash();

        if (useCustomBounce)
            ApplyCustomBounce(collision);

        OnBallCollision?.Invoke(collision);
    }

    private void ApplyCustomBounce(Collision collision)
    {
        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null)
            return;

        ContactPoint contact = collision.contacts[0];

        float halfWidth = boxCollider.bounds.extents.x;
        if (halfWidth <= 0.0001f)
            return;

        float hitOffsetX = contact.point.x - boxCollider.bounds.center.x;
        float normalizedX = Mathf.Clamp(hitOffsetX / halfWidth, -1f, 1f);

        normalizedX = Mathf.Clamp(normalizedX, -maxInfluence, maxInfluence);
        normalizedX /= maxInfluence;

        float absX = Mathf.Abs(normalizedX);

        if (absX < centerDeadZone)
        {
            normalizedX = 0f;
            absX = 0f;
        }
        else
        {
            float sign = Mathf.Sign(normalizedX);
            absX = Mathf.InverseLerp(centerDeadZone, 1f, absX);
            normalizedX = sign * absX;
        }

        float paddleX = Mathf.Sign(normalizedX) * Mathf.Pow(Mathf.Abs(normalizedX), bounceCurvePower);

        Vector3 incomingVel = ballRb.linearVelocity;
        float incomingSpeed = incomingVel.magnitude;

        if (incomingSpeed < 0.01f)
            incomingSpeed = 0.01f;

        Vector3 incomingDir = incomingVel.normalized;

        float naturalX = Mathf.Clamp(incomingDir.x, -0.75f, 0.75f);

        float centerBias = 0.35f;
        float centerInfluence = 1f - absX;

        naturalX += Mathf.Sign(naturalX) * centerBias * centerInfluence;
        naturalX = Mathf.Clamp(naturalX, -0.95f, 0.95f);

        float edgeWeight = Mathf.SmoothStep(0f, 1f, absX);
        float naturalWeight = 1f - edgeWeight;

        float finalX = (naturalX * naturalWeight) + (paddleX * edgeWeight);
        finalX = Mathf.Clamp(finalX, -0.95f, 0.95f);

        Vector3 bounceDir = new Vector3(finalX, 1f, 0f).normalized;

        if (bounceDir.y < 0.05f)
        {
            bounceDir.y = 0.05f;
            bounceDir.Normalize();
        }

        float outgoingSpeed = incomingSpeed * bounceSpeedMultiplier;

        float currentMinEffectiveSpeed = Mathf.Lerp(centerSpeedFloor, edgeSpeedFloor, absX);

        if (outgoingSpeed < currentMinEffectiveSpeed)
            outgoingSpeed = currentMinEffectiveSpeed;

        ballRb.linearVelocity = bounceDir * outgoingSpeed;
    }
}