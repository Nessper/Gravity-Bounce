using UnityEngine;
using TMPro;

/// <summary>
/// Composant d'affichage de nombre entier avec animation.
/// Ne contient aucune logique de score :
/// - La "vérité" du score reste dans ScoreManager.
/// - Ce composant ne fait qu'animer l'affichage d'un entier dans un TMP_Text.
/// 
/// Utilisation typique :
/// - Attaché sur un GameObject contenant un TMP_Text (HUD score, score de fin de niveau).
/// - Appeler SetInstant(...) pour fixer une valeur immédiatement.
/// - Appeler AnimateTo(...) pour faire défiler la valeur affichée vers une nouvelle cible.
/// </summary>
public class AnimatedIntText : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private TMP_Text targetText;
    // TMP_Text à mettre à jour. Si non renseigné, sera cherché automatiquement sur le GameObject.

    [Header("Format")]
    [SerializeField] private bool useThousandSeparator = true;
    // Si true : format avec séparateur de milliers (ex: 12 345 via "N0").
    // Si false : format simple (ex: 12345 via "0").

    [Header("Animation")]
    [SerializeField] private float unitsPerSecond = 2000f;
    // Vitesse "théorique" de défilement en points par seconde.
    // Exemple : delta 1000, unitsPerSecond=2000 => durée brute ~0.5s avant clamp.

    [SerializeField] private float minDuration = 0.1f;
    [SerializeField] private float maxDuration = 0.6f;
    // Durée minimale et maximale de l'animation, quel que soit le delta.

    [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    // Courbe d'easing appliquée sur le facteur de progression (0->1).
    // Linear par défaut. Peut être modifiée dans l'Inspector.

    // Etat interne
    private int displayedValue;
    private int targetValue;

    private bool isAnimating;
    private float animStartTime;
    private float animDuration;
    private int startValue;

    private void Awake()
    {
        // Si aucun TMP_Text assigné, on essaie de le récupérer automatiquement.
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        // Initialisation du texte avec la valeur initiale (0).
        ApplyValueToText(displayedValue);
    }

    private void Update()
    {
        if (!isAnimating)
            return;

        float elapsed = Time.unscaledTime - animStartTime;
        float t = animDuration > 0f ? Mathf.Clamp01(elapsed / animDuration) : 1f;

        // Application de la courbe d'easing (lineaire par défaut).
        float eased = curve != null ? curve.Evaluate(t) : t;

        // Interpolation entre startValue et targetValue.
        int newValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, eased));

        // Evite les updates inutiles si la valeur ne change pas.
        if (newValue != displayedValue)
        {
            displayedValue = newValue;
            ApplyValueToText(displayedValue);
        }

        // Fin d'animation si on est arrivé au bout.
        if (t >= 1f)
        {
            isAnimating = false;
            displayedValue = targetValue;
            ApplyValueToText(displayedValue);
        }
    }

    /// <summary>
    /// Fixe instantanément la valeur affichée.
    /// Coupe toute animation en cours.
    /// </summary>
    public void SetInstant(int value)
    {
        isAnimating = false;
        displayedValue = value;
        targetValue = value;
        ApplyValueToText(displayedValue);
    }

    /// <summary>
    /// Lance une animation de la valeur actuelle affichée vers la nouvelle cible.
    /// Si une animation est déjà en cours, elle est écrasée,
    /// et le nouveau départ est pris sur la valeur actuellement affichée.
    /// </summary>
    public void AnimateTo(int value)
    {
        // Si aucune cible ou valeur identique, on peut simplement fixer instantanément.
        if (!isAnimating && value == displayedValue)
        {
            SetInstant(value);
            return;
        }

        // Nouveau target.
        targetValue = value;

        // Point de départ = valeur actuellement affichée.
        startValue = displayedValue;

        int delta = Mathf.Abs(targetValue - startValue);

        // Si delta nul, on fixe directement.
        if (delta == 0)
        {
            SetInstant(targetValue);
            return;
        }

        // Durée brute basée sur la vitesse souhaitée.
        float rawDuration = unitsPerSecond > 0f ? (delta / unitsPerSecond) : 0f;

        // Clamp entre min et max.
        animDuration = Mathf.Clamp(rawDuration, minDuration, maxDuration);

        animStartTime = Time.unscaledTime;
        isAnimating = true;
    }

    /// <summary>
    /// Applique une valeur entière au TMP_Text en respectant le format choisi.
    /// </summary>
    private void ApplyValueToText(int value)
    {
        if (targetText == null)
            return;

        if (useThousandSeparator)
        {
            // "N0" => nombre formaté avec séparateurs de milliers, 0 décimales.
            targetText.text = value.ToString("N0");
        }
        else
        {
            // "0" => entier sans séparateur de milliers.
            targetText.text = value.ToString("0");
        }
    }

    /// <summary>
    /// Retourne la valeur actuellement affichée (pour debug ou autres besoins).
    /// </summary>
    public int GetDisplayedValue()
    {
        return displayedValue;
    }

    public bool IsAnimating
    {
        get { return isAnimating; }
    }

}
