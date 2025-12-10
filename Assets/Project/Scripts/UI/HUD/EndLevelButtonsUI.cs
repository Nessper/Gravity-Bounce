using UnityEngine;

/// <summary>
/// Gere les boutons de fin de niveau (MENU / RETRY / NEXT).
/// Ce composant ne fait que montrer ou cacher les boutons
/// en fonction du resultat (victoire, defaite, game over).
/// La logique de navigation est geree ailleurs (LevelEndFlowController).
/// </summary>
public class EndLevelButtonsUI : MonoBehaviour
{
    [Header("Button roots")]
    [SerializeField] private GameObject buttonMenuRoot;
    [SerializeField] private GameObject buttonRetryRoot;
    [SerializeField] private GameObject buttonNextRoot;

    /// <summary>
    /// Cas victoire: MENU + NEXT visibles, RETRY cache.
    /// </summary>
    public void ShowVictory()
    {
        if (buttonMenuRoot != null) buttonMenuRoot.SetActive(true);
        if (buttonRetryRoot != null) buttonRetryRoot.SetActive(false);
        if (buttonNextRoot != null) buttonNextRoot.SetActive(true);

        Debug.Log("[EndLevelButtonsUI] ShowVictory -> MENU + NEXT");
    }

    /// <summary>
    /// Cas defaite (mais contrat encore valide): MENU + RETRY visibles.
    /// </summary>
    public void ShowDefeat()
    {
        if (buttonMenuRoot != null) buttonMenuRoot.SetActive(true);
        if (buttonRetryRoot != null) buttonRetryRoot.SetActive(true);
        if (buttonNextRoot != null) buttonNextRoot.SetActive(false);

        Debug.Log("[EndLevelButtonsUI] ShowDefeat -> MENU + RETRY");
    }

    /// <summary>
    /// Cas game over: uniquement MENU visible.
    /// </summary>
    public void ShowGameOver()
    {
        if (buttonMenuRoot != null) buttonMenuRoot.SetActive(true);
        if (buttonRetryRoot != null) buttonRetryRoot.SetActive(false);
        if (buttonNextRoot != null) buttonNextRoot.SetActive(false);

        Debug.Log("[EndLevelButtonsUI] ShowGameOver -> MENU only");
    }
}
