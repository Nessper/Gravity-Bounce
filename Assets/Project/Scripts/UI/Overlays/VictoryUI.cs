using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// UI de victoire :
/// - se contente d'afficher/cacher le panneau.
/// - gère les boutons Retry/Menu.
/// Le contenu du score est géré ailleurs (LevelScoreSummaryUI).
/// </summary>
public class VictoryUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject victoryPanel;

    [Header("Actions")]
    public UnityEvent OnRetryRequested;
    public UnityEvent OnMenuRequested;

    private void Awake()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        if (victoryPanel != null)
        {
            gameObject.SetActive(true);
            victoryPanel.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        gameObject.SetActive(false);
    }

    public void OnClickRetry()
    {
        OnRetryRequested?.Invoke();
    }

    public void OnClickMenu()
    {
        OnMenuRequested?.Invoke();
    }
}
