using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game flow controller.
/// Responsable des transitions de scenes : Boot -> Title -> ShipSelect -> RunHub -> Main -> Credits.
/// Il ne decide pas quel vaisseau / quel niveau lancer (RunConfig / SaveManager).
///
/// Musique :
/// - Pilote la musique "macro" : Title/ShipSelect, RunHub, Credits.
/// - Avant de charger Main, coupe la musique (fade out) pour laisser Main gerer ses sous-phases
///   (MainBriefing, MainGameplay, MainEndSequence).
///
/// Notes :
/// - La musique macro est appliquee une fois la scene chargee (comportement simple et stable).
/// - Optionnel : on peut aussi declencher le crossfade AVANT le load (si tu actives le flag),
///   mais ce n'est pas obligatoire.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public enum GameFlowPhase
    {
        Boot,
        Title,
        ShipSelect,
        RunHub,
        Level,
        Credits,
        Loading
    }

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string shipSelectSceneName = "ShipSelect";
    [SerializeField] private string runHubSceneName = "RunHub";
    [SerializeField] private string levelSceneName = "Main";
    [SerializeField] private string creditsSceneName = "CreditsScene";

    [Header("Music - Fade (macro)")]
    [Tooltip("Fade utilise pour les transitions entre scenes non-Gameplay (Title/ShipSelect/RunHub/Credits).")]
    [SerializeField] private float macroMusicFadeOutSec = 0.8f;

    [SerializeField] private float macroMusicFadeInSec = 0.8f;

    [Tooltip("Fade utilise quand on quitte une scene macro pour entrer dans Main (Level).")]
    [SerializeField] private float enterMainMusicFadeOutSec = 0.8f;

    [Header("Music - Advanced (optionnel)")]
    [Tooltip("Si true, tente de lancer la musique macro AVANT le chargement de la scene (peut lisser le ressenti si load long).")]
    [SerializeField] private bool prewarmMacroMusicBeforeLoad = false;

    public GameFlowPhase CurrentPhase { get; private set; } = GameFlowPhase.Boot;

    /// <summary>
    /// True pendant un load async. Evite les doubles transitions.
    /// </summary>
    private bool isLoadingScene;

    private void Awake()
    {
        BootRoot.RegisterGameFlow(this);
    }

    // ---------------------------------------------------------
    // Public API
    // ---------------------------------------------------------

    public void GoToTitle()
    {
        if (isLoadingScene) return;
        Debug.Log("[GameFlow] GoToTitle");
        StartSceneTransition(titleSceneName, GameFlowPhase.Title);
    }

    public void GoToShipSelect()
    {
        if (isLoadingScene) return;
        StartSceneTransition(shipSelectSceneName, GameFlowPhase.ShipSelect);
    }

    public void GoToRunHub()
    {
        if (isLoadingScene) return;
        StartSceneTransition(runHubSceneName, GameFlowPhase.RunHub);
    }

    public void StartLevel()
    {
        if (isLoadingScene) return;
        StartSceneTransition(levelSceneName, GameFlowPhase.Level);
    }

    public void StartCredits()
    {
        if (isLoadingScene) return;
        StartSceneTransition(creditsSceneName, GameFlowPhase.Credits);
    }

    public void RetryLevel()
    {
        if (isLoadingScene) return;
        Debug.Log("[GameFlow] RetryLevel");
        StartSceneTransition(levelSceneName, GameFlowPhase.Level);
    }

    // ---------------------------------------------------------
    // Internal
    // ---------------------------------------------------------

    /// <summary>
    /// Mapping "phase -> MusicId" pour la musique macro.
    /// - Title et ShipSelect partagent la meme musique (continuité).
    /// - Main (Level) est gere en interne par le flow de Main (Briefing / Gameplay / EndSequence).
    /// </summary>
    private MusicId GetMusicForPhase(GameFlowPhase phase)
    {
        switch (phase)
        {
            case GameFlowPhase.Title:
            case GameFlowPhase.ShipSelect:
                return MusicId.Title;

            case GameFlowPhase.RunHub:
                return MusicId.RunHub;

            case GameFlowPhase.Credits:
                return MusicId.Credits;

            default:
                return MusicId.None;
        }
    }

    private void StartSceneTransition(string sceneName, GameFlowPhase targetPhase)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameFlowController] Scene name is empty. Cannot start transition.");
            return;
        }

        // 1) Cas special : entrer dans Main (Level)
        // On coupe la musique macro AVANT le chargement.
        // Main relancera MainBriefing au moment exact ou son UI s'affiche.
        if (AudioManager.Instance != null && targetPhase == GameFlowPhase.Level)
        {
            AudioManager.Instance.StopMusic(enterMainMusicFadeOutSec);
        }

        // 2) Optionnel : prewarm macro music (avant load) pour les scenes non-Level
        // Attention : si tu actives ce flag, tu assumes que la transition scene est "coherente"
        // (pas de fade ecran qui contredit). Par defaut, OFF = simple et stable.
        if (prewarmMacroMusicBeforeLoad && AudioManager.Instance != null && targetPhase != GameFlowPhase.Level)
        {
            MusicId id = GetMusicForPhase(targetPhase);

            if (id != MusicId.None)
                AudioManager.Instance.PlayMusic(id, macroMusicFadeOutSec, macroMusicFadeInSec);
        }

        StartCoroutine(LoadSceneRoutine(sceneName, targetPhase));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, GameFlowPhase targetPhase)
    {
        isLoadingScene = true;
        CurrentPhase = GameFlowPhase.Loading;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        CurrentPhase = targetPhase;
        isLoadingScene = false;

        // --------------------------------------------------------------------
        // Audio macro (hors Main)
        // --------------------------------------------------------------------
        if (AudioManager.Instance != null && targetPhase != GameFlowPhase.Level)
        {
            // Reset du volume musique (on peut venir d'un level ducké)
            AudioManager.Instance.SetMusicVolumeMultiplier(1f, 1f);

            MusicId id = GetMusicForPhase(targetPhase);

            if (id != MusicId.None)
                AudioManager.Instance.PlayMusic(id, macroMusicFadeOutSec, macroMusicFadeInSec);
            else
                AudioManager.Instance.StopMusic(macroMusicFadeOutSec);
        }
    }
}