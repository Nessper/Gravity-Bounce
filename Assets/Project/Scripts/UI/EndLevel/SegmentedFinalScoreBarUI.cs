using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barre de score final segmentée (EndLevel).
/// - supporte 3 thresholds (Bronze / Silver / Gold),
/// - la progression est basée sur un ratio [0..1] du score final,
/// - expose la médaille actuellement affichée par la barre animée,
///   y compris lors d'une redescente progressive.
/// </summary>
public class SegmentedFinalScoreBarUI : MonoBehaviour
{
    [Header("Segments")]
    [SerializeField] private Image[] segments;

    [Header("Couleurs - base")]
    [SerializeField] private Color inactiveColor = new Color(0.05f, 0.2f, 0.25f, 0.4f);
    [SerializeField] private Color activeColor = new Color(0.2f, 0.9f, 1.0f, 1.0f);

    [Header("Couleurs - thresholds")]
    [SerializeField] private Color bronzeColor = new Color(0.9f, 0.6f, 0.3f, 1.0f);
    [SerializeField] private Color bronzeReachedColor = new Color(1.0f, 0.7f, 0.35f, 1.0f);

    [SerializeField] private Color silverColor = new Color(0.8f, 0.8f, 0.9f, 1.0f);
    [SerializeField] private Color silverReachedColor = new Color(0.9f, 0.9f, 1.0f, 1.0f);

    [SerializeField] private Color goldColor = new Color(1.0f, 0.9f, 0.3f, 1.0f);
    [SerializeField] private Color goldReachedColor = new Color(1.0f, 1.0f, 0.5f, 1.0f);

    [Header("Animation")]
    [SerializeField] private bool animateSteps = true;
    [SerializeField] private float stepDelay = 0.04f;
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.08f;

    private int segmentCount;
    private int currentFilledSegments;
    private Coroutine stepRoutine;

    // Index des segments correspondant aux thresholds.
    // -1 si non définis.
    private int bronzeIndex = -1;
    private int silverIndex = -1;
    private int goldIndex = -1;

    private EndMedal displayedMedal = EndMedal.None;

    public int SegmentCount => segmentCount;
    public EndMedal DisplayedMedal => displayedMedal;

    /// <summary>
    /// Event unique : la médaille actuellement affichée par la barre a changé.
    /// </summary>
    public event Action<EndMedal> OnDisplayedMedalChanged;

