using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gère les boutons de fin de niveau (MENU / RETRY / NEXT) :
/// - Affiche / cache les boutons selon le résultat.
/// - Charge les scènes Title / Main quand on clique.
/// </summary>
public class EndLevelButtonsUI : MonoBehaviour
{
    [Header("Button roots")]
    [SerializeField] private GameObject buttonMenuRoot;
    [SerializeField] private GameObject buttonRetryRoot;
    [SerializeField] private GameObject buttonNextRoot;

    [Header("Scene names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string mainSceneName = "Main";

    // ----------------------------------------
    // CONFIG D'AFFICHAGE
    // ----------------------------------------

    public void ShowVictory()
    {
        if (buttonMenuRoot != null) buttonMenuRoot.SetActive(true);
        if (buttonRetryRoot != null) buttonRetryRoot.SetActive(false);
        if (buttonNextRoot != null) buttonNextRoot.SetActive(true);

        Debug.Log("[EndLevelButtonsUI] ShowVictory() -> MENU+NEXT");
    }

    public void ShowDefeat()
    {
        if (buttonMenuRoot != null) buttonMenuRoot.SetActive(true);
        if (buttonRetryRoot != null) buttonRetryRoot.SetActive(true);
        if (buttonNextRoot != null) buttonNextRoot.SetActive(false);

        Debug.Log("[EndLevelButtonsUI] ShowDefeat() -> MENU+RETRY");
    }

    public void ShowGameOver()
    {
        if (buttonMenuRoot != null) buttonMenuRoot.SetActive(true);
        if (buttonRetryRoot != null) buttonRetryRoot.SetActive(false);
        if (buttonNextRoot != null) buttonNextRoot.SetActive(false);

        Debug.Log("[EndLevelButtonsUI] ShowGameOver() -> MENU only");
    }

    // ----------------------------------------
    // CALLBACKS BOUTONS
    // (à lier dans les Button.onClick)
    // ----------------------------------------

    public void OnClickMenu()
    {
        Debug.Log("[EndLevelButtonsUI] MENU clicked -> load " + titleSceneName);

        if (string.IsNullOrEmpty(titleSceneName))
        {
            Debug.LogWarning("[EndLevelButtonsUI] titleSceneName non configuré.");
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }

    public void OnClickRetry()
    {
        Debug.Log("[EndLevelButtonsUI] RETRY clicked -> load " + mainSceneName);

        if (string.IsNullOrEmpty(mainSceneName))
        {
            Debug.LogWarning("[EndLevelButtonsUI] mainSceneName non configuré.");
            return;
        }

        SceneManager.LoadScene(mainSceneName);
    }

    public void OnClickNext()
    {
        Debug.Log("[EndLevelButtonsUI] NEXT clicked (pas encore branché campagne).");
        // Plus tard : navigation vers le prochain niveau.
    }
}
