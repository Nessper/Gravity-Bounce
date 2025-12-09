using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Barre de score final segmentée (EndLevel).
/// Très proche de SegmentedProgressBarUI, mais :
/// - supporte 3 thresholds (Bronze / Silver / Gold),
/// - la progression est basée sur un ratio [0..1] du score final (score / progressMax),
///   calculé en dehors (FinalScoreBarUI).
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

    [Header("Events (médailles)")]
    public UnityEvent OnBronzeSegmentLit;
    public UnityEvent OnSilverSegmentLit;
    public UnityEvent OnGoldSegmentLit;

    // Infos runtime
    private int segmentCount;
    private int currentFilledSegments;
    private Coroutine stepRoutine;

    // Index des segments correspondant aux thresholds.
    // -1 si non définis.
    private int bronzeIndex = -1;
    private int silverIndex = -1;
    private int goldIndex = -1;

    // Flags pour ne déclencher chaque médaille qu'une seule fois.
    private bool bronzeFired = false;
    private bool silverFired = false;
    private bool goldFired = false;

    public int SegmentCount => segmentCount;

    private void Awake()
    {
        if (segments == null || segments.Length == 0)
        {
            segments = GetComponentsInChildren<Image>();
        }

        segmentCount = segments.Length;
        segmentCount = Mathf.Max(0, segmentCount);

        currentFilledSegments = 0;
        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Configuration des thresholds (depuis les points de score)
    // --------------------------------------------------------------------

    /// <summary>
    /// Configure les thresholds à partir des points de médaille et du score max.
    /// Exemple :
    /// - bronzeScore, silverScore, goldScore : valeurs depuis LevelData.ScoreGoals
    /// - maxScore : progressMax (ex: Gold * 1.2)
    /// </summary>
    public void SetThresholdsFromGoals(int bronzeScore, int silverScore, int goldScore, int maxScore)
    {
        if (segmentCount <= 0 || maxScore <= 0)
        {
            bronzeIndex = silverIndex = goldIndex = -1;
            bronzeFired = silverFired = goldFired = false;
            UpdateVisual();
            return;
        }

        // Convertit un score en index de segment [0 .. segmentCount-1]
        int ScoreToIndex(int score)
        {
            score = Mathf.Max(0, score);
            float ratio = Mathf.Clamp01((float)score / maxScore);
            return Mathf.RoundToInt(ratio * (segmentCount - 1));
        }

        bronzeIndex = (bronzeScore > 0) ? ScoreToIndex(bronzeScore) : -1;
        silverIndex = (silverScore > 0) ? ScoreToIndex(silverScore) : -1;
        goldIndex = (goldScore > 0) ? ScoreToIndex(goldScore) : -1;

        // On réarme les flags de déclenchement
        bronzeFired = silverFired = goldFired = false;

        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Mise à jour de la progression (0..1)
    // --------------------------------------------------------------------

    public void SetProgress01(float progress01)
    {
        if (segmentCount <= 0)
            return;

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

    public void ResetInstant()
    {
        if (stepRoutine != null)
        {
            StopCoroutine(stepRoutine);
            stepRoutine = null;
        }

        currentFilledSegments = 0;

        // On réarme aussi les flags ici au cas où on réutilise la barre
        bronzeFired = silverFired = goldFired = false;

        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Animation : on allume les segments un par un
    // --------------------------------------------------------------------

    private IEnumerator AnimateToTargetFilledSegments(int targetFilledSegments)
    {
        targetFilledSegments = Mathf.Clamp(targetFilledSegments, 0, segmentCount);

        // Si on diminue (peu probable ici), on met à jour direct sans anim.
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

            // Déclenchement des événements de médailles
            if (!bronzeFired && bronzeIndex >= 0 && segmentIndex == bronzeIndex)
            {
                bronzeFired = true;
                OnBronzeSegmentLit?.Invoke();
            }

            if (!silverFired && silverIndex >= 0 && segmentIndex == silverIndex)
            {
                silverFired = true;
                OnSilverSegmentLit?.Invoke();
            }

            if (!goldFired && goldIndex >= 0 && segmentIndex == goldIndex)
            {
                goldFired = true;
                OnGoldSegmentLit?.Invoke();
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
            Image img = segments[i];
            if (img == null) continue;

            // Cas Gold
            if (i == goldIndex && goldIndex >= 0)
            {
                img.color = isActive ? goldReachedColor : goldColor;
            }
            // Cas Silver
            else if (i == silverIndex && silverIndex >= 0)
            {
                img.color = isActive ? silverReachedColor : silverColor;
            }
            // Cas Bronze
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
