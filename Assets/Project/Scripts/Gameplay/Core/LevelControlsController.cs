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
        if (player != null)
            player.SetActiveControl(false);

        if (closeBinController != null)
            closeBinController.SetActiveControl(false);

        if (isMobileRuntime && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(false);
    }

    /// <summary>
    /// Active les controles gameplay et affiche l UI mobile.
    /// </summary>
    public void EnableGameplayControls()
    {
        if (player != null)
            player.SetActiveControl(true);

        if (closeBinController != null)
            closeBinController.SetActiveControl(true);

        if (isMobileRuntime && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(true);
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
}