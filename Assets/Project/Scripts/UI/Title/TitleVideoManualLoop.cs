using UnityEngine;
using UnityEngine.Video;

public class TitleVideoManualLoop : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
            return;

        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;
    }

    private void OnEnable()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.loopPointReached += OnLoopPointReached;
    }

    private void OnDisable()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.loopPointReached -= OnLoopPointReached;
    }

    private void OnLoopPointReached(VideoPlayer vp)
    {
        // Relance manuelle au debut.
        // Sur certains appareils, c'est moins "violent" que le looping interne.
        vp.time = 0;
        vp.Play();
    }
}
