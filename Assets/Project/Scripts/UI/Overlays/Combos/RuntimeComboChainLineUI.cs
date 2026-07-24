using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeComboChainLineUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup group;

    [Header("Display")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image fill;

    [Header("Animation")]
    [SerializeField] private float fillLerpDuration = 0.16f;

    private Coroutine fillLerpRoutine;

    public void SetValue(
    string displayName,
    Color uiColor,
    int currentBalls,
    int stepBalls,
    int awardedLevel,
    int maxLevel)
    {
        int safeStep = Mathf.Max(1, stepBalls);
        int safeMaxLevel = Mathf.Max(1, maxLevel);

        int completedLevels = currentBalls / safeStep;
        bool isMaxLevelReached = completedLevels >= safeMaxLevel;
        int displayLevel = isMaxLevelReached
            ? safeMaxLevel
            : Mathf.Max(1, completedLevels + 1);

        int targetBalls = isMaxLevelReached
            ? safeMaxLevel * safeStep
            : displayLevel * safeStep;

        float ratio = isMaxLevelReached
            ? 1f
            : Mathf.Clamp01((currentBalls % safeStep) / (float)safeStep);

        if (group != null)
        {
            group.alpha = currentBalls > 0 ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (label != null)
        {
            label.color = uiColor;
            label.text =
                displayName +
                " LV" + displayLevel +
                "\n" +
                "  " + Mathf.Min(currentBalls, targetBalls) +
                "/" + targetBalls;
        }

        if (fill != null)
        {
            fill.color = uiColor;
            AnimateFill(ratio);
        }
    }

    public void Hide()
    {
        StopFillLerp();

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (fill != null)
            fill.fillAmount = 0f;
    }

    private void AnimateFill(float targetRatio)
    {
        StopFillLerp();

        if (fill == null)
            return;

        if (fillLerpDuration <= 0f)
        {
            fill.fillAmount = targetRatio;
            return;
        }

        fillLerpRoutine = StartCoroutine(FillLerpRoutine(fill.fillAmount, targetRatio));
    }

    private IEnumerator FillLerpRoutine(float startRatio, float targetRatio)
    {
        float elapsed = 0f;

        while (elapsed < fillLerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fillLerpDuration);
            fill.fillAmount = Mathf.Lerp(startRatio, targetRatio, t);
            yield return null;
        }

        fill.fillAmount = targetRatio;
        fillLerpRoutine = null;
    }

    private void StopFillLerp()
    {
        if (fillLerpRoutine == null)
            return;

        StopCoroutine(fillLerpRoutine);
        fillLerpRoutine = null;
    }
}
