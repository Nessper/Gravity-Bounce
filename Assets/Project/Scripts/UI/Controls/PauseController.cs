using UnityEngine;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    public bool IsPaused { get; private set; }
    private bool allowPause = true; // <- NEW: on peut désactiver la pause

    private void Awake()
    {
        Resume(); // sécurité au Play
    }

    private void Update()
    {
        if (!allowPause) return;                 // <- NEW: pas de pause si interdite
        if (Input.GetKeyDown(toggleKey))
            TogglePause();
    }

    /// <summary>Autorise ou interdit la pause. Si on l'interdit alors qu'on est en pause, on reprend.</summary>
    public void EnablePause(bool enabled)
    {
        allowPause = enabled;
        if (!enabled && IsPaused)
            Resume(); // sécurité: on sort de pause si on coupe la possibilité de pauser
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        if (pauseOverlay) pauseOverlay.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pauseOverlay) pauseOverlay.SetActive(false);
    }
}
