using System;
using UnityEngine;

public class ObstacleHitEmitter : MonoBehaviour
{
    public static event Action<ObstacleHitInfo> OnObstacleHit;

    [Header("Détection")]
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private bool requireBallStateComponent = true;

    [Header("Filtrage")]
    [SerializeField] private float minImpactSpeed = 0.2f;
    [SerializeField] private float cooldownSec = 0.05f;

    [Header("Références")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject chargedVisual;

    private float lastHitTime = -999f;

    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public GameObject ChargedVisual => chargedVisual;

    private void Reset()
    {
        if (transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    private void Awake()
    {
        if (chargedVisual != null)
            chargedVisual.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsBallCollision(collision.collider))
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minImpactSpeed)
            return;

        float now = Time.unscaledTime;

        if (cooldownSec > 0f && now - lastHitTime < cooldownSec)
            return;

        lastHitTime = now;

        ContactPoint contact = collision.contactCount > 0
            ? collision.GetContact(0)
            : default;

        Vector3 hitPoint = collision.contactCount > 0
            ? contact.point
            : transform.position;

        Vector3 direction = (transform.position - hitPoint).normalized;

        if (direction.sqrMagnitude <= 0.001f)
            direction = -collision.relativeVelocity.normalized;

        OnObstacleHit?.Invoke(new ObstacleHitInfo(
            this,
            VisualRoot,
            ChargedVisual,
            hitPoint,
            direction,
            impactSpeed
        ));
    }

    private bool IsBallCollision(Collider other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrEmpty(ballTag) && !other.CompareTag(ballTag))
            return false;

        if (requireBallStateComponent && !other.TryGetComponent(out BallState _))
            return false;

        return true;
    }
}

public readonly struct ObstacleHitInfo
{
    public readonly ObstacleHitEmitter Emitter;
    public readonly Transform VisualRoot;
    public readonly GameObject ChargedVisual;
    public readonly Vector3 HitPoint;
    public readonly Vector3 Direction;
    public readonly float ImpactSpeed;

    public ObstacleHitInfo(
        ObstacleHitEmitter emitter,
        Transform visualRoot,
        GameObject chargedVisual,
        Vector3 hitPoint,
        Vector3 direction,
        float impactSpeed)
    {
        Emitter = emitter;
        VisualRoot = visualRoot;
        ChargedVisual = chargedVisual;
        HitPoint = hitPoint;
        Direction = direction;
        ImpactSpeed = impactSpeed;
    }
}