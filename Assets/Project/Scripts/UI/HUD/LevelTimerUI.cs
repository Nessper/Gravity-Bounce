using System.Collections;
using UnityEngine;
using TMPro;

public class LevelTimerUI : MonoBehaviour
{
    [SerializeField] private LevelTimer timer;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Fin de timer")]
    [SerializeField] private float holdAtZeroDuration = 2f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    private bool hideSequenceStarted;
    private Coroutine hideRoutine;

    private void OnEnable()
    {
        if (timer != null)
            timer.OnTimerStarted += HandleTimerStarted;

        ResetUI();
    }

    private void OnDisable()
    {
        if (timer != null)
            timer.OnTimerStarted -= HandleTimerStarted;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }

    private void HandleTimerStarted()
    {
        ResetUI();
    }

    public void ResetUI()
    {
        hideSequenceStarted = false;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void Update()
    {
        if (timer == null || timerText == null)
            return;

        float timeLeft = timer.GetTimeLeft();
        int seconds = Mathf.CeilToInt(timeLeft);

        if (seconds < 0)
            seconds = 0;

        timerText.text = seconds.ToString("00");

        if (!hideSequenceStarted && timeLeft <= 0f)
        {
            hideSequenceStarted = true;
            hideRoutine = StartCoroutine(CoHideAfterZero());
        }
    }

    private IEnumerator CoHideAfterZero()
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.alpha = 1f;

        if (holdAtZeroDuration > 0f)
            yield return new WaitForSecondsRealtime(holdAtZeroDuration);

        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        hideRoutine = null;
    }
}