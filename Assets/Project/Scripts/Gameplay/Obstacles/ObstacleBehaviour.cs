using System.Collections;
using UnityEngine;

/// <summary>
/// Feedback visuel d'un obstacle lorsqu'une bille le touche.
/// - Détection de collision par tag et/ou BallState
/// - Flash "charged"
/// - Scale punch sur le visuel
/// Aucun FX externe.
/// </summary>
public class ObstacleBehaviour : MonoBehaviour
{
    [Header("Références visuelles")]
    [Tooltip("Objet contenant le visuel (Idle+Charged).")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("Visuel Idle (sprite ou mesh principal).")]
    [SerializeField] private GameObject idleVisual;

    [Tooltip("Visuel Charged à activer brièvement lors d'un impact.")]
    [SerializeField] private GameObject chargedVisual;

    [Header("Détection de collision")]
    [Tooltip("Tag utilisé par les billes (laisser vide pour ignorer).")]
    [SerializeField] private string ballTag = "Ball";

    [Tooltip("Vérifier la présence d'un BallState sur le collider.")]
    [SerializeField] private bool requireBallStateComponent = true;

    [Header("Flash Charged")]
    [Tooltip("Durée du flash 'charged' lors d'un impact.")]
    [SerializeField] private float flashDuration = 0.03f;

    [Header("Scale punch")]
    [Tooltip("Multiplicateur de scale lors du pic de l'impact.")]
    [SerializeField] private float scaleMultiplier = 1.04f;

    [Tooltip("Durée totale de l'animation de scale (aller-retour).")]
    [SerializeField] private float scalePunchDuration = 0.10f;

    // Coroutines en cours (évite d'empiler les effets)
    private Coroutine _hitRoutine;
    private Coroutine _flashRoutine;

    private void Reset()
    {
        // Valeur par défaut : premier child comme visuel.
        if (transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsBallCollision(collision.collider))
        {
            return;
        }

        PlayHitFeedback();
    }

    /// <summary>
    /// Vérifie si le collider appartient à une bille.
    /// </summary>
    private bool IsBallCollision(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        // Tag
        if (!string.IsNullOrEmpty(ballTag) && !collider.CompareTag(ballTag))
        {
            return false;
        }

        // BallState
        if (requireBallStateComponent && !collider.TryGetComponent(out BallState _))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Lance le feedback complet.
    /// </summary>
    public void PlayHitFeedback()
    {
        if (_hitRoutine != null)
        {
            StopCoroutine(_hitRoutine);
        }

        _hitRoutine = StartCoroutine(HitRoutine());
    }

    /// <summary>
    /// Coroutine principale :
    /// - flash charged
    /// - scale punch
    /// </summary>
    private IEnumerator HitRoutine()
    {
        if (visualRoot == null)
        {
            yield break;
        }

        // Assurer que Idle est actif si besoin
        if (idleVisual != null)
        {
            idleVisual.SetActive(true);
        }

        // Flash en parallèle
        if (chargedVisual != null && flashDuration > 0f)
        {
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        // Scale punch
        yield return StartCoroutine(ScalePunchRoutine());

        _hitRoutine = null;
    }

    /// <summary>
    /// Flash ultra court du visuel charged.
    /// </summary>
    private IEnumerator FlashRoutine()
    {
        if (chargedVisual == null)
        {
            yield break;
        }

        chargedVisual.SetActive(true);

        if (flashDuration > 0f)
        {
            yield return new WaitForSeconds(flashDuration);
        }

        chargedVisual.SetActive(false);
        _flashRoutine = null;
    }

    /// <summary>
    /// Scale punch :
    /// - montée rapide (ease-out)
    /// - retour doux (ease-in)
    /// </summary>
    private IEnumerator ScalePunchRoutine()
    {
        Vector3 baseScale = visualRoot.localScale;
        Vector3 targetScale = baseScale * scaleMultiplier;

        float upDuration = scalePunchDuration * 0.4f;
        float downDuration = scalePunchDuration * 0.6f;

        float t = 0f;

        // Montée
        while (t < upDuration)
        {
            float n = t / Mathf.Max(upDuration, 0.0001f);
            float eased = 1f - Mathf.Pow(1f - n, 2f); // ease-out
            visualRoot.localScale = Vector3.LerpUnclamped(baseScale, targetScale, eased);

            t += Time.deltaTime;
            yield return null;
        }

        // Descente
        t = 0f;
        while (t < downDuration)
        {
            float n = t / Mathf.Max(downDuration, 0.0001f);
            float eased = n * n; // ease-in
            visualRoot.localScale = Vector3.LerpUnclamped(targetScale, baseScale, eased);

            t += Time.deltaTime;
            yield return null;
        }

        visualRoot.localScale = baseScale;
    }
}
