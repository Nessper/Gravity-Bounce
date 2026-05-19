using System.Collections;
using UnityEngine;

/// <summary>
/// Cree un leger afterimage du ship quand son mouvement est assez rapide.
///
/// A placer sur GameFeelRoot.
///
/// Important :
/// - observe un Transform cible
/// - ne controle pas le mouvement
/// - clone uniquement un SpriteRenderer source
/// - place le ghost derriere la direction du mouvement
/// - peut filtrer la direction pour eviter les ghosts au freinage
/// </summary>
public class ShipMotionAfterimageController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform observedRoot;
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Header("Trigger")]
    [SerializeField] private float velocityThreshold = 0.35f;
    [SerializeField] private float spawnCooldown = 0.05f;

    [Header("Direction Filter")]
    [SerializeField] private bool useDirectionFilter = true;
    [SerializeField] private Vector2 allowedDirection = Vector2.up;
    [Range(-1f, 1f)]
    [SerializeField] private float minDirectionDot = 0.35f;

    [Header("Afterimage")]
    [SerializeField] private Color ghostColor = new Color(0f, 0.9f, 1f, 0.18f);
    [SerializeField] private float lifetime = 0.12f;
    [SerializeField] private float scaleMultiplier = 1.01f;
    [SerializeField] private int sortingOrderOffset = -1;

    [Header("Placement")]
    [SerializeField] private bool placeBehindMovement = true;
    [SerializeField] private bool invertTrailDirection = false;
    [SerializeField] private float trailDistance = 0.08f;

    private Vector3 lastPosition;
    private Vector3 lastVelocityDirection;

    private float lastSpawnTime = -999f;

    private void Awake()
    {
        if (observedRoot == null || sourceRenderer == null)
        {
            Debug.LogWarning("[ShipMotionAfterimageController] References manquantes.");
            enabled = false;
            return;
        }

        lastPosition = observedRoot.position;
        lastVelocityDirection = Vector3.zero;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 currentPosition = observedRoot.position;
        Vector3 delta = currentPosition - lastPosition;

        float velocity = delta.magnitude / dt;

        if (delta.sqrMagnitude > 0.000001f)
            lastVelocityDirection = delta.normalized;

        bool directionAllowed = IsDirectionAllowed(delta);

        if (velocity >= velocityThreshold &&
            directionAllowed &&
            Time.time >= lastSpawnTime + spawnCooldown)
        {
            SpawnAfterimage();
            lastSpawnTime = Time.time;
        }

        lastPosition = currentPosition;
    }

    private bool IsDirectionAllowed(Vector3 delta)
    {
        if (!useDirectionFilter)
            return true;

        Vector2 delta2D = new Vector2(delta.x, delta.y);

        if (delta2D.sqrMagnitude <= 0.000001f)
            return false;

        Vector2 allowed = allowedDirection;

        if (allowed.sqrMagnitude <= 0.000001f)
            allowed = Vector2.up;

        float dot = Vector2.Dot(
            delta2D.normalized,
            allowed.normalized
        );

        return dot >= minDirectionDot;
    }

    private void SpawnAfterimage()
    {
        GameObject ghost = new GameObject("Ship_Afterimage");

        Vector3 offset = Vector3.zero;

        if (placeBehindMovement &&
            lastVelocityDirection.sqrMagnitude > 0.000001f)
        {
            Vector3 direction =
                invertTrailDirection
                ? lastVelocityDirection
                : -lastVelocityDirection;

            offset = direction * Mathf.Abs(trailDistance);
        }

        ghost.transform.position =
            sourceRenderer.transform.position + offset;

        ghost.transform.rotation =
            sourceRenderer.transform.rotation;

        ghost.transform.localScale =
            sourceRenderer.transform.lossyScale * scaleMultiplier;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();

        sr.sprite = sourceRenderer.sprite;
        sr.flipX = sourceRenderer.flipX;
        sr.flipY = sourceRenderer.flipY;

        sr.color = ghostColor;

        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        sr.sortingOrder =
            sourceRenderer.sortingOrder + sortingOrderOffset;

        StartCoroutine(FadeAndDestroy(sr, ghost));
    }

    private IEnumerator FadeAndDestroy(
        SpriteRenderer sr,
        GameObject ghost)
    {
        float elapsed = 0f;
        Color startColor = sr.color;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / lifetime);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);

            sr.color = c;

            yield return null;
        }

        Destroy(ghost);
    }
}