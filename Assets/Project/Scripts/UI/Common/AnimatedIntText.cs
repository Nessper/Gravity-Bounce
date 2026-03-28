using System;
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

    [Header("Format")]
    [SerializeField] private bool useThousandSeparator = true;

    [Header("Animation")]
    [SerializeField] private float unitsPerSecond = 2000f;
    [SerializeField] private float minDuration = 0.1f;
    [SerializeField] private float maxDuration = 0.6f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public event Action<int> OnValueStep;

    private int displayedValue;
    private int targetValue;

    private bool isAnimating;
    private float animStartTime;
    private float animDuration;
    private int startValue;

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        ApplyValueToText(displayedValue);
    }

    private void Update()
    {
        if (!isAnimating)
            return;

        float elapsed = Time.unscaledTime - animStartTime;
        float t = animDuration > 0f ? Mathf.Clamp01(elapsed / animDuration) : 1f;

        float eased = curve != null ? curve.Evaluate(t) : t;

        int newValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, eased));

        if (newValue != displayedValue)
        {
            displayedValue = newValue;
            ApplyValueToText(displayedValue);
            OnValueStep?.Invoke(displayedValue);
        }

        if (t >= 1f)
        {
            isAnimating = false;

            if (displayedValue != targetValue)
            {
                displayedValue = targetValue;
                ApplyValueToText(displayedValue);
                OnValueStep?.Invoke(displayedValue);
            }
        }
    }

    public void SetInstant(int value)
    {
        isAnimating = false;
        displayedValue = value;
        targetValue = value;
        ApplyValueToText(displayedValue);
    }

    public void AnimateTo(int value)
    {
        if (!isAnimating && value == displayedValue)
        {
            SetInstant(value);
            return;
        }

        targetValue = value;
        startValue = displayedValue;

        int delta = Mathf.Abs(targetValue - startValue);

        if (delta == 0)
        {
            SetInstant(targetValue);
            return;
        }

        float rawDuration = unitsPerSecond > 0f ? (delta / unitsPerSecond) : 0f;
        animDuration = Mathf.Clamp(rawDuration, minDuration, maxDuration);

        animStartTime = Time.unscaledTime;
        isAnimating = true;
    }

    private void ApplyValueToText(int value)
    {
        if (targetText == null)
            return;

        if (useThousandSeparator)
            targetText.text = value.ToString("N0");
        else
            targetText.text = value.ToString("0");
    }

    public int GetDisplayedValue()
    {
        return displayedValue;
    }

    public bool IsAnimating
    {
        get { return isAnimating; }
    }
}