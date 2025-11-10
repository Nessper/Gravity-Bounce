using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton; // optionnel

    [Header("Config")]
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private string nextScene = "Main";

    private void Start()
    {
        // désactive les clics pendant le fade-in
        if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }

        if (playButton) playButton.onClick.AddListener(OnPlayPressed);
        if (quitButton) quitButton.onClick.AddListener(OnQuitPressed);

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInTime);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        // réactive les clics
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void OnPlayPressed()
    {
        // coupe la musique en fondu
        FindFirstObjectByType<TitleMusicPlayer>()?.StartCoroutine(FindFirstObjectByType<TitleMusicPlayer>().FadeOut());
        // empêche double clics pendant le fade-out
        canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false;
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
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeOutTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        SceneManager.LoadScene(nextScene);
    }
}
