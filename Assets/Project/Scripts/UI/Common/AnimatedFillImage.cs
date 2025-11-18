using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant d'animation de fillAmount pour une Image (barre de progression).
/// Ne contient aucune logique de jeu :
/// - Reçoit une valeur cible entre 0 et 1.
/// - Anime progressivement le fillAmount de l'Image vers cette valeur.
/// 
/// Utilisation typique :
/// - Attaché à un GameObject avec une Image (progress bar).
/// - Appeler SetInstant01(...) pour fixer immédiatement la valeur.
/// - Appeler AnimateTo01(...) pour animer la barre vers une nouvelle valeur.
/// </summary>
public class AnimatedFillImage : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Image targetImage;
    // Image dont on va animer le fillAmount.

    [Header("Animation")]
    [SerializeField] private float unitsPerSecond = 2f;
    // Vitesse de remplissage en "unités de fill" par seconde.
    // 1.0 = la barre peut passer de 0 à 1 en 1 seconde.
    // 2.0 = de 0 à 1 en 0.5 seconde (plus rapide).

    [SerializeField] private float minDuration = 0.05f;
    [SerializeField] private float maxDuration = 0.5f;
    // Durée minimale et maximale de l'animation, quel que soit le delta.

    [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    // Courbe d'easing appliquée sur la progression (0->1).
    // Linéaire par défaut, modifiable dans l'Inspector.

    // Etat interne
    private float displayedValue;
    private float targetValue;

    private bool isAnimating;
    private float animStartTime;
    private float animDuration;
    private float startValue;

    private void Awake()
    {
        // Si aucune Image n'est assignée, on tente de la récupérer automatiquement.
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        // Initialisation du fillAmount avec la valeur initiale (en général 0).
        ApplyValueToImage(displayedValue);
    }

    private void Update()
    {
        if (!isAnimating)
            return;

        float elapsed = Time.unscaledTime - animStartTime;
        float t = animDuration > 0f ? Mathf.Clamp01(elapsed / animDuration) : 1f;

        float eased = curve != null ? curve.Evaluate(t) : t;

        float newValue = Mathf.Lerp(startValue, targetValue, eased);

        if (!Mathf.Approximately(newValue, displayedValue))
        {
            displayedValue = newValue;
            ApplyValueToImage(displayedValue);
        }

        if (t >= 1f)
        {
            isAnimating = false;
            displayedValue = targetValue;
            ApplyValueToImage(displayedValue);
        }
    }

    /// <summary>
    /// Fixe immédiatement la valeur de fill (0..1) sans animation.
    /// Coupe toute animation en cours.
    /// </summary>
    public void SetInstant01(float value01)
    {
        isAnimating = false;
        targetValue = Mathf.Clamp01(value01);
        displayedValue = targetValue;
        ApplyValueToImage(displayedValue);
    }

    /// <summary>
    /// Anime la barre vers une nouvelle valeur de fill (0..1).
    /// Si une animation est en cours, elle est écrasée et repart
    /// de la valeur actuellement affichée.
    /// </summary>
    public void AnimateTo01(float value01)
    {
        float clamped = Mathf.Clamp01(value01);

        // Si aucune animation en cours et même valeur, rien à faire.
        if (!isAnimating && Mathf.Approximately(clamped, displayedValue))
        {
            SetInstant01(clamped);
            return;
        }

        targetValue = clamped;
        startValue = displayedValue;

        float delta = Mathf.Abs(targetValue - startValue);

        if (delta <= 0f)
        {
            SetInstant01(targetValue);
            return;
        }

        // Durée brute basée sur la vitesse souhaitée.
        float rawDuration = unitsPerSecond > 0f ? (delta / unitsPerSecond) : 0f;

        animDuration = Mathf.Clamp(rawDuration, minDuration, maxDuration);
        animStartTime = Time.unscaledTime;
        isAnimating = true;
    }

    /// <summary>
    /// Applique la valeur de fillAmount à l'Image cible.
    /// </summary>
    private void ApplyValueToImage(float value01)
    {
        if (targetImage == null)
            return;

        targetImage.fillAmount = Mathf.Clamp01(value01);
    }

    /// <summary>
    /// Retourne la valeur actuellement affichée (fillAmount).
    /// </summary>
    public float GetDisplayed01()
    {
        return displayedValue;
    }

    public bool IsAnimating
    {
        get { return isAnimating; }
    }

}
