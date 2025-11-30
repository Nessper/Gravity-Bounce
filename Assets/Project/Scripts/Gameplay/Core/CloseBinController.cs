using UnityEngine;

/// <summary>
/// Gère l'ouverture / fermeture des bacs côté gameplay (murs de fermeture)
/// et notifie le visuel de chaque bac.
/// Ne lit aucun input directement :
/// l'état "fermé / ouvert" est fourni par des sources externes (clavier, bouton, etc.).
/// </summary>
public class CloseBinController : MonoBehaviour
{
    [Header("Close walls physiques des bacs")]
    [SerializeField] private GameObject leftCloseWall;
    [SerializeField] private GameObject rightCloseWall;

    [Header("Contrôleurs visuels des bacs")]
    [SerializeField] private BinVisualController leftBinVisual;
    [SerializeField] private BinVisualController rightBinVisual;

    private bool isClosed = false;   // true = bacs fermés
    private bool canControl = true;  // false = input désactivé (fin de niveau, pause, etc.)

    /// <summary>
    /// Active ou désactive la prise en compte de l'input.
    /// Si on coupe le contrôle alors que les bacs sont fermés,
    /// on les rouvre pour éviter de rester bloqué visuellement / physiquement.
    /// </summary>
    public void SetActiveControl(bool state)
    {
        canControl = state;

        if (!canControl && isClosed)
        {
            SetClosedState(false);
        }
    }

    /// <summary>
    /// Méthode appelée par une source d'input (clavier, bouton, etc.).
    /// desiredClosed = true si l'input demande de fermer les bacs (maintien),
    /// false si l'input lâche et que les bacs doivent s'ouvrir.
    /// </summary>
    public void SetClosedFromInput(bool desiredClosed)
    {
        // Si le contrôle est désactivé, on force l'ouverture
        if (!canControl)
        {
            if (isClosed)
            {
                SetClosedState(false);
            }
            return;
        }

        // Si l'état demandé est déjà l'état actuel, on ne fait rien
        if (desiredClosed == isClosed)
            return;

        SetClosedState(desiredClosed);
    }

    /// <summary>
    /// Applique l'état ouvert / fermé :
    /// - active / désactive les murs de fermeture
    /// - met à jour le visuel des bacs via BinVisualController.
    /// </summary>
    private void SetClosedState(bool closed)
    {
        isClosed = closed;

        // Colliders / murs physiques
        if (leftCloseWall != null) leftCloseWall.SetActive(closed);
        if (rightCloseWall != null) rightCloseWall.SetActive(closed);

        // Visuel des bacs
        if (leftBinVisual != null) leftBinVisual.SetClosed(closed);
        if (rightBinVisual != null) rightBinVisual.SetClosed(closed);
    }

    /// <summary>
    /// Permet à d'autres systèmes de connaitre l'état actuel des bacs.
    /// </summary>
    public bool IsClosed()
    {
        return isClosed;
    }
}
