using UnityEngine;

/// <summary>
/// Gère l'ouverture / fermeture des bacs côté gameplay (murs de fermeture)
/// et notifie le visuel de chaque bac.
/// Ne lit aucun input directement : l'état "fermé / ouvert" est fourni par des sources externes.
/// Joue un SFX système lors d'un changement d'état (close/open).
/// </summary>
public class CloseBinController : MonoBehaviour
{
    [Header("Close walls physiques des bacs")]
    [SerializeField] private GameObject leftCloseWall;
    [SerializeField] private GameObject rightCloseWall;

    [Header("Contrôleurs visuels des bacs")]
    [SerializeField] private BinVisualController leftBinVisual;
    [SerializeField] private BinVisualController rightBinVisual;

    [Header("Audio (Bin Lock)")]
    [SerializeField] private SfxId closeSfx = SfxId.CloseBin;

    [Tooltip("Optionnel : son à l'ouverture. Laisse None si tu veux silence à l'ouverture.")]
    [SerializeField] private SfxId openSfx = SfxId.None;

    [Tooltip("Anti spam : délai minimal entre deux sons d'état (secondes, unscaled).")]
    [SerializeField] private float stateSfxCooldownSec = 0.08f;

    [Tooltip("Si vrai, ForceCloseAndLock joue aussi le son de fermeture.")]
    [SerializeField] private bool playSfxOnForceCloseAndLock = false;

    private bool isClosed = false;
    private bool canControl = true;

    // Si true, on force les bacs fermés même si canControl=false
    private bool lockClosed = false;

    private float lastStateSfxTimeUnscaled = -999f;

    /// <summary>
    /// Active ou désactive la prise en compte de l'input.
    /// Si on coupe le contrôle alors que les bacs sont fermés,
    /// on les rouvre pour éviter de rester bloqué visuellement / physiquement,
    /// sauf si lockClosed est actif.
    /// </summary>
    public void SetActiveControl(bool state)
    {
        canControl = state;

        if (!canControl && isClosed && !lockClosed)
        {
            SetClosedState(false, playSfx: true);
        }
    }

    /// <summary>
    /// Méthode appelée par une source d'input (clavier, bouton, etc.).
    /// desiredClosed = true si l'input demande de fermer les bacs (maintien),
    /// false si l'input lâche et que les bacs doivent s'ouvrir.
    /// </summary>
    public void SetClosedFromInput(bool desiredClosed)
    {
        if (lockClosed)
            return;

        if (!canControl)
        {
            if (isClosed)
                SetClosedState(false, playSfx: true);

            return;
        }

        if (desiredClosed == isClosed)
            return;

        SetClosedState(desiredClosed, playSfx: true);
    }

    /// <summary>
    /// Force les bacs fermés et verrouille cet état.
    /// Utilisé pour GameOver "Hull détruit" : on veut figer le plateau.
    /// </summary>
    public void ForceCloseAndLock()
    {
        lockClosed = true;
        SetClosedState(true, playSfx: playSfxOnForceCloseAndLock);
    }

    /// <summary>
    /// Déverrouille le lock (utile si tu réutilises la scène sans reload).
    /// </summary>
    public void ClearLock()
    {
        lockClosed = false;
    }

    /// <summary>
    /// Permet à d'autres systèmes de connaitre l'état actuel des bacs.
    /// </summary>
    public bool IsClosed()
    {
        return isClosed;
    }

    /// <summary>
    /// Applique l'état ouvert / fermé :
    /// - active / désactive les murs de fermeture
    /// - met à jour le visuel des bacs via BinVisualController
    /// - déclenche le SFX système si demandé
    /// </summary>
    private void SetClosedState(bool closed, bool playSfx)
    {
        isClosed = closed;

        // Physique
        if (leftCloseWall != null) leftCloseWall.SetActive(closed);
        if (rightCloseWall != null) rightCloseWall.SetActive(closed);

        // Visuels
        if (leftBinVisual != null) leftBinVisual.SetClosed(closed);
        if (rightBinVisual != null) rightBinVisual.SetClosed(closed);

        // Audio
        if (playSfx)
            TryPlayStateSfx(closed);
    }

    private void TryPlayStateSfx(bool closed)
    {
        if (BootRoot.Audio == null)
            return;

        float now = Time.unscaledTime;
        if (stateSfxCooldownSec > 0f && (now - lastStateSfxTimeUnscaled) < stateSfxCooldownSec)
            return;

        lastStateSfxTimeUnscaled = now;

        if (closed)
        {
            if (closeSfx != SfxId.None)
                BootRoot.Audio.PlaySfx(closeSfx);
        }
        else
        {
            if (openSfx != SfxId.None)
                BootRoot.Audio.PlaySfx(openSfx);
        }
    }
}
