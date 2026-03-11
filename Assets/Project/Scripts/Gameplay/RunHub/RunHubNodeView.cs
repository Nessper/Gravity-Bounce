using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunHubNodeView : MonoBehaviour
{
    public enum VisualState
    {
        Done,
        Current,
        Locked
    }

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    [Header("Text Colors")]
    [SerializeField] private Color doneTextColor = Color.white;
    [SerializeField] private Color currentTextColor = Color.white;
    [SerializeField] private Color lockedTextColor = new Color(0.45f, 0.45f, 0.45f);

    [Header("Icon Colors")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color lockedIconColor = new Color(0.45f, 0.45f, 0.45f);

    [Header("Background Colors")]
    [SerializeField] private Color currentBgColor = new Color(0.24f, 0.55f, 0.53f); // #3D8B87
    [SerializeField] private Color defaultBgColor = Color.black;

    private const float BackgroundAlpha = 0.2f;

    public void Setup(Sprite sprite, string text, VisualState state)
    {
        if (icon != null)
            icon.sprite = sprite;

        if (label != null)
            label.text = text;

        ApplyState(state);
    }

    private void ApplyState(VisualState state)
    {
        if (label == null || background == null || icon == null)
            return;

        switch (state)
        {
            case VisualState.Done:
                label.color = doneTextColor;
                icon.color = normalIconColor;
                SetBackground(defaultBgColor);
                break;

            case VisualState.Current:
                label.color = currentTextColor;
                icon.color = normalIconColor;
                SetBackground(currentBgColor);
                break;

            case VisualState.Locked:
                label.color = lockedTextColor;
                icon.color = lockedIconColor;
                SetBackground(defaultBgColor);
                break;
        }
    }

    private void SetBackground(Color baseColor)
    {
        Color c = baseColor;
        c.a = BackgroundAlpha;
        background.color = c;
    }
}
