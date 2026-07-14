using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only animated presentation of an integer. Numeric interpolation remains
/// the default; the optional mechanical mode renders independent rolling glyphs.
/// </summary>
public class AnimatedIntText : MonoBehaviour
{
    public enum AnimationMode
    {
        NumericLerp = 0,
        MechanicalOdometer = 1
    }

    [Header("Target")]
    [SerializeField] private TMP_Text targetText;

    [Header("Format")]
    [SerializeField] private bool useThousandSeparator = true;

    [Header("Animation")]
    [SerializeField] private AnimationMode animationMode = AnimationMode.NumericLerp;
    [SerializeField] private float unitsPerSecond = 2000f;
    [SerializeField] private float minDuration = 0.1f;
    [SerializeField] private float maxDuration = 0.6f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Mechanical Odometer")]
    [SerializeField] private float mechanicalRollDuration = 0.22f;
    [SerializeField] private float mechanicalRollDistance = 1f;
    [SerializeField, Range(0.05f, 1f)]
    private float mechanicalCarryWindow = 0.2f;
    [SerializeField] private AnimationCurve mechanicalRollCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool increasingValuesRollUp = true;

    public event Action<int> OnValueStep;

    private sealed class MechanicalGlyph
    {
        public TMP_Text Current;
        public TMP_Text Incoming;
        public char TargetCharacter;
        public Vector2 BasePosition;
        public long PlaceValue;
        public bool Rolls;
    }

    private int displayedValue;
    private int targetValue;
    private bool isAnimating;
    private float animStartTime;
    private float animDuration;
    private int startValue;

    private RectTransform mechanicalRoot;
    private readonly List<MechanicalGlyph> mechanicalGlyphs =
        new List<MechanicalGlyph>();
    private float mechanicalElapsed;
    private float mechanicalDirection = 1f;
    private double mechanicalStartValue;
    private double mechanicalCurrentValue;
    private int mechanicalTargetValue;
    private string mechanicalDisplayedText = string.Empty;

    public bool UsesMechanicalOdometer =>
        animationMode == AnimationMode.MechanicalOdometer;

    private void Awake()
    {
        EnsureTarget();

        if (UsesMechanicalOdometer)
            SetMechanicalInstant(displayedValue);
        else
            ApplyValueToText(displayedValue);
    }

    private void Update()
    {
        if (!isAnimating)
            return;

        if (UsesMechanicalOdometer)
        {
            UpdateMechanicalAnimation();
            return;
        }

        UpdateNumericAnimation();
    }

    public void SetInstant(int value)
    {
        EnsureTarget();
        isAnimating = false;
        displayedValue = value;
        targetValue = value;

        if (UsesMechanicalOdometer)
            SetMechanicalInstant(value);
        else
            ApplyValueToText(value);
    }

    public void AnimateTo(int value)
    {
        EnsureTarget();

        if (UsesMechanicalOdometer)
        {
            AnimateMechanicalTo(value);
            return;
        }

        AnimateNumericTo(value);
    }

    public void SetVisualColor(Color color)
    {
        if (targetText != null)
            targetText.color = color;

        for (int i = 0; i < mechanicalGlyphs.Count; i++)
        {
            MechanicalGlyph glyph = mechanicalGlyphs[i];

            if (glyph.Current != null)
                glyph.Current.color = color;

            if (glyph.Incoming != null)
                glyph.Incoming.color = color;
        }
    }

    public int GetDisplayedValue()
    {
        return displayedValue;
    }

    public bool IsAnimating => isAnimating;

    private void AnimateNumericTo(int value)
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

