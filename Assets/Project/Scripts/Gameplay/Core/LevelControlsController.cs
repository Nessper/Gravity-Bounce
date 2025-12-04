using UnityEngine;

public class LevelControlsController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;

    [Header("Mobile Controls UI (optionnel)")]
    [Tooltip("Racine de l'UI mobile (ControlsLayer ou equivalent).")]
    [SerializeField] private GameObject mobileControlsRoot;

    private bool isMobileRuntime;

    private void Awake()
    {
        // True sur Android / iOS, false sur PC / Editor
        isMobileRuntime = Application.isMobilePlatform;
    }

    private void Start()
    {
        // Au demarrage, on s'assure que l'UI mobile est OFF.
        // - Sur mobile, elle sera reactivee au moment du gameplay.
        // - Sur PC, elle doit rester OFF tout le temps.
        if (mobileControlsRoot != null)
        {
            mobileControlsRoot.SetActive(false);
        }
    }

    public void DisableGameplayControls()
    {
        if (player != null)
            player.SetActiveControl(false);

        if (closeBinController != null)
            closeBinController.SetActiveControl(false);

        // L'UI mobile ne bouge que sur mobile.
        if (isMobileRuntime && mobileControlsRoot != null)
        {
            mobileControlsRoot.SetActive(false);
        }
    }

    public void EnableGameplayControls()
    {
        if (player != null)
            player.SetActiveControl(true);

        if (closeBinController != null)
            closeBinController.SetActiveControl(true);

        // Sur mobile uniquement, on montre l'UI mobile au moment du gameplay.
        if (isMobileRuntime && mobileControlsRoot != null)
        {
            mobileControlsRoot.SetActive(true);
        }
    }
}
