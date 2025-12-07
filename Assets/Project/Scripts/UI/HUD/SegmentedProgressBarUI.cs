using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barre de progression segmentée (UI).
/// Gère:
/// - la coloration des segments selon la progression et le threshold.
/// - une animation "step by step" quand la progression augmente:
///   les segments s'allument un par un avec un petit pulse.
/// </summary>
public class SegmentedProgressBarUI : MonoBehaviour
{
    [Header("Segments")]
    [SerializeField] private Image[] segments;
    [SerializeField] private int thresholdIndex = 10;

    [Header("Couleurs")]
    [SerializeField] private Color inactiveColor = new Color(0.05f, 0.2f, 0.25f, 0.4f);
    [SerializeField] private Color activeColor = new Color(0.2f, 0.9f, 1.0f, 1.0f);
    [SerializeField] private Color goalColor = new Color(1.0f, 0.9f, 0.3f, 1.0f);
    [SerializeField] private Color postGoalColor = new Color(0.4f, 1.0f, 0.4f, 1.0f);

    [Header("Animation")]
    [SerializeField] private bool animateSteps = true;
    [SerializeField] private float stepDelay = 0.04f;
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.08f;

    private int segmentCount;
    private int currentFilledSegments;
    private Coroutine stepRoutine;

    public int SegmentCount => segmentCount;

    private void Awake()
    {
        if (segments == null || segments.Length == 0)
        {
            segments = GetComponentsInChildren<Image>();
        }

        segmentCount = segments.Length;
        thresholdIndex = Mathf.Clamp(thresholdIndex, 0, Mathf.Max(0, segmentCount - 1));

        currentFilledSegments = 0;
        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Configuration du seuil
    // --------------------------------------------------------------------

    public void SetThresholdFromGoal(int goalCount, int totalCount)
    {
        if (totalCount <= 0)
        {
            SetThresholdIndex(segmentCount - 1);
            return;
        }

        float ratio = Mathf.Clamp01((float)goalCount / totalCount);
        int index = Mathf.RoundToInt(ratio * (segmentCount - 1));
        SetThresholdIndex(index);
    }

    public void SetThresholdIndex(int index)
    {
        thresholdIndex = Mathf.Clamp(index, 0, Mathf.Max(0, segmentCount - 1));
        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Mise à jour de la progression
    // --------------------------------------------------------------------

    public void SetProgress01(float progress01)
    {
        progress01 = Mathf.Clamp01(progress01);
        int targetFilledSegments = Mathf.RoundToInt(progress01 * segmentCount);

        if (!animateSteps)
        {
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            return;
        }

        // On stoppe une éventuelle anim en cours
        if (stepRoutine != null)
        {
            StopCoroutine(stepRoutine);
        }

        stepRoutine = StartCoroutine(AnimateToTargetFilledSegments(targetFilledSegments));
    }

    public void SetProgressCounts(int current, int total)
    {
        if (total <= 0)
        {
            SetProgress01(0f);
            return;
        }

        float p = (float)current / total;
        SetProgress01(p);
    }

    // --------------------------------------------------------------------
    // Animation: on allume les segments un par un
    // --------------------------------------------------------------------

    private IEnumerator AnimateToTargetFilledSegments(int targetFilledSegments)
    {
        targetFilledSegments = Mathf.Clamp(targetFilledSegments, 0, segmentCount);

        // Si on diminue (reset, fin de niveau), on met à jour direct sans anim.
        if (targetFilledSegments <= currentFilledSegments)
        {
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            stepRoutine = null;
            yield break;
        }

        // On augmente: on allume les segments un par un.
        while (currentFilledSegments < targetFilledSegments)
        {
            currentFilledSegments++;
            UpdateVisual();

            int segmentIndex = currentFilledSegments - 1;
            if (segmentIndex >= 0 && segmentIndex < segments.Length)
            {
                StartCoroutine(PulseSegment(segments[segmentIndex]));
            }

            yield return new WaitForSeconds(stepDelay);
        }

        stepRoutine = null;
    }

    private IEnumerator PulseSegment(Image segment)
    {
        if (segment == null)
            yield break;

        Transform t = segment.transform;
        Vector3 baseScale = Vector3.one;
        Vector3 targetScale = baseScale * pulseScale;

        float halfDuration = pulseDuration * 0.5f;
        float tTime = 0f;

        // Scale up
        while (tTime < halfDuration)
        {
            tTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tTime / halfDuration);
            t.localScale = Vector3.Lerp(baseScale, targetScale, k);
            yield return null;
        }

        // Scale down
        tTime = 0f;
        while (tTime < halfDuration)
        {
            tTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tTime / halfDuration);
            t.localScale = Vector3.Lerp(targetScale, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
    }

    // --------------------------------------------------------------------
    // Application des couleurs
    // --------------------------------------------------------------------

    private void UpdateVisual()
    {
        if (segments == null || segments.Length == 0)
            return;

        for (int i = 0; i < segmentCount; i++)
        {
            bool isActive = i < currentFilledSegments;

            if (i == thresholdIndex)
            {
                // Objectif: jaune avant d'être atteint, vert après
                segments[i].color = isActive ? postGoalColor : goalColor;
            }
            else
            {
                segments[i].color = isActive ? activeColor : inactiveColor;
            }
        }
    }
}
