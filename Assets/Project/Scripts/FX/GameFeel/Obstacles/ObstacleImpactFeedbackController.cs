using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralise le feedback visuel des obstacles lorsqu'ils sont touchés.
/// Reçoit les impacts via ObstacleHitEmitter.
/// </summary>
public class ObstacleImpactFeedbackController : MonoBehaviour
{
    [Header("Punch scale")]
    [SerializeField] private float minScaleMultiplier = 1.015f;
    [SerializeField] private float maxScaleMultiplier = 1.06f;
    [SerializeField] private float impactSpeedForMaxScale = 8f;
    [SerializeField] private float scaleDuration = 0.12f;

    [Header("Punch position")]
    [SerializeField] private float minOffset = 0.005f;
    [SerializeField] private float maxOffset = 0.035f;
    [SerializeField] private float impactSpeedForMaxOffset = 8f;
    [SerializeField] private float offsetDuration = 0.10f;

    private readonly Dictionary<Transform, Coroutine> activeRoutines = new();

    private void OnEnable()
    {
        ObstacleHitEmitter.OnObstacleHit += HandleObstacleHit;
    }

    private void OnDisable()
    {
        ObstacleHitEmitter.OnObstacleHit -= HandleObstacleHit;

        foreach (var routine in activeRoutines.Values)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        activeRoutines.Clear();
    }

    private void HandleObstacleHit(ObstacleHitInfo hit)
    {
        if (hit.VisualRoot == null)
            return;

        if (activeRoutines.TryGetValue(hit.VisualRoot, out Coroutine running) && running != null)
            StopCoroutine(running);

        activeRoutines[hit.VisualRoot] = StartCoroutine(ImpactRoutine(hit));
    }

    private IEnumerator ImpactRoutine(ObstacleHitInfo hit)
    {
        Transform target = hit.VisualRoot;

        Vector3 baseScale = target.localScale;
        Vector3 baseLocalPosition = target.localPosition;

        float impact01 = Mathf.InverseLerp(0f, impactSpeedForMaxScale, hit.ImpactSpeed);

        float scaleMultiplier = Mathf.Lerp(minScaleMultiplier, maxScaleMultiplier, impact01);
        float offsetAmount = Mathf.Lerp(minOffset, maxOffset, Mathf.InverseLerp(0f, impactSpeedForMaxOffset, hit.ImpactSpeed));

        Vector3 localHitDirection = target.parent != null
            ? target.parent.InverseTransformDirection(hit.Direction).normalized
            : hit.Direction.normalized;

        Vector3 targetScale = baseScale * scaleMultiplier;
        Vector3 targetOffset = baseLocalPosition + localHitDirection * offsetAmount;

        float duration = Mathf.Max(scaleDuration, offsetDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);

            float punch = Mathf.Sin(n * Mathf.PI);

            if (scaleDuration > 0f)
            {
                float scaleN = Mathf.Clamp01(t / scaleDuration);
                float scalePunch = Mathf.Sin(scaleN * Mathf.PI);
                target.localScale = Vector3.LerpUnclamped(baseScale, targetScale, scalePunch);
            }

            if (offsetDuration > 0f)
            {
                float offsetN = Mathf.Clamp01(t / offsetDuration);
                float offsetPunch = Mathf.Sin(offsetN * Mathf.PI);
                target.localPosition = Vector3.LerpUnclamped(baseLocalPosition, targetOffset, offsetPunch);
            }

            yield return null;
        }

        target.localScale = baseScale;
        target.localPosition = baseLocalPosition;

        activeRoutines.Remove(target);
    }
}