using UnityEngine;
using UnityEngine.Events;

public class DefeatGameOverUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private GameObject gameOverPanel;
    // defeatPanel : panneau affiché en cas de défaite simple (le joueur a encore des vies).
    // gameOverPanel : panneau affiché en cas de Game Over (plus de vies restantes).

    [Header("Actions")]
    public UnityEvent OnRetryRequested;
    public UnityEvent OnMenuRequested;
    // OnRetryRequested : invoqué lorsque le joueur clique sur "Retry".
    // OnMenuRequested : invoqué lorsque le joueur clique sur "Menu".

    private void Awake()
    {
        // On s'assure au démarrage que les panneaux internes sont éteints.
        // L'overlay (ce GameObject) peut être actif ou non selon la scène,
        // mais par défaut on n'affiche ni Defeat ni Game Over.
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    // Affiche l'état "Defeat" (le joueur a raté le niveau mais il lui reste des vies).
    public void ShowDefeat()
    {
        // Active la racine de l'overlay...
        gameObject.SetActive(true);

        // ...et n'affiche que le panneau Defeat.
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(true);
    }

    // Affiche l'état "Game Over" (plus aucune vie dans la run).
    public void ShowGameOver()
    {
        // Active la racine de l'overlay...
        gameObject.SetActive(true);

        // ...et n'affiche que le panneau Game Over.
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // Masque complètement l'overlay Defeat/Game Over.
    public void HideAll()
    {
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // Coupe complètement l'overlay pour ne pas bloquer les clics ni l'interaction.
        gameObject.SetActive(false);
    }

    // Méthodes branchées sur les boutons Retry/Menu (OnClick dans l'Inspector).

    // Le joueur demande un Retry : on remonte l'intention via l'event.
    public void OnClickRetry()
    {
        OnRetryRequested?.Invoke();
    }

    // Le joueur demande le retour au menu (Title).
    public void OnClickMenu()
    {
        OnMenuRequested?.Invoke();
    }
}
