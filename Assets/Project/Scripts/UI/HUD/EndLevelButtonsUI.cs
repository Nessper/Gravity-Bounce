using UnityEngine;

/// <summary>
/// Gere les boutons de fin de niveau (MENU / RETRY / NEXT).
/// Ce composant ne fait que montrer ou cacher les boutons
/// en fonction du resultat (victoire, defaite, game over).
/// La logique de navigation est geree ailleurs.
/// </summary>
public class EndLevelButtonsUI : MonoBehaviour
{
    [Header("Button roots")]
    [SerializeField] private GameObject buttonMenuRoot;
    [SerializeField] private GameObject buttonRetryRoot;
    [SerializeField] private GameObject buttonNextRoot;

    [Header("Optional root (HUD_EndLevel)")]
    [SerializeField] private GameObject root;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        HideAll();
    }

    public void HideAll()
    {
        if (root != null)
            root.SetActive(false);

        SetActive(buttonMenuRoot, false);
        SetActive(buttonRetryRoot, false);
        SetActive(buttonNextRoot, false);
    }

    /// <summary>
    /// Cas victoire: MENU + NEXT.
    /// </summary>
    public void ShowVictory()
    {
        if (root != null)
            root.SetActive(true);

        SetActive(buttonMenuRoot, true);
        SetActive(buttonRetryRoot, false);
        SetActive(buttonNextRoot, true);

        Debug.Log("[EndLevelButtonsUI] ShowVictory -> MENU + NEXT");
    }

    /// <summary>
    /// Cas defaite (mais contrat encore valide): MENU + RETRY.
    /// </summary>
    public void ShowDefeat()
    {
        if (root != null)
            root.SetActive(true);

        SetActive(buttonMenuRoot, true);
        SetActive(buttonRetryRoot, true);
        SetActive(buttonNextRoot, false);

        Debug.Log("[EndLevelButtonsUI] ShowDefeat -> MENU + RETRY");
    }

    /// <summary>
    /// Cas game over: uniquement MENU.
    /// </summary>
    public void ShowGameOver()
    {
        if (root != null)
            root.SetActive(true);

        SetActive(buttonMenuRoot, true);
        SetActive(buttonRetryRoot, false);
        SetActive(buttonNextRoot, false);

        Debug.Log("[EndLevelButtonsUI] ShowGameOver -> MENU only");
    }

    private void SetActive(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }
}
