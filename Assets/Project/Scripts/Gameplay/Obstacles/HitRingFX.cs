using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HitRingFX : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float duration = 0.1f;

    [Header("Scale (facteurs relatifs)")]
    [SerializeField] private float startFactor = 0.8f;
    [SerializeField] private float endFactor = 1.2f;

    [Header("Alpha")]
    [SerializeField]
    private AnimationCurve alphaCurve =
        AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private SpriteRenderer _renderer;
    private Coroutine _routine;
    private float _baseScale = 1f;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        _baseScale = transform.localScale.x; // scale calculée par ObstacleBehaviour

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(FXRoutine());
    }

    private IEnumerator FXRoutine()
    {
        float t = 0f;
        Color baseColor = _renderer.color;

        while (t < duration)
        {
            float n = t / Mathf.Max(duration, 0.0001f);

            // Scale relatif à la base
            float factor = Mathf.Lerp(startFactor, endFactor, n);
            float currentScale = _baseScale * factor;
            transform.localScale = Vector3.one * currentScale;

            // Alpha
            float alpha = alphaCurve.Evaluate(n);
            _renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            t += Time.deltaTime;
            yield return null;
        }

        transform.localScale = Vector3.one * (_baseScale * endFactor);
        _renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        Destroy(gameObject);
    }
}
