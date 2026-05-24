using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeComboChainLineUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup group;

    [Header("Display")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image fill;

    public void SetValue(
    string displayName,
    Color uiColor,
    int currentBalls,
    int stepBalls,
    int awardedLevel)
    {
        int safeStep = Mathf.Max(1, stepBalls);

        int currentLevel = currentBalls / safeStep;
        int displayLevel = Mathf.Max(1, currentLevel + 1);

        int targetBalls = displayLevel * safeStep;

        float ratio = Mathf.Clamp01(currentBalls / (float)targetBalls);

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
                "  " + currentBalls +
                "/" + targetBalls;
        }

        if (fill != null)
        {
            fill.color = uiColor;
            fill.fillAmount = ratio;
        }
    }

    public void Hide()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (fill != null)
            fill.fillAmount = 0f;
    }
}