    private void Awake()
    {
        if (segments == null || segments.Length == 0)
            segments = GetComponentsInChildren<Image>();

        segmentCount = segments != null ? segments.Length : 0;
        segmentCount = Mathf.Max(0, segmentCount);

        currentFilledSegments = 0;
        displayedMedal = EndMedal.None;

        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Configuration des thresholds (depuis les points de score)
    // --------------------------------------------------------------------

    public void SetThresholdsFromGoals(int bronzeScore, int silverScore, int goldScore, int maxScore)
    {
        if (segmentCount <= 0 || maxScore <= 0)
        {
            bronzeIndex = silverIndex = goldIndex = -1;
            currentFilledSegments = 0;
            SetDisplayedMedalInternal(EndMedal.None);
            UpdateVisual();
            return;
        }

        int ScoreToIndex(int score)
        {
            score = Mathf.Max(0, score);
            float ratio = Mathf.Clamp01((float)score / maxScore);
            int filledSegments = Mathf.FloorToInt(ratio * segmentCount);

            if (filledSegments <= 0)
                return 0;

            return Mathf.Clamp(filledSegments - 1, 0, segmentCount - 1);
        }

        bronzeIndex = (bronzeScore > 0) ? ScoreToIndex(bronzeScore) : -1;
        silverIndex = (silverScore > 0) ? ScoreToIndex(silverScore) : -1;
        goldIndex = (goldScore > 0) ? ScoreToIndex(goldScore) : -1;

        UpdateVisual();
        RefreshDisplayedMedal();
    }

    // --------------------------------------------------------------------
    // Mise à jour de la progression (0..1)
    // --------------------------------------------------------------------

    public void SetProgress01(float progress01)
    {
        if (segmentCount <= 0)
            return;

        progress01 = Mathf.Clamp01(progress01);
        int targetFilledSegments = Mathf.FloorToInt(progress01 * segmentCount);

        if (!animateSteps)
        {
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            RefreshDisplayedMedal();
            return;
        }

        if (stepRoutine != null)
            StopCoroutine(stepRoutine);

        stepRoutine = StartCoroutine(AnimateToTargetFilledSegments(targetFilledSegments));
    }

    public void ResetInstant()
    {
        if (stepRoutine != null)
        {
            StopCoroutine(stepRoutine);
            stepRoutine = null;
        }

        currentFilledSegments = 0;
        UpdateVisual();
        SetDisplayedMedalInternal(EndMedal.None);
    }

    // --------------------------------------------------------------------
    // Animation
    // --------------------------------------------------------------------

    private IEnumerator AnimateToTargetFilledSegments(int targetFilledSegments)
    {
        targetFilledSegments = Mathf.Clamp(targetFilledSegments, 0, segmentCount);

        if (targetFilledSegments == currentFilledSegments)
        {
            stepRoutine = null;
            yield break;
        }

        // Montée progressive
        while (currentFilledSegments < targetFilledSegments)
        {
            currentFilledSegments++;
            UpdateVisual();
            RefreshDisplayedMedal();

            int segmentIndex = currentFilledSegments - 1;
            if (segmentIndex >= 0 && segmentIndex < segments.Length)
                StartCoroutine(PulseSegment(segments[segmentIndex]));

            yield return new WaitForSecondsRealtime(stepDelay);
        }

        // Descente progressive
        while (currentFilledSegments > targetFilledSegments)
        {
            currentFilledSegments--;
            UpdateVisual();
            RefreshDisplayedMedal();

            yield return new WaitForSecondsRealtime(stepDelay);
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
        float timer = 0f;

        while (timer < halfDuration)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / halfDuration);
            t.localScale = Vector3.Lerp(baseScale, targetScale, k);
            yield return null;
        }

        timer = 0f;
        while (timer < halfDuration)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / halfDuration);
            t.localScale = Vector3.Lerp(targetScale, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
    }

    // --------------------------------------------------------------------
    // Médaille actuellement affichée
    // --------------------------------------------------------------------

    private void RefreshDisplayedMedal()
    {
        SetDisplayedMedalInternal(ComputeDisplayedMedalFromFilledSegments(currentFilledSegments));
    }

    private EndMedal ComputeDisplayedMedalFromFilledSegments(int filledSegments)
    {
        // Un threshold est atteint si son index est dans les segments allumés.
        if (goldIndex >= 0 && goldIndex < filledSegments)
            return EndMedal.Gold;

        if (silverIndex >= 0 && silverIndex < filledSegments)
            return EndMedal.Silver;

        if (bronzeIndex >= 0 && bronzeIndex < filledSegments)
            return EndMedal.Bronze;

        return EndMedal.None;
    }

    private void SetDisplayedMedalInternal(EndMedal medal)
    {
        if (displayedMedal == medal)
            return;

        displayedMedal = medal;
        OnDisplayedMedalChanged?.Invoke(displayedMedal);
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
            Image img = segments[i];
            if (img == null)
                continue;

            if (i == goldIndex && goldIndex >= 0)
            {
                img.color = isActive ? goldReachedColor : goldColor;
            }
            else if (i == silverIndex && silverIndex >= 0)
            {
                img.color = isActive ? silverReachedColor : silverColor;
            }
            else if (i == bronzeIndex && bronzeIndex >= 0)
            {
                img.color = isActive ? bronzeReachedColor : bronzeColor;
            }
            else
            {
                img.color = isActive ? activeColor : inactiveColor;
            }
        }
    }
}