using UnityEngine;
using UnityEngine.Events;

public class DefeatGameOverUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Actions")]
    public UnityEvent OnRetryRequested;
    public UnityEvent OnMenuRequested;

    private void Awake()
    {
        // On s'assure que les panneaux internes sont éteints au départ.
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public void ShowDefeat()
    {
        // Active la racine de l'overlay + panneau Defeat uniquement
        gameObject.SetActive(true);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(true);
    }

    public void ShowGameOver()
    {
        // Active la racine de l'overlay + panneau Game Over uniquement
        gameObject.SetActive(true);

        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void HideAll()
    {
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // Coupe complètement l'overlay pour ne pas bloquer les clics
        gameObject.SetActive(false);
    }

    // Branchés sur les boutons Retry/Menu (OnClick)
    public void OnClickRetry()
    {
        OnRetryRequested?.Invoke();
    }

    public void OnClickMenu()
    {
        OnMenuRequested?.Invoke();
    }
}
