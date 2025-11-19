using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Overlay affiché en cas de défaite.
/// - Mode DEFEAT : le joueur a encore des vies (Retry = garder les vies)
/// - Mode GAME OVER : plus de vies (Retry = reset complet)
/// Ce script gère :
/// - L'affichage d'un seul panel avec deux modes
/// - L'activation des blocs (score niveau / score run)
/// - Les actions des boutons gérées EN CODE (pas via Inspector)
/// </summary>
public class DefeatGameOverUI : MonoBehaviour
{
    [Header("Root Panel")]
    [SerializeField] private GameObject rootPanel;

    [Header("Titles")]
    [SerializeField] private GameObject defeatTitle;
    [SerializeField] private GameObject gameOverTitle;

    [Header("Score Blocks")]
    [SerializeField] private GameObject levelScoreBlock; // Score du niveau
    [SerializeField] private GameObject runScoreBlock;   // Score de campagne (uniquement GameOver)

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    // Events émis vers GameFlowController
    public UnityEvent OnRetryDefeat;     // Retry -> garder les vies
    public UnityEvent OnRetryGameOver;   // Retry -> reset complet
    public UnityEvent OnMenu;

    private bool isGameOver = false;

    private void Awake()
    {
        // Au démarrage : panel caché et tout off
        if (rootPanel != null) rootPanel.SetActive(false);
        if (defeatTitle != null) defeatTitle.SetActive(false);
        if (gameOverTitle != null) gameOverTitle.SetActive(false);
        if (levelScoreBlock != null) levelScoreBlock.SetActive(false);
        if (runScoreBlock != null) runScoreBlock.SetActive(false);

        gameObject.SetActive(false);

        // Boutons gérés en code
        if (retryButton != null)
            retryButton.onClick.AddListener(HandleRetryClicked);

        if (menuButton != null)
            menuButton.onClick.AddListener(() => OnMenu?.Invoke());
    }

    // =====================================================================
    // PUBLIC API : GameFlowController appelle ces méthodes
    // =====================================================================

    public void ShowDefeat()
    {
        isGameOver = false;

        gameObject.SetActive(true);
        rootPanel.SetActive(true);

        defeatTitle.SetActive(true);
        gameOverTitle.SetActive(false);

        levelScoreBlock.SetActive(true);
        runScoreBlock.SetActive(false);
    }

    public void ShowGameOver()
    {
        isGameOver = true;

        gameObject.SetActive(true);
        rootPanel.SetActive(true);

        defeatTitle.SetActive(false);
        gameOverTitle.SetActive(true);

        levelScoreBlock.SetActive(true);
        runScoreBlock.SetActive(true);
    }

    public void HideAll()
    {
        defeatTitle.SetActive(false);
        gameOverTitle.SetActive(false);
        levelScoreBlock.SetActive(false);
        runScoreBlock.SetActive(false);
        rootPanel.SetActive(false);

        gameObject.SetActive(false);
    }

    // =====================================================================
    // BOUTONS (gérés 100% en C#)
    // =====================================================================

    private void HandleRetryClicked()
    {
        if (isGameOver)
        {
            // Reset complet (nouvelles vies depuis ShipDefinition)
            OnRetryGameOver?.Invoke();
        }
        else
        {
            // Garde les vies actuelles
            OnRetryDefeat?.Invoke();
        }
    }
}
