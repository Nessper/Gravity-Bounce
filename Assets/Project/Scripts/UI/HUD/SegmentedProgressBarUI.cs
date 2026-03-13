using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barre de progression segmentee (UI).
/// Gere :
/// - la coloration des segments selon la progression + seuil (objectif)
/// - une animation "step by step" lorsque la progression augmente
///
/// CONTRAT IMPORTANT :
/// Ce composant doit rester robuste meme si son GameObject
/// (ou un parent) devient inactif pendant un flow de fin / evacuation.
/// Dans ce cas :
/// - on ne lance pas de coroutine
/// - on applique directement l'etat visuel final
/// - on evite tout warning Unity de type StartCoroutine sur objet inactif
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
    /// Permet a un autre systeme d'attendre la fin de l'anim.
    /// </summary>
    public bool IsAnimating => stepRoutine != null;

    private void Awake()
    {
        // Fallback : si aucun tableau n'est assigne, on recupere les Images enfants.
        if (segments == null || segments.Length == 0)
            segments = GetComponentsInChildren<Image>();

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

        if (goalCount <= 0)
        {
            SetThresholdIndex(0);
            return;
        }

        float ratio = Mathf.Clamp01((float)goalCount / totalCount);

        // Nombre de segments qu'il faut allumer pour atteindre l'objectif.
        int requiredFilledSegments = Mathf.CeilToInt(ratio * segmentCount);
        requiredFilledSegments = Mathf.Clamp(requiredFilledSegments, 1, segmentCount);

        // L'index du segment objectif est le dernier segment requis.
        int index = requiredFilledSegments - 1;
        SetThresholdIndex(index);
    }

    /// <summary>
    /// Definit l'index du segment "objectif" (seuil) et rafraichit les couleurs.
    /// </summary>
    public void SetThresholdIndex(int index)
    {
        thresholdIndex = Mathf.Clamp(index, 0, Mathf.Max(0, segmentCount - 1));
        UpdateVisual();
    }

    // --------------------------------------------------------------------
    // Mise a jour de la progression
    // --------------------------------------------------------------------

    /// <summary>
    /// Met a jour la progression en 0..1.
    /// Si animateSteps = true : allumage step-by-step vers la cible.
    /// Si le composant n'est pas dans un etat animable, on applique directement
    /// l'etat final sans lancer de coroutine.
    /// </summary>
    public void SetProgress01(float progress01)
    {
        if (segmentCount <= 0)
            return;

        progress01 = Mathf.Clamp01(progress01);

        int targetFilledSegments = Mathf.CeilToInt(progress01 * segmentCount);
        targetFilledSegments = Mathf.Clamp(targetFilledSegments, 0, segmentCount);

        // Si l'objet / composant n'est pas animable, on applique direct.
        if (!CanAnimate())
        {
            StopStepRoutineIfAny();
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            return;
        }

        // Pas d'animation : update immediat.
        if (!animateSteps)
        {
            StopStepRoutineIfAny();
            currentFilledSegments = targetFilledSegments;
            UpdateVisual();
            return;
        }

        // On stoppe une anim en cours avant d'en relancer une nouvelle.
        StopStepRoutineIfAny();
        stepRoutine = StartCoroutine(AnimateToTargetFilledSegments(targetFilledSegments));
    }

    /// <summary>
    /// Version pratique : calcule progress01 a partir de current/total.
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
    /// Utile pour eviter qu'un overlay de fin s'affiche avant la fin du remplissage.
    /// </summary>
    public IEnumerator WaitForAnimationComplete(float timeoutSec = 2f)
    {
        float t = 0f;

        while (IsAnimating)
        {
            t += Time.unscaledDeltaTime;

            // Safety : on evite un blocage infini si quelque chose se passe mal.
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
            // Si entre-temps le GO / composant n'est plus animable,
            // on termine instantanement proprement sans warning.
            if (!CanAnimate())
            {
                currentFilledSegments = targetFilledSegments;
                UpdateVisual();
                stepRoutine = null;
                yield break;
            }

            currentFilledSegments++;
            UpdateVisual();

            int segmentIndex = currentFilledSegments - 1;
            if (segmentIndex >= 0 && segments != null && segmentIndex < segments.Length)
            {
                if (CanAnimate())
                    StartCoroutine(PulseSegment(segments[segmentIndex]));
            }

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

        if (!CanAnimate())
            yield break;

        Transform tr = segment.transform;

        Vector3 baseScale = Vector3.one;
        Vector3 targetScale = baseScale * pulseScale;

        float halfDuration = pulseDuration * 0.5f;

        // Scale up
        float t = 0f;
        while (t < halfDuration)
        {
            if (!CanAnimate())
            {
                tr.localScale = baseScale;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float k = (halfDuration <= 0f) ? 1f : Mathf.Clamp01(t / halfDuration);
            tr.localScale = Vector3.Lerp(baseScale, targetScale, k);
            yield return null;
        }

        // Scale down
        t = 0f;
        while (t < halfDuration)
        {
            if (!CanAnimate())
            {
                tr.localScale = baseScale;
                yield break;
            }

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

        currentFilledSegments = Mathf.Clamp(currentFilledSegments, 0, segmentCount);

        for (int i = 0; i < segmentCount; i++)
        {
            bool isActive = i < currentFilledSegments;

            if (i == thresholdIndex)
            {
                // Segment objectif :
                // - avant d'etre atteint : jaune
                // - apres : vert
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

    /// <summary>
    /// Retourne true si le composant est dans un etat ou il peut lancer / executer
    /// des coroutines d'animation UI.
    /// </summary>
    private bool CanAnimate()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }
}