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

    public void Hide()
    {
        if (countdownText)
            countdownText.gameObject.SetActive(false);
    }
}
