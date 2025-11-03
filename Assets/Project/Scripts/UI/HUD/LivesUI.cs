using TMPro;
using UnityEngine;

public class LivesUI : MonoBehaviour
{
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private string prefix = "x";

    public void SetLives(int lives)
    {
        if (livesText != null)
            livesText.text = $"{prefix}{lives}";
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (livesText == null)
            livesText = GetComponentInChildren<TMP_Text>();
    }
#endif
}
