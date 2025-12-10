using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Title scene.
/// Handles UI fade in and out, New Game, Continue and Quit actions.
/// Scene transitions are delegated to the GameFlowController through BootRoot.
/// </summary>
public class TitleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Main Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("Warning New Game")]
    [SerializeField] private GameObject warningNewGamePanel;
    [SerializeField] private Button warningNewGame_BackButton;
    [SerializeField] private Button warningNewGame_OkButton;

    [Header("Config")]
    [SerializeField] private float fadeInDelay = 5f;
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;

    private void Start()
    {
        // The Title scene should always be loaded from Boot
        // so that BootRoot and GameFlowController are correctly set up.
        if (BootRoot.GameFlow == null)
        {
            Debug.LogError("[TitleController] BootRoot.GameFlow is null. Title scene must be started from Boot scene.");
        }

        SetupInitialState();
        SetupContinueButtonVisibility();

        // Optional skip of the intro fade once, controlled by RunConfig.
        if (RunConfig.Instance != null && RunConfig.Instance.SkipTitleIntroOnce)
        {
            SkipIntroFade();
            return;
        }

        StartCoroutine(FadeInRoutine());
    }

    // ---------------------------------------------------------
    // Initial UI setup
    // ---------------------------------------------------------

    /// <summary>
    /// Prepares the initial state of the canvas and hides the New Game warning panel.
    /// </summary>
    private void SetupInitialState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);
    }

    /// <summary>
    /// Shows or hides the Continue button depending on saved run data.
    /// </summary>
    private void SetupContinueButtonVisibility()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.Current : null;

        bool hasRun = save != null
                      && save.runState != null
                      && save.runState.hasOngoingRun;

        if (continueButton != null)
            continueButton.gameObject.SetActive(hasRun);
    }

    /// <summary>
    /// Immediately skips the intro fade and enables all UI.
    /// </summary>
    private void SkipIntroFade()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = false;
    }

    // ---------------------------------------------------------
    // Fade logic
    // ---------------------------------------------------------

    /// <summary>
    /// Fades the UI canvas in after an optional delay.
    /// Uses unscaled time for consistent appearance.
    /// </summary>
    private IEnumerator FadeInRoutine()
    {
        if (fadeInDelay > 0f)
            yield return new WaitForSecondsRealtime(fadeInDelay);

        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInTime);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Fades the UI canvas out before leaving the Title scene.
    /// </summary>
    private IEnumerator FadeOutRoutine()
    {
        float startAlpha = canvasGroup.alpha;
        float t = 0f;

        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeOutTime);
            yield return null;
        }

        canvasGroup.alpha = 0f;

        // Small pause to ensure the fade completes visually.
        yield return new WaitForSecondsRealtime(0.2f);
    }

    // ---------------------------------------------------------
    // Button callbacks
    // These methods are meant to be hooked from the Inspector.
    // ---------------------------------------------------------

    /// <summary>
    /// Called when the New Game button is pressed.
    /// Shows a confirmation popup if a run is already in progress.
    /// </summary>
    public void OnNewGamePressed()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.Current : null;

        bool hasRun = save != null
                      && save.runState != null
                      && save.runState.hasOngoingRun;

        if (hasRun)
        {
            if (warningNewGamePanel != null)
                warningNewGamePanel.SetActive(true);

            return;
        }

        StartNewGame();
    }

    /// <summary>
    /// Called when the Back button of the New Game warning popup is pressed.
    /// </summary>
    public void OnNewGameWarningBack()
    {
        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);
    }

    /// <summary>
    /// Called when the OK button of the New Game warning popup is pressed.
    /// Resets run data then starts a new game.
    /// </summary>
    public void OnNewGameWarningOk()
    {
        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);

        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetRunState();

        StartNewGame();
    }

    /// <summary>
    /// Resets the run state and starts a new game by moving to the ShipSelect scene.
    /// </summary>
    private void StartNewGame()
    {
        // Explicit reset to guarantee a clean state.
        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetRunState();

        StartCoroutine(StartNewGameRoutine());
    }

    private IEnumerator StartNewGameRoutine()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        yield return FadeOutRoutine();

        // New game always leads to ShipSelect.
        BootRoot.GameFlow.GoToShipSelect();
    }

    /// <summary>
    /// Called when the Continue button is pressed.
    /// Resumes the run in progress by loading the main level scene.
    /// </summary>
    public void OnContinuePressed()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        StartCoroutine(ContinueRoutine());
    }

    private IEnumerator ContinueRoutine()
    {
        yield return FadeOutRoutine();

        BootRoot.GameFlow.StartLevel();
    }

    /// <summary>
    /// Called when the Quit button is pressed.
    /// Exits the application or stops play mode in the editor.
    /// </summary>
    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
