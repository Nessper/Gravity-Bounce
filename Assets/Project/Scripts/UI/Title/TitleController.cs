using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Config")]
    [SerializeField] private float fadeInDelay = 5f;  // délai avant l’apparition
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private string nextScene = "Main";

    private void Start()
    {
        // Désactive les clics pendant le fade-in
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (playButton) playButton.onClick.AddListener(OnPlayPressed);
        if (quitButton) quitButton.onClick.AddListener(OnQuitPressed);

        if (RunConfig.Instance != null && RunConfig.Instance.SkipTitleIntroOnce)
        {
            // affichage instantané
            if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            RunConfig.Instance.SkipTitleIntroOnce = false; // reset one-shot
            return;
        }

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        // Attend le délai défini avant de lancer le fade
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

    public void OnPlayPressed()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        StartCoroutine(FadeOutCanvasAndLoad());
    }


    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator FadeOutCanvasAndLoad()
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
        SceneManager.LoadScene(nextScene);
    }
}
