using System;
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

    public event Action<bool> OnClosedStateChanged;

    private bool isClosed = false;
    private bool canControl = true;
    private bool lockClosed = false;

    private float lastStateSfxTimeUnscaled = -999f;

    public void SetActiveControl(bool state)
    {
        canControl = state;

        if (!canControl && isClosed && !lockClosed)
        {
            SetClosedState(false, playSfx: true);
        }
    }

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

    public void ForceCloseAndLock()
    {
        lockClosed = true;
        SetClosedState(true, playSfx: playSfxOnForceCloseAndLock);
    }

    public void ClearLock()
    {
        lockClosed = false;
    }

    public bool IsClosed()
    {
        return isClosed;
    }

    private void SetClosedState(bool closed, bool playSfx)
    {
        isClosed = closed;

        if (leftCloseWall != null) leftCloseWall.SetActive(closed);
        if (rightCloseWall != null) rightCloseWall.SetActive(closed);

        if (leftBinVisual != null) leftBinVisual.SetClosed(closed);
        if (rightBinVisual != null) rightBinVisual.SetClosed(closed);

        if (playSfx)
            TryPlayStateSfx(closed);

        OnClosedStateChanged?.Invoke(isClosed);
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