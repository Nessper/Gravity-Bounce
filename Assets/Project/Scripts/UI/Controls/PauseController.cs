using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la pause du jeu :
/// - Toggle via une touche clavier (Esc par défaut)
/// - Toggle via un bouton UI de pause
/// - Affiche / masque un overlay de pause
/// - Expose EnablePause pour permettre au LevelManager (ou autre)
///   d'autoriser / interdire la pause à certains moments.
/// </summary>
public class PauseController : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private GameObject pauseOverlay;   // Panel UI affiché en pause

    [Header("Inputs")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape; // Touche clavier pour pauser (optionnel)
    [SerializeField] private Button pauseButton;                  // Bouton HUD (icône pause)
    [SerializeField] private Button resumeButton;                 // Bouton "Resume" dans l'overlay (optionnel)

    /// <summary>True si le jeu est actuellement en pause.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Si false, la pause est désactivée (ni touche, ni bouton ne font effet).
    /// </summary>
    private bool allowPause = true;

    // =====================================================================
    // CYCLE UNITY
    // =====================================================================

    private void Awake()
    {
        // Sécurité : on démarre toujours en mode "jeu normal"
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);

        // Câblage du bouton Pause (HUD)
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseButtonClicked);

        // Câblage du bouton Resume (overlay) si présent
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeButtonClicked);
    }

    private void Update()
    {
        // Si la pause est interdite, on ignore les inputs
        if (!allowPause)
            return;

        // Touche clavier pour basculer la pause (optionnelle)
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            TogglePause();
        }
    }

    // =====================================================================
    // API PUBLIQUE
    // =====================================================================

    /// <summary>
    /// Autorise ou interdit la pause.
    /// Si on coupe la pause alors qu'on est déjà en pause, on reprend le jeu.
    /// </summary>
    public void EnablePause(bool enabled)
    {
        allowPause = enabled;

        if (!enabled && IsPaused)
        {
            Resume();
        }
    }

    /// <summary>
    /// Bascule l'état de pause : si en jeu -> pause, si en pause -> reprise.
    /// </summary>
    public void TogglePause()
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    /// <summary>
    /// Met le jeu en pause (Time.timeScale = 0).
    /// </summary>
    public void Pause()
    {
        if (IsPaused || !allowPause)
            return;

        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(true);
    }

    /// <summary>
    /// Reprend le jeu (Time.timeScale = 1).
    /// </summary>
    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);
    }

    // =====================================================================
    // CALLBACKS BOUTONS UI
    // =====================================================================

    private void OnPauseButtonClicked()
    {
        if (!allowPause)
            return;

        TogglePause();
    }

    private void OnResumeButtonClicked()
    {
        // Le bouton Resume force simplement la reprise
        Resume();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        // Tentative auto de retrouver un overlay dans les enfants
        if (pauseOverlay == null && transform.childCount > 0)
        {
            pauseOverlay = transform.GetChild(0).gameObject;
        }
    }
#endif
}
