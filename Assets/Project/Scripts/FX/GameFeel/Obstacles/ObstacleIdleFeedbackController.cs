using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère les animations idle visuelles des obstacles actifs.
/// Les obstacles sont trouvés automatiquement via ObstacleHitEmitter.
/// Gère aussi le flash ChargedVisual au contact.
/// </summary>
public class ObstacleIdleFeedbackController : MonoBehaviour
{
    [Header("Recherche")]
    [SerializeField] private float refreshInterval = 1f;

    [Header("Rotation idle")]
    [SerializeField] private bool useRotation = true;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;
    [SerializeField] private float rotationDegreesPerSecond = 4f;

    [Header("Wobble idle")]
    [SerializeField] private bool useWobble = true;
    [SerializeField] private float wobbleAmplitude = 0.01f;
    [SerializeField] private float wobbleSpeed = 0.8f;
    [SerializeField] private Vector3 wobbleBaseAxis = Vector3.right;
    [SerializeField] private float wobbleRandomAxisStrength = 0.35f;

    [Header("Variation par obstacle")]
    [SerializeField] private float minSpeedMultiplier = 0.85f;
    [SerializeField] private float maxSpeedMultiplier = 1.15f;

    [Header("Charged flash")]
    [SerializeField] private bool useChargedFlash = true;
    [SerializeField] private float chargedFlashDuration = 0.04f;

    private readonly List<ObstacleHitEmitter> emitters = new();

    private readonly Dictionary<Transform, Vector3> baseLocalPositions = new();
    private readonly Dictionary<Transform, Quaternion> baseLocalRotations = new();
    private readonly Dictionary<Transform, float> randomPhases = new();
    private readonly Dictionary<Transform, float> randomSpeedMultipliers = new();
    private readonly Dictionary<Transform, Vector3> randomWobbleAxes = new();

    private readonly Dictionary<GameObject, Coroutine> activeFlashRoutines = new();

    private float nextRefreshTime;

    private void OnEnable()
    {
        ObstacleHitEmitter.OnObstacleHit += HandleObstacleHit;
        RefreshObstacles();
    }

    private void OnDisable()
    {
        ObstacleHitEmitter.OnObstacleHit -= HandleObstacleHit;

        StopAllFlashRoutines();
        RestoreTransforms();

        emitters.Clear();
        baseLocalPositions.Clear();
        baseLocalRotations.Clear();
        randomPhases.Clear();
        randomSpeedMultipliers.Clear();
        randomWobbleAxes.Clear();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
            RefreshObstacles();

        AnimateObstacles();
    }

    private void HandleObstacleHit(ObstacleHitInfo hit)
    {
        if (!useChargedFlash || hit.ChargedVisual == null)
            return;

        if (activeFlashRoutines.TryGetValue(hit.ChargedVisual, out Coroutine running) && running != null)
            StopCoroutine(running);

        activeFlashRoutines[hit.ChargedVisual] =
            StartCoroutine(ChargedFlashRoutine(hit.ChargedVisual));
    }

    private void RefreshObstacles()
    {
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);

        emitters.Clear();

        ObstacleHitEmitter[] found = FindObjectsByType<ObstacleHitEmitter>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || found[i].VisualRoot == null)
                continue;

            emitters.Add(found[i]);

            Transform visual = found[i].VisualRoot;

            if (!baseLocalPositions.ContainsKey(visual))
                baseLocalPositions.Add(visual, visual.localPosition);

            if (!baseLocalRotations.ContainsKey(visual))
            {
                visual.localRotation = Quaternion.Euler(
                    visual.localEulerAngles.x,
                    visual.localEulerAngles.y,
                    Random.Range(0f, 360f)
                );

                baseLocalRotations.Add(visual, visual.localRotation);
            }

            if (!randomPhases.ContainsKey(visual))
                randomPhases.Add(visual, Random.Range(0f, 100f));

            if (!randomSpeedMultipliers.ContainsKey(visual))
            {
                float min = Mathf.Min(minSpeedMultiplier, maxSpeedMultiplier);
                float max = Mathf.Max(minSpeedMultiplier, maxSpeedMultiplier);

                randomSpeedMultipliers.Add(visual, Random.Range(min, max));
            }

            if (!randomWobbleAxes.ContainsKey(visual))
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    0f
                ) * wobbleRandomAxisStrength;

                Vector3 axis = (wobbleBaseAxis + randomOffset).normalized;

                if (axis.sqrMagnitude <= 0.0001f)
                    axis = Vector3.right;

                randomWobbleAxes.Add(visual, axis);
            }

            if (found[i].ChargedVisual != null)
                found[i].ChargedVisual.SetActive(false);
        }
    }

    private void AnimateObstacles()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < emitters.Count; i++)
        {
            if (emitters[i] == null || emitters[i].VisualRoot == null)
                continue;

            Transform visual = emitters[i].VisualRoot;

            float speedMultiplier =
                randomSpeedMultipliers.TryGetValue(visual, out float mult)
                    ? mult
                    : 1f;

            if (useRotation)
            {
                float angle = rotationDegreesPerSecond * speedMultiplier * dt;
                visual.Rotate(rotationAxis, angle, Space.Self);
            }

            if (useWobble && baseLocalPositions.TryGetValue(visual, out Vector3 basePos))
            {
                float phaseOffset =
                    randomPhases.TryGetValue(visual, out float phase)
                        ? phase
                        : 0f;

                Vector3 wobbleAxis =
                    randomWobbleAxes.TryGetValue(visual, out Vector3 axis)
                        ? axis
                        : Vector3.right;

                float phaseValue = Time.time * wobbleSpeed * speedMultiplier + phaseOffset;
                float offset = Mathf.Sin(phaseValue) * wobbleAmplitude;

                visual.localPosition = basePos + wobbleAxis * offset;
            }
        }
    }

    private IEnumerator ChargedFlashRoutine(GameObject chargedVisual)
    {
        if (chargedVisual == null)
            yield break;

        chargedVisual.SetActive(true);

        if (chargedFlashDuration > 0f)
            yield return new WaitForSeconds(chargedFlashDuration);

        if (chargedVisual != null)
            chargedVisual.SetActive(false);

        activeFlashRoutines.Remove(chargedVisual);
    }

    private void StopAllFlashRoutines()
    {
        foreach (var kvp in activeFlashRoutines)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);

            if (kvp.Key != null)
                kvp.Key.SetActive(false);
        }

        activeFlashRoutines.Clear();
    }

    private void RestoreTransforms()
    {
        foreach (var kvp in baseLocalPositions)
        {
            if (kvp.Key != null)
                kvp.Key.localPosition = kvp.Value;
        }

        foreach (var kvp in baseLocalRotations)
        {
            if (kvp.Key != null)
                kvp.Key.localRotation = kvp.Value;
        }
    }
}