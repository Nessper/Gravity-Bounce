using UnityEngine;

/// <summary>
/// Positionne le point d'origine du trail derrière la bille,
/// opposé à la direction de sa vitesse.
/// </summary>
public class TrailLookback : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Transform qui porte le TrailRenderer (ex: child 'TrailOrigin').")]
    [SerializeField] private Transform trailOrigin;

    [Tooltip("Rigidbody de la bille.")]
    [SerializeField] private Rigidbody rb;

    [Header("Réglages")]
    [Tooltip("Distance depuis le centre de la bille jusqu'au bord (rayon visuel).")]
    [SerializeField] private float radiusOffset = 0.1f;

    [Tooltip("Vitesse minimale avant de commencer à orienter le trail.")]
    [SerializeField] private float minSpeed = 0.05f;

    private Vector3 defaultLocalOffset;

    private void Awake()
    {
        if (trailOrigin == null)
        {
            // Si non assigné, on essaie de trouver un child nommé "TrailOrigin"
            Transform t = transform.Find("TrailOrigin");
            if (t != null)
            {
                trailOrigin = t;
            }
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (trailOrigin != null)
        {
            defaultLocalOffset = trailOrigin.localPosition;
        }
    }

    private void LateUpdate()
    {
        if (trailOrigin == null || rb == null)
            return;

        Vector3 v = rb.linearVelocity;

        // Si la bille est quasi immobile, on garde un offset par défaut
        if (v.sqrMagnitude < minSpeed * minSpeed)
        {
            trailOrigin.localPosition = defaultLocalOffset;
            return;
        }

        // Direction de la vitesse en monde
        Vector3 dirWorld = v.normalized;

        // On place le trail derrière la bille en monde
        Vector3 targetWorldPos = transform.position - dirWorld * radiusOffset;

        // Et on convertit en coordonnées locales du BallNode
        trailOrigin.localPosition = transform.InverseTransformPoint(targetWorldPos);
    }
}
