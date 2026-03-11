using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float startValue = 3f; // Compte a rebours de 3 secondes

    private Coroutine running;

    /// <summary>
    /// Countdown de demarrage de niveau : 3-2-1 puis GO.
    /// </summary>
    public void PlayStartCountdown(Action onComplete)
    {
        StopRunning();
        running = StartCoroutine(PlayStartCountdownRoutine(onComplete));
    }

    /// <summary>
    /// Countdown generique en secondes (scale).
    /// showGo permet d afficher (ou non) "GO!" a la fin.
    /// </summary>
    public void PlaySeconds(float totalSeconds, bool showGo, Action onComplete = null)
    {
        StopRunning();
        running = StartCoroutine(PlaySecondsRoutine(totalSeconds, showGo, onComplete));
    }

    private IEnumerator PlayStartCountdownRoutine(Action onComplete)
    {
        if (!countdownText)
        {
            Debug.LogWarning("[CountdownUI] Aucun TMP_Text assigne !");
            onComplete?.Invoke();
            yield break;
        }

        countdownText.gameObject.SetActive(true);

        int counter = Mathf.CeilToInt(startValue);
        while (counter > 0)
        {
            countdownText.text = counter.ToString();
            PlaySfxSafe(SfxId.CountdownTick);

            yield return new WaitForSeconds(1f);
            counter--;
        }

        countdownText.text = "GO!";
        PlaySfxSafe(SfxId.CountdownGo);

        yield return new WaitForSeconds(0.5f);

        countdownText.gameObject.SetActive(false);
        running = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlaySecondsRoutine(float totalSeconds, bool showGo, Action onComplete)
    {
        if (!countdownText)
        {
            Debug.LogWarning("[CountdownUI] Aucun TMP_Text assigne !");
            onComplete?.Invoke();
            yield break;
        }

        float remaining = Mathf.Max(0f, totalSeconds);
        countdownText.gameObject.SetActive(true);

        int lastDisplayed = -1;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;

            int displayValue = Mathf.CeilToInt(remaining);
            if (displayValue < 0)
                displayValue = 0;

            if (displayValue != lastDisplayed)
            {
                lastDisplayed = displayValue;
                countdownText.text = displayValue.ToString();

                if (displayValue > 0)
                    PlaySfxSafe(SfxId.CountdownTick);
            }

            yield return null;
        }

        if (showGo)
        {
            countdownText.text = "GO!";
            PlaySfxSafe(SfxId.CountdownGo);
            yield return new WaitForSeconds(0.35f);
        }

        countdownText.gameObject.SetActive(false);
        running = null;
        onComplete?.Invoke();
    }

    public void Hide()
    {
        StopRunning();

        if (countdownText)
            countdownText.gameObject.SetActive(false);
    }

    private void StopRunning()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }
    }

    private void PlaySfxSafe(SfxId id)
    {
        if (BootRoot.Audio == null)
            return;

        BootRoot.Audio.PlaySfx(id);
    }
}
