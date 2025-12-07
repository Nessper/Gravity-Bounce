using TMPro;
using UnityEngine;

public class HullUI : MonoBehaviour
{
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private string prefix = "x";

    public void SetHull(int lives)
    {
        if (hullText != null)
            hullText.text = $"{prefix}{lives}";
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (hullText == null)
            hullText = GetComponentInChildren<TMP_Text>();
    }
#endif
}
