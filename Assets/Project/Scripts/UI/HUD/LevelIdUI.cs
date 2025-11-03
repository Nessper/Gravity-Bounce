using TMPro;
using UnityEngine;

public class LevelIdUI : MonoBehaviour
{
    [SerializeField] private TMP_Text idText;

    public void SetLevelId(string levelId)
    {
        if (idText != null)
            idText.text = levelId;
    }

}
