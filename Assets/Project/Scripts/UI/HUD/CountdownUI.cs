using System.Collections;
using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float startValue = 3f; // compte à rebours de 3 secondes

    public IEnumerator PlayCountdown(System.Action onComplete)
    {
        if (!countdownText)
        {
            Debug.LogWarning("[CountdownUI] Aucun TMP_Text assigné !");
            onComplete?.Invoke();
            yield break;
        }

        countdownText.gameObject.SetActive(true);

        int counter = Mathf.CeilToInt(startValue);
        while (counter > 0)
        {
            countdownText.text = counter.ToString();
            yield return new WaitForSeconds(1f); // temps SCALÉ -> respecte Time.timeScale
            counter--;
        }

        // "GO !" final
        countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        countdownText.gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    // --- VERSION SCALÉE : respecte la pause (Time.timeScale) ---
    public IEnumerator PlayCountdownSeconds(float totalSeconds, System.Action onComplete = null)
    {
        if (!countdownText)
        {
            Debug.LogWarning("[CountdownUI] Aucun TMP_Text assigné !");
            onComplete?.Invoke();
            yield break;
        }

        float remaining = Mathf.Max(0f, totalSeconds);
        countdownText.gameObject.SetActive(true);

        int lastDisplayed = -1;

        // Boucle en temps SCALÉ -> Time.deltaTime s'arrête quand Time.timeScale = 0
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
            }

            yield return null;
        }

        countdownText.gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    public void Hide()
    {
        if (countdownText)
            countdownText.gameObject.SetActive(false);
    }
}
