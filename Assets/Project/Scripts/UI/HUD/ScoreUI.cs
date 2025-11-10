using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;

    public void UpdateScoreText(int value)
    {
        if (scoreText != null)
            scoreText.text = value.ToString();
    }
}
