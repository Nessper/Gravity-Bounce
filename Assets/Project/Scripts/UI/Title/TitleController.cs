using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private string shipSelectScene = "ShipSelect";
    [SerializeField] private string mainScene = "Main";

    private void Start()
    {
        SetupInitialFadeState();
        WireButtons();
        SetupContinueButtonVisibility();

        if (RunConfig.Instance != null && RunConfig.Instance.SkipTitleIntroOnce)
        {
            SkipIntroFade();
            return;
        }

        StartCoroutine(FadeIn());
    }

    // ---------------------------------------------------------
    // Init / wiring
    // ---------------------------------------------------------

    private void SetupInitialFadeState()
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

    private void WireButtons()
    {
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGamePressed);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinuePressed);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitPressed);

        // Popup warning buttons
        if (warningNewGame_BackButton != null) warningNewGame_BackButton.onClick.AddListener(OnNewGameWarningBack);
        if (warningNewGame_OkButton != null) warningNewGame_OkButton.onClick.AddListener(OnNewGameWarningOk);
    }

    private void SetupContinueButtonVisibility()
    {
        // Continue only if an ongoing run exists
        var save = SaveManager.Instance != null ? SaveManager.Instance.Current : null;

        bool hasRun = save != null
                      && save.runState != null
                      && save.runState.hasOngoingRun;

        if (continueButton != null)
            continueButton.gameObject.SetActive(hasRun);
    }

    private void SkipIntroFade()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        RunConfig.Instance.SkipTitleIntroOnce = false;
    }

    // ---------------------------------------------------------
    // Fade logic
    // ---------------------------------------------------------

    private IEnumerator FadeIn()
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

    private IEnumerator FadeOutCanvas()
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
        yield return new WaitForSecondsRealtime(0.2f);
    }

    // ---------------------------------------------------------
    // Button actions
    // ---------------------------------------------------------

    private void OnNewGamePressed()
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

    private void OnNewGameWarningBack()
    {
        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);
    }

    private void OnNewGameWarningOk()
    {
        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);

        SaveManager.Instance.ResetRunState();
        StartNewGame();
    }

    private void StartNewGame()
    {
        StartCoroutine(StartGameAfterFade());
    }

    private IEnumerator StartGameAfterFade()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        yield return FadeOutCanvas();

        SceneManager.LoadScene(shipSelectScene);
    }

    private void OnContinuePressed()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        StartCoroutine(ContinueAfterFade());
    }

    private IEnumerator ContinueAfterFade()
    {
        yield return FadeOutCanvas();

        // Continue doit envoyer vers la scène Main
        if (!string.IsNullOrEmpty(mainScene))
        {
            SceneManager.LoadScene(mainScene);
        }
        else
        {
            Debug.LogWarning("[TitleController] mainScene non défini, fallback sur ShipSelect.");
            SceneManager.LoadScene(shipSelectScene);
        }
    }


    private void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
