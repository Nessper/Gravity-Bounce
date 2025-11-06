using System;
using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    private float levelDuration;
    private float timeLeft;
    // Temps écoulé depuis le début du niveau
    public float GetElapsedTime() => Mathf.Clamp(levelDuration - timeLeft, 0f, levelDuration);

    public bool isOver => timeLeft <= 0f;

    public event Action OnTimerEnd;

    void Awake() => ResetTimer();

    void Update()
    {
        if (isOver) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            OnTimerEnd?.Invoke(); // notifie proprement
        }
    }

    // Lance un nouveau timer avec une durée donnée
    public void StartTimer(float duration)
    {

        levelDuration = Mathf.Max(0f, duration);
        timeLeft = levelDuration;
    }

    public void ResetTimer() => timeLeft = levelDuration;

    public float GetTimeLeft() => timeLeft;

    public string GetTimeAsText() => Mathf.CeilToInt(timeLeft).ToString();


}
