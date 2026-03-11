using System;
using UnityEngine;

/// <summary>
/// Controleur central de pause :
/// - Toggle pause/resume (clavier + bouton HUD)
/// - Gere Time.timeScale
/// - Affiche / masque l'overlay de pause
/// - Met en pause l'audio (SFX gameplay + UI + musique)
///
/// IMPORTANT :
/// - Ne construit PAS le contenu de l'overlay.
/// - Expose un event OnPauseOpening pour que l'orchestrateur rende l'UI juste avant le freeze.
/// </summary>
public class PauseController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Vue overlay (affichage). Le contenu est rendu par l'orchestrateur.")]
    [SerializeField] private PauseOverlayUI overlayUI;

    [Header("Inputs")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Mobile Controls (optionnel)")]
    [SerializeField] private GameObject mobileControlsRoot;

    /// <summary>
    /// Callback appele juste avant l'ouverture de la pause.
    /// Le LevelPauseFlowHandler (ou LevelManager) s'y accroche pour rendre le contenu.
    /// </summary>
    public event Action OnPauseOpening;

    public bool IsPaused { get; private set; }

    private bool allowPause = true;

    private void Awake()
    {
        IsPaused = false;
        allowPause = true;

        // Safety : au chargement, on garantit un etat "non pause".
        Time.timeScale = 1f;

        overlayUI?.Hide();
    }

    private void Update()
    {
        if (!allowPause)
            return;

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            TogglePause();
    }

    public void EnablePause(bool enabled)
    {
        allowPause = enabled;

        // Si on desactive la pause alors qu'on est pause, on resume.
        if (!enabled && IsPaused)
            Resume();
    }

    public void ForceResume()
    {
        if (IsPaused)
            Resume();
    }

    // A cabler dans l'Inspector sur le bouton Pause du HUD
    public void OnPauseButtonClicked()
    {
        TogglePause();
    }

    public void TogglePause()
    {
        if (!allowPause)
            return;

        if (IsPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (IsPaused)
            return;

        IsPaused = true;

        // Juste avant le freeze : l'orchestrateur peut rendre l'UI (stats, boutons, etc.)
        OnPauseOpening?.Invoke();

        // Freeze du jeu
        Time.timeScale = 0f;

        // Audio : pause TOUT (gameplay + UI + musique)
        AudioManager.Instance?.SetPaused(true);

        // UI mobile : masque les controles tactiles
        if (Application.isMobilePlatform && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(false);

        overlayUI?.Show();
    }

    public void Resume()
    {
        if (!IsPaused)
            return;

        IsPaused = false;

        // Unfreeze
        Time.timeScale = 1f;

        // Audio : reprise TOUT (gameplay + UI + musique)
        AudioManager.Instance?.SetPaused(false);

        overlayUI?.Hide();

        // UI mobile : reaffiche les controles tactiles
        if (Application.isMobilePlatform && mobileControlsRoot != null)
            mobileControlsRoot.SetActive(true);
    }
}