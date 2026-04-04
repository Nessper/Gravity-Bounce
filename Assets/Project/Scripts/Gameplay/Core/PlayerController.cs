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
    [SerializeField] private PlayerFlashFeedback flashFeedback;

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

        flashFeedback?.TriggerFlash();

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

        // --------------------------------------------------
        // 1) POSITION D'IMPACT SUR LE PADDLE
        // --------------------------------------------------

        float hitOffsetX = contact.point.x - boxCollider.bounds.center.x;
        float normalizedX = Mathf.Clamp(hitOffsetX / halfWidth, -1f, 1f);

        // On garde un collider large pour le confort,
        // mais on limite l'influence des extrémités.
        normalizedX = Mathf.Clamp(normalizedX, -maxInfluence, maxInfluence);
        normalizedX /= maxInfluence;

        // --------------------------------------------------
        // 2) ZONE CENTRALE + COURBE DE CONTRÔLE
        // --------------------------------------------------

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

        // Influence "volontaire" du paddle :
        // douce au centre, plus forte sur les bords.
        float paddleX = Mathf.Sign(normalizedX) * Mathf.Pow(Mathf.Abs(normalizedX), bounceCurvePower);

        // --------------------------------------------------
        // 3) DIRECTION D'ARRIVÉE DE LA BALLE
        // --------------------------------------------------

        Vector3 incomingVel = ballRb.linearVelocity;
        float incomingSpeed = incomingVel.magnitude;
        if (incomingSpeed < 0.01f)
            incomingSpeed = 0.01f;

        Vector3 incomingDir = incomingVel.normalized;

        // Composante "naturelle" :
        // au centre, on garde une partie de la tendance horizontale
        // de la balle entrante pour un ressenti plus intuitif.
        float naturalX = Mathf.Clamp(incomingDir.x, -0.75f, 0.75f);

        // --------------------------------------------------
        // 4) MÉLANGE NATUREL / CONTRÔLE JOUEUR
        // --------------------------------------------------

        // Centre = plus naturel
        // Bords = plus de contrôle paddle
        float edgeWeight = Mathf.SmoothStep(0f, 1f, absX);
        float naturalWeight = 1f - edgeWeight;

        float finalX = (naturalX * naturalWeight) + (paddleX * edgeWeight);

        // Sécurité : on évite les angles trop plats / trop extrêmes.
        finalX = Mathf.Clamp(finalX, -0.95f, 0.95f);

        // --------------------------------------------------
        // 5) DIRECTION FINALE DE REBOND
        // --------------------------------------------------

        Vector3 bounceDir = new Vector3(finalX, 1f, 0f).normalized;

        // Toujours repartir franchement vers le haut.
        if (bounceDir.y < 0.05f)
        {
            bounceDir.y = 0.05f;
            bounceDir.Normalize();
        }

        // --------------------------------------------------
        // 6) IMPULSION DE RENVOI DU PADDLE
        // --------------------------------------------------

        // Logique multiplicative, proche du rebond physique d'avant :
        // une balle rapide repart fort, une balle lente repart plus calmement.
        float outgoingSpeed = incomingSpeed * bounceSpeedMultiplier;

        // Plancher dynamique :
        // fort au centre pour éviter les billes collées,
        // faible sur les bords pour préserver les coups précis.
        float currentMinEffectiveSpeed = Mathf.Lerp(centerSpeedFloor, edgeSpeedFloor, absX);

        if (outgoingSpeed < currentMinEffectiveSpeed)
            outgoingSpeed = currentMinEffectiveSpeed;

        ballRb.linearVelocity = bounceDir * outgoingSpeed;
    }
}