        float rawDuration = unitsPerSecond > 0f ? delta / unitsPerSecond : 0f;
        animDuration = Mathf.Clamp(rawDuration, minDuration, maxDuration);
        animStartTime = Time.unscaledTime;
        isAnimating = true;
    }

    private void UpdateNumericAnimation()
    {
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

        if (t < 1f)
            return;

        isAnimating = false;

        if (displayedValue == targetValue)
            return;

        displayedValue = targetValue;
        ApplyValueToText(displayedValue);
        OnValueStep?.Invoke(displayedValue);
    }

    private void AnimateMechanicalTo(int value)
    {
        EnsureMechanicalRoot();
        targetValue = value;

        if (isAnimating)
        {
            if (value == mechanicalTargetValue)
                return;

            double retargetStart = mechanicalCurrentValue;
            displayedValue = Mathf.RoundToInt((float)retargetStart);
            mechanicalDisplayedText = FormatValue(displayedValue);
            isAnimating = false;
            BeginMechanicalTransition(value, retargetStart);
            return;
        }

        if (value != displayedValue)
            BeginMechanicalTransition(value);
    }

    private void BeginMechanicalTransition(
        int value,
        double? visualStartValue = null)
    {
        string nextText = FormatValue(value);
        int visualStartRounded = visualStartValue.HasValue
            ? Mathf.RoundToInt((float)visualStartValue.Value)
            : displayedValue;
        string currentText = string.IsNullOrEmpty(mechanicalDisplayedText)
            ? FormatValue(visualStartRounded)
            : mechanicalDisplayedText;
        int glyphCount = Mathf.Max(currentText.Length, nextText.Length);
        currentText = currentText.PadLeft(glyphCount, ' ');
        nextText = nextText.PadLeft(glyphCount, ' ');

        mechanicalTargetValue = value;
        mechanicalStartValue = visualStartValue ?? displayedValue;
        mechanicalCurrentValue = mechanicalStartValue;
        bool magnitudeIncreases =
            Math.Abs((double)value) >= Math.Abs(mechanicalStartValue);
        mechanicalDirection = magnitudeIncreases ? 1f : -1f;

        if (!increasingValuesRollUp)
            mechanicalDirection *= -1f;

        RebuildMechanicalGlyphs(currentText, nextText);

        mechanicalElapsed = 0f;
        isAnimating = true;

        if (!HasRollingGlyph())
            CompleteMechanicalTransition();
    }

    private void UpdateMechanicalAnimation()
    {
        mechanicalElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, mechanicalRollDuration);
        float distance = Mathf.Max(0.1f, mechanicalRollDistance) * GetGlyphHeight();
        float t = Mathf.Clamp01(mechanicalElapsed / duration);
        float eased = mechanicalRollCurve != null
            ? Mathf.Clamp01(mechanicalRollCurve.Evaluate(t))
            : t;
        double currentValue = mechanicalStartValue +
            (mechanicalTargetValue - mechanicalStartValue) * eased;
        mechanicalCurrentValue = currentValue;
        double magnitude = Math.Abs(currentValue);
        bool magnitudeIncreases =
            Math.Abs((double)mechanicalTargetValue) >=
            Math.Abs(mechanicalStartValue);

        for (int i = 0; i < mechanicalGlyphs.Count; i++)
        {
            MechanicalGlyph glyph = mechanicalGlyphs[i];

            if (!glyph.Rolls)
                continue;

            UpdateMechanicalWheel(
                glyph,
                magnitude,
                magnitudeIncreases,
                distance
            );
        }

        if (t >= 1f)
            CompleteMechanicalTransition();
    }

    private void UpdateMechanicalWheel(
        MechanicalGlyph glyph,
        double magnitude,
        bool magnitudeIncreases,
        float distance)
    {
        long placeValue = Math.Max(1L, glyph.PlaceValue);
        double scaled = magnitude / placeValue;
        long currentQuotient;
        long incomingQuotient;
        float rollProgress;

        if (magnitudeIncreases)
        {
            currentQuotient = (long)Math.Floor(scaled);
            incomingQuotient = currentQuotient + 1L;
            double remainder = magnitude - currentQuotient * (double)placeValue;

            if (placeValue == 1L)
            {
                rollProgress = (float)remainder;
            }
            else
            {
                double carryDistance =
                    placeValue * Mathf.Clamp(mechanicalCarryWindow, 0.05f, 1f);
                double carryStart = placeValue - carryDistance;
                rollProgress = Mathf.Clamp01(
                    (float)((remainder - carryStart) / carryDistance)
                );
            }
        }
        else
        {
            double floored = Math.Floor(scaled);
            double remainder = magnitude - floored * placeValue;

            if (remainder <= 0.000001d)
            {
                currentQuotient = (long)floored;
                incomingQuotient = currentQuotient - 1L;
                rollProgress = 0f;
            }
            else
            {
                currentQuotient = (long)floored + 1L;
                incomingQuotient = currentQuotient - 1L;
                double distanceFromUpper = placeValue - remainder;
                double carryDistance = placeValue == 1L
                    ? 1d
                    : placeValue * Mathf.Clamp(mechanicalCarryWindow, 0.05f, 1f);
                rollProgress = Mathf.Clamp01(
                    (float)(distanceFromUpper / carryDistance)
                );
            }
        }

        glyph.Current.text = GetWheelCharacter(currentQuotient, placeValue);
        glyph.Incoming.text = GetWheelCharacter(incomingQuotient, placeValue);
        glyph.Incoming.gameObject.SetActive(rollProgress > 0f);
        glyph.Current.rectTransform.anchoredPosition =
            glyph.BasePosition +
            Vector2.up * mechanicalDirection * distance * rollProgress;
        glyph.Incoming.rectTransform.anchoredPosition =
            glyph.BasePosition +
            Vector2.up * mechanicalDirection * distance * (rollProgress - 1f);
    }

    private void CompleteMechanicalTransition()
    {
        displayedValue = mechanicalTargetValue;
        mechanicalDisplayedText = FormatValue(displayedValue);
        isAnimating = false;

        for (int i = 0; i < mechanicalGlyphs.Count; i++)
        {
            MechanicalGlyph glyph = mechanicalGlyphs[i];
            glyph.Current.text = CharacterToText(glyph.TargetCharacter);
            glyph.Current.rectTransform.anchoredPosition = glyph.BasePosition;
            glyph.Incoming.gameObject.SetActive(false);
            glyph.Rolls = false;
        }

        ApplyValueToTemplate(displayedValue);
        OnValueStep?.Invoke(displayedValue);
    }

    private void SetMechanicalInstant(int value)
    {
        EnsureMechanicalRoot();
        mechanicalStartValue = value;
        mechanicalCurrentValue = value;
        mechanicalTargetValue = value;
        mechanicalDisplayedText = FormatValue(value);
        RebuildMechanicalGlyphs(mechanicalDisplayedText, mechanicalDisplayedText);
        ApplyValueToTemplate(value);
    }

    private void EnsureTarget()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
    }

    private void EnsureMechanicalRoot()
    {
        EnsureTarget();

        if (mechanicalRoot != null || targetText == null)
            return;

        GameObject rootObject = new GameObject(
            "MechanicalOdometer",
            typeof(RectTransform),
            typeof(RectMask2D)
        );

        mechanicalRoot = rootObject.GetComponent<RectTransform>();
        RectTransform targetRect = targetText.rectTransform;
        mechanicalRoot.SetParent(targetRect.parent, false);
        mechanicalRoot.anchorMin = targetRect.anchorMin;
        mechanicalRoot.anchorMax = targetRect.anchorMax;
        mechanicalRoot.anchoredPosition = targetRect.anchoredPosition;
        mechanicalRoot.sizeDelta = targetRect.sizeDelta;
        mechanicalRoot.pivot = targetRect.pivot;
        mechanicalRoot.localRotation = targetRect.localRotation;
        mechanicalRoot.localScale = targetRect.localScale;
        mechanicalRoot.SetSiblingIndex(targetRect.GetSiblingIndex() + 1);
        targetText.enabled = false;
    }

    private void RebuildMechanicalGlyphs(string currentText, string nextText)
    {
        EnsureMechanicalRoot();

        if (mechanicalRoot == null || targetText == null)
            return;

        if (mechanicalGlyphs.Count != currentText.Length)
        {
            ClearMechanicalGlyphs();

            for (int i = 0; i < currentText.Length; i++)
            {
                mechanicalGlyphs.Add(new MechanicalGlyph
                {
                    Current = CreateGlyphText("Current", ' ', 1f, 0f),
                    Incoming = CreateGlyphText("Incoming", ' ', 1f, 0f)
                });
            }
        }

        float totalWidth = 0f;
        float[] widths = new float[currentText.Length];

        for (int i = 0; i < currentText.Length; i++)
        {
            char widthCharacter = nextText[i] != ' ' ? nextText[i] : currentText[i];
            widths[i] = GetGlyphWidth(widthCharacter);
            totalWidth += widths[i];
        }

        float x = -totalWidth * 0.5f;
        int numericCount = 0;

        for (int i = 0; i < currentText.Length; i++)
        {
            if (char.IsDigit(nextText[i]) || char.IsDigit(currentText[i]))
                numericCount++;
        }

        int remainingNumericPlaces = numericCount;

        for (int i = 0; i < currentText.Length; i++)
        {
            float width = widths[i];
            bool numeric = char.IsDigit(nextText[i]) || char.IsDigit(currentText[i]);

            if (numeric)
                remainingNumericPlaces--;

            MechanicalGlyph glyph = mechanicalGlyphs[i];
            long placeValue = numeric
                ? GetPlaceValue(remainingNumericPlaces)
                : 0L;
            glyph.TargetCharacter = nextText[i];
            glyph.PlaceValue = placeValue;
            glyph.Rolls = numeric &&
                GetWheelQuotient(mechanicalStartValue, placeValue) !=
                GetWheelQuotient(mechanicalTargetValue, placeValue);
            glyph.BasePosition = new Vector2(x + width * 0.5f, 0f);

            ConfigureGlyph(glyph.Current, currentText[i], width, x);
            ConfigureGlyph(glyph.Incoming, nextText[i], width, x);
            glyph.Incoming.gameObject.SetActive(glyph.Rolls);
            x += width;
        }
    }

    private TMP_Text CreateGlyphText(string suffix, char character, float width, float x)
    {
        GameObject glyphObject = new GameObject(
            "OdometerGlyph_" + suffix,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );

        RectTransform rect = glyphObject.GetComponent<RectTransform>();
        rect.SetParent(mechanicalRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, GetGlyphHeight());
        rect.anchoredPosition = new Vector2(x + width * 0.5f, 0f);

        TMP_Text glyph = glyphObject.GetComponent<TMP_Text>();
        CopyTextStyle(targetText, glyph);
        glyph.text = CharacterToText(character);
        return glyph;
    }

    private void ConfigureGlyph(
        TMP_Text glyph,
        char character,
        float width,
        float x)
    {
        if (glyph == null)
            return;

        RectTransform rect = glyph.rectTransform;
        rect.sizeDelta = new Vector2(width, GetGlyphHeight());
        rect.anchoredPosition = new Vector2(x + width * 0.5f, 0f);
        glyph.text = CharacterToText(character);
        glyph.color = targetText.color;
        glyph.enableVertexGradient = targetText.enableVertexGradient;
        glyph.colorGradient = targetText.colorGradient;
    }

    private static void CopyTextStyle(TMP_Text source, TMP_Text destination)
    {
        destination.font = source.font;
        destination.fontSharedMaterial = source.fontSharedMaterial;
        destination.fontSize = source.fontSize;
        destination.fontStyle = source.fontStyle;
        destination.fontWeight = source.fontWeight;
        destination.color = source.color;
        destination.enableVertexGradient = source.enableVertexGradient;
        destination.colorGradient = source.colorGradient;
        destination.alignment = TextAlignmentOptions.Center;
        destination.characterSpacing = source.characterSpacing;
        destination.raycastTarget = false;
        destination.enableAutoSizing = false;
        destination.overflowMode = TextOverflowModes.Overflow;
    }

    private void ClearMechanicalGlyphs()
    {
        for (int i = 0; i < mechanicalGlyphs.Count; i++)
        {
            MechanicalGlyph glyph = mechanicalGlyphs[i];

            if (glyph.Current != null)
            {
                glyph.Current.gameObject.SetActive(false);
                Destroy(glyph.Current.gameObject);
            }

            if (glyph.Incoming != null)
            {
                glyph.Incoming.gameObject.SetActive(false);
                Destroy(glyph.Incoming.gameObject);
            }
        }

        mechanicalGlyphs.Clear();
    }

    private bool HasRollingGlyph()
    {
        for (int i = 0; i < mechanicalGlyphs.Count; i++)
        {
            if (mechanicalGlyphs[i].Rolls)
                return true;
        }

        return false;
    }

    private float GetGlyphWidth(char character)
    {
        if (targetText == null)
            return 24f;

        string sample = character == ' ' ? "0" : character.ToString();
        return Mathf.Max(4f, targetText.GetPreferredValues(sample).x);
    }

    private float GetGlyphHeight()
    {
        if (targetText == null)
            return 48f;

        return Mathf.Max(1f, targetText.rectTransform.rect.height);
    }

    private static long GetPlaceValue(int power)
    {
        long value = 1L;

        for (int i = 0; i < power && value <= long.MaxValue / 10L; i++)
            value *= 10L;

        return value;
    }

    private static long GetWheelQuotient(double value, long placeValue)
    {
        if (placeValue <= 0L)
            return 0L;

        return (long)Math.Floor(Math.Abs(value) / placeValue);
    }

    private static string GetWheelCharacter(long quotient, long placeValue)
    {
        long safeQuotient = Math.Max(0L, quotient);

        if (placeValue > 1L && safeQuotient == 0L)
            return string.Empty;

        return (safeQuotient % 10L).ToString();
    }

    private void ApplyValueToText(int value)
    {
        if (targetText != null)
            targetText.text = FormatValue(value);
    }

    private void ApplyValueToTemplate(int value)
    {
        if (targetText == null)
            return;

        targetText.text = FormatValue(value);
        targetText.enabled = false;
    }

    private string FormatValue(int value)
    {
        return useThousandSeparator ? value.ToString("N0") : value.ToString("0");
    }

    private static string CharacterToText(char character)
    {
        return character == ' ' ? string.Empty : character.ToString();
    }
}
