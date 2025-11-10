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
            yield return new WaitForSeconds(1f);
            counter--;
        }

        // "GO !" final
        countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        countdownText.gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    // --- AJOUT MINIMAL : compte à rebours générique (realtime), sans "GO!" ---
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

        // Affichage entier 10,9,8... (cadence 1s). Si tu veux plus fluide, passe à 0.1f.
        while (remaining > 0f)
        {
            countdownText.text = Mathf.CeilToInt(remaining).ToString();
            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;
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
