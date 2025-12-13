using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barre de progression segmentée (UI).
/// Gère :
/// - La coloration des segments selon la progression + seuil (objectif).
/// - Une animation "step by step" lorsque la progression augmente :
///   les segments s'allument un par un, avec un pulse.
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

    /// <summary>Nombre total de segments.</summary>
    public int SegmentCount => segmentCount;

    /// <summary>
    /// Vrai si l'animation step-by-step est en cours.
    /// Permet à un autre système (fin de niveau) d'attendre la fin de l'anim.
    /// </summary>
    public bool IsAnimating => stepRoutine != null;

    private void Awake()
    {
        // Fallback : si aucun tableau n'est assigné, on récupère les Images enfants.
        if (segments == null || segments.Length == 0)
        {
            segments = GetComponentsInChildren<Image>();
        }

        segmentCount = (segments != null) ? segments.Length : 0;
        thresholdIndex = Mathf.Clamp(thresholdIndex, 0, Mathf.Max(0, segmentCount - 1));

        currentFilledSegments = 0;
        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Configuration du seuil
    // --------------------------------------------------------------------

    /// <summary>
    /// Convertit un objectif (goalCount / totalCount) en index de segment.
    /// </summary>
    public void SetThresholdFromGoal(int goalCount, int totalCount)
    {
        if (segmentCount <= 0)
            return;

        if (totalCount <= 0)
        {
            SetThresholdIndex(segmentCount - 1);
            return;
        }

        float ratio = Mathf.Clamp01((float)goalCount / totalCount);
        int index = Mathf.RoundToInt(ratio * (segmentCount - 1));
        SetThresholdIndex(index);
    }

    /// <summary>
    /// Définit l'index du segment "objectif" (seuil) et rafraîchit les couleurs.
    /// </summary>
    public void SetThresholdIndex(int index)
    {
        thresholdIndex = Mathf.Clamp(index, 0, Mathf.Max(0, segmentCount - 1));
        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Mise à jour de la progression
    // --------------------------------------------------------------------

    /// <summary>
    /// Met à jour la progression en 0..1.
    /// Si animateSteps = true : allumage step-by-step vers la cible.
    /// </summary>
    public void SetProgress01(float progress01)
    {
        if (segmentCount <= 0)
            return;

        progress01 = Mathf.Clamp01(progress01);

        // IMPORTANT : on vise un nombre de segments allumés.
        int targetFilledSegments = Mathf.RoundToInt(progress01 * segmentCount);
        targetFilledSegments = Mathf.Clamp(targetFilledSegments, 0, segmentCount);

        // Pas d'animation : update immédiat.
        if (!animateSteps)
        {
            StopStepRoutineIfAny();
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            return;
        }

        // On stoppe une éventuelle anim en cours avant d'en relancer une nouvelle.
        StopStepRoutineIfAny();
        stepRoutine = StartCoroutine(AnimateToTargetFilledSegments(targetFilledSegments));
    }

    /// <summary>
    /// Version pratique : calcule progress01 à partir de current/total.
    /// </summary>
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

    /// <summary>
    /// Attend la fin de l'animation step-by-step.
    /// Utile pour éviter que l'overlay de fin s'affiche avant la fin du remplissage.
    /// </summary>
    public IEnumerator WaitForAnimationComplete(float timeoutSec = 2f)
    {
        float t = 0f;

        while (IsAnimating)
        {
            t += Time.unscaledDeltaTime;

            // Safety : on évite un blocage infini si quelque chose se passe mal.
            if (timeoutSec > 0f && t >= timeoutSec)
                yield break;

            yield return null;
        }
    }

    // --------------------------------------------------------------------
    // Animation : on allume les segments un par un
    // --------------------------------------------------------------------

    private IEnumerator AnimateToTargetFilledSegments(int targetFilledSegments)
    {
        // Si on diminue (reset, fin de niveau, etc.), update direct sans anim.
        if (targetFilledSegments <= currentFilledSegments)
        {
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            stepRoutine = null;
            yield break;
        }

        // Sinon, on augmente : step-by-step.
        while (currentFilledSegments < targetFilledSegments)
        {
            currentFilledSegments++;
            UpdateVisual();

            int segmentIndex = currentFilledSegments - 1;
            if (segmentIndex >= 0 && segments != null && segmentIndex < segments.Length)
            {
                // Pulse non bloquant : OK si plusieurs pulses se chevauchent légèrement.
                StartCoroutine(PulseSegment(segments[segmentIndex]));
            }

            // Realtime pour ne pas dépendre de timeScale (fin de niveau, pause, etc.)
            if (stepDelay > 0f)
                yield return new WaitForSecondsRealtime(stepDelay);
            else
                yield return null;
        }

        stepRoutine = null;
    }

    private IEnumerator PulseSegment(Image segment)
    {
        if (segment == null)
            yield break;

        Transform tr = segment.transform;

        Vector3 baseScale = Vector3.one;
        Vector3 targetScale = baseScale * pulseScale;

        float halfDuration = pulseDuration * 0.5f;

        // Scale up
        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = (halfDuration <= 0f) ? 1f : Mathf.Clamp01(t / halfDuration);
            tr.localScale = Vector3.Lerp(baseScale, targetScale, k);
            yield return null;
        }

        // Scale down
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = (halfDuration <= 0f) ? 1f : Mathf.Clamp01(t / halfDuration);
            tr.localScale = Vector3.Lerp(targetScale, baseScale, k);
            yield return null;
        }

        tr.localScale = baseScale;
    }

    // --------------------------------------------------------------------
    // Application des couleurs
    // --------------------------------------------------------------------

    private void UpdateVisual()
    {
        if (segments == null || segments.Length == 0)
            return;

        // Clamp par sécurité.
        currentFilledSegments = Mathf.Clamp(currentFilledSegments, 0, segmentCount);

        for (int i = 0; i < segmentCount; i++)
        {
            bool isActive = i < currentFilledSegments;

            if (i == thresholdIndex)
            {
                // Segment objectif :
                // - avant d'être atteint : jaune
                // - après : vert
                segments[i].color = isActive ? postGoalColor : goalColor;
            }
            else
            {
                segments[i].color = isActive ? activeColor : inactiveColor;
            }
        }
    }

    private void StopStepRoutineIfAny()
    {
        if (stepRoutine != null)
        {
            StopCoroutine(stepRoutine);
            stepRoutine = null;
        }
    }
}
