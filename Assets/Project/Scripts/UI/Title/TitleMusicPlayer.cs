using UnityEngine;
using System.Collections;

public class TitleMusicPlayer : MonoBehaviour
{
    public static TitleMusicPlayer Instance { get; private set; }

    [SerializeField] private AudioClip music;
    [SerializeField] private float fadeInTime = 1.5f;
    [SerializeField] private float fadeOutTime = 1.5f;
    [SerializeField] private float targetVolume = 0.5f;

    private AudioSource source;
    private Coroutine currentFade;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        if (music != null)
        {
            source.clip = music;
            source.volume = 0f;
            source.loop = true;
            source.Play();
            currentFade = StartCoroutine(FadeIn());
        }
    }

    public void SnapToTargetVolume()
    {
        if (currentFade != null)
        {
            StopCoroutine(currentFade);
            currentFade = null;
        }
        if (source != null)
            source.volume = targetVolume;
    }

    public IEnumerator FadeOut()
    {
        if (currentFade != null)
        {
            StopCoroutine(currentFade);
            currentFade = null;
        }

        float startVol = source.volume;
        float t = 0f;

        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, t / fadeOutTime);
            yield return null;
        }

        source.Stop();
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;

        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, t / fadeInTime);
            yield return null;
        }

        source.volume = targetVolume;
        currentFade = null;
    }
}
