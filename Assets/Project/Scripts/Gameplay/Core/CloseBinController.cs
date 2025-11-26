using UnityEngine;

/// <summary>
/// Gère l'ouverture / fermeture des bacs côté gameplay (murs de fermeture)
/// et notifie le visuel de chaque bac.
/// Input actuel : maintien de Shift pour fermer les bacs.
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
    /// Active / désactive la prise en compte de l'input.
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

    private void Update()
    {
        if (!canControl)
            return;

        bool shiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shiftDown)
        {
            if (!isClosed)
                SetClosedState(true);
        }
        else
        {
            if (isClosed)
                SetClosedState(false);
        }
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
}
