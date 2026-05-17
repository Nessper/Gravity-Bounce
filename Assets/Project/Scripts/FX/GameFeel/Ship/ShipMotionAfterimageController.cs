using System.Collections;
using UnityEngine;

/// <summary>
/// Crée un léger afterimage du ship quand son mouvement est assez rapide.
/// 
/// À placer sur GameFeelRoot.
/// Important :
/// - observe un Transform cible
/// - ne contrôle pas le mouvement
/// - clone uniquement un SpriteRenderer source
/// </summary>
public class ShipMotionAfterimageController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform observedRoot;
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Header("Trigger")]
    [SerializeField] private float velocityThreshold = 0.35f;
    [SerializeField] private float spawnCooldown = 0.05f;

    [Header("Afterimage")]
    [SerializeField] private Color ghostColor = new Color(0f, 0.9f, 1f, 0.18f);
    [SerializeField] private float lifetime = 0.12f;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float scaleMultiplier = 1.01f;
    [SerializeField] private int sortingOrderOffset = -1;

    private Vector3 lastPosition;
    private float lastSpawnTime = -999f;

    private void Awake()
    {
        if (observedRoot == null || sourceRenderer == null)
        {
            Debug.LogWarning("[ShipMotionAfterimageController] Références manquantes.");
            enabled = false;
            return;
        }

        lastPosition = observedRoot.position;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 currentPosition = observedRoot.position;
        float velocity = (currentPosition - lastPosition).magnitude / dt;

        if (velocity >= velocityThreshold && Time.time >= lastSpawnTime + spawnCooldown)
        {
            SpawnAfterimage();
            lastSpawnTime = Time.time;
        }

        lastPosition = currentPosition;
    }

    private void SpawnAfterimage()
    {
        GameObject ghost = new GameObject("Ship_Afterimage");
        ghost.transform.position = sourceRenderer.transform.position;
        ghost.transform.rotation = sourceRenderer.transform.rotation;
        ghost.transform.localScale = sourceRenderer.transform.lossyScale * scaleMultiplier;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = sourceRenderer.sprite;
        sr.flipX = sourceRenderer.flipX;
        sr.flipY = sourceRenderer.flipY;
        sr.color = ghostColor;
        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        sr.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;

        StartCoroutine(FadeAndDestroy(sr, ghost));
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer sr, GameObject ghost)
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