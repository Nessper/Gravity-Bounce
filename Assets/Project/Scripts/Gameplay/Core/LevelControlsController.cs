using UnityEngine;

/// <summary>
/// Controleur simple des controles gameplay du niveau.
///
/// Responsabilites :
/// - activer / desactiver les inputs gameplay
/// - afficher / masquer l UI mobile
///
/// IMPORTANT :
/// - ne gere plus la pause
/// - la pause est pilotee ailleurs par le systeme UI principal
/// </summary>
public class LevelControlsController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;

    [Header("Mobile Controls UI (optionnel)")]
    [Tooltip("Racine de l UI mobile (ControlsLayer ou equivalent).")]
    [SerializeField] private GameObject mobileControlsRoot;

    private bool isMobileRuntime;
    private bool gameplayControlsRequested = true;
    private bool paused;

    private void Awake()
    {
        // True sur Android / iOS, false sur PC / Editor
        isMobileRuntime = Application.isMobilePlatform;
    }

    private void Start()
    {
        // Au demarrage, on s assure que l UI mobile est OFF.
        if (mobileControlsRoot != null)
            mobileControlsRoot.SetActive(false);
    }

    /// <summary>
    /// Coupe les controles gameplay et masque l UI mobile.
    /// </summary>
    public void DisableGameplayControls()
    {
        gameplayControlsRequested = false;
        ApplyGameplayControlsState();
    }

    /// <summary>
    /// Active les controles gameplay et affiche l UI mobile.
    /// </summary>
    public void EnableGameplayControls()
    {
        gameplayControlsRequested = true;
        ApplyGameplayControlsState();
    }

    /// <summary>
    /// Applique un verrou temporaire de pause sans perdre l'etat demande par le flow du niveau.
    /// </summary>
    public void SetPaused(bool state)
    {
        paused = state;
        ApplyGameplayControlsState();
    }

    /// <summary>
    /// Affiche ou masque l UI mobile sans toucher aux controles gameplay.
    /// </summary>
    public void ShowMobileControlsUI(bool visible)
    {
        if (!isMobileRuntime || mobileControlsRoot == null)
            return;

        mobileControlsRoot.SetActive(visible);
    }

    private void ApplyGameplayControlsState()
    {
        bool enabled = gameplayControlsRequested && !paused;

        if (player != null)
            player.SetActiveControl(enabled);

        if (closeBinController != null)
            closeBinController.SetActiveControl(enabled);

        if (isMobileRuntime && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(enabled);
    }
}
