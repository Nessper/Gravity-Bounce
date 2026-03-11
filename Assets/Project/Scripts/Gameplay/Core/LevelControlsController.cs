using UnityEngine;

public class LevelControlsController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;

    [Header("Pause")]
    [Tooltip("Controleur de pause. Active pendant le gameplay, desactive pendant les sequences non-interruptibles.")]
    [SerializeField] private PauseController pauseController;

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
        if (mobileControlsRoot != null)
            mobileControlsRoot.SetActive(false);
    }

    public void DisableGameplayControls()
    {
        // 1) Gameplay inputs OFF
        if (player != null)
            player.SetActiveControl(false);

        if (closeBinController != null)
            closeBinController.SetActiveControl(false);

        // 2) Pause OFF (et force un resume si elle etait active)
        if (pauseController != null)
        {
            pauseController.ForceResume();
            pauseController.EnablePause(false);
        }

        // 3) UI mobile OFF
        if (isMobileRuntime && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(false);
    }

    public void EnableGameplayControls()
    {
        // 1) Gameplay inputs ON
        if (player != null)
            player.SetActiveControl(true);

        if (closeBinController != null)
            closeBinController.SetActiveControl(true);

        // 2) Pause ON
        if (pauseController != null)
            pauseController.EnablePause(true);

        // 3) UI mobile ON (mobile uniquement)
        if (isMobileRuntime && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(true);
    }

    // Affiche l'UI mobile sans toucher aux controles
    public void ShowMobileControlsUI(bool visible)
    {
        if (!isMobileRuntime || mobileControlsRoot == null)
            return;

        mobileControlsRoot.SetActive(visible);
    }
}
