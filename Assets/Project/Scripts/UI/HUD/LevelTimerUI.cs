using UnityEngine;
using TMPro;

public class LevelTimerUI : MonoBehaviour
{
    [SerializeField] private LevelTimer timer;
    [SerializeField] private TMP_Text timerText;

    void Update()
    {
        if (!timer || !timerText) return;

        int seconds = Mathf.CeilToInt(timer.GetTimeLeft());
        timerText.text = seconds.ToString("00"); // "09", "32", etc.
    }
}
