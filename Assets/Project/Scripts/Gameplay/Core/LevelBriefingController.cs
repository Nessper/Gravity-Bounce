using System;
using UnityEngine;

/// <summary>
/// Controle l'affichage du briefing de niveau.
/// Responsabilites :
/// - Afficher l'UI de briefing (IntroLevelUI)
/// - Injecter les infos runtime du vaisseau (Hull/HullMax/Shield)
/// - Declencher la musique de briefing (MainBriefing) au moment ou le briefing est affiche
///
/// IMPORTANT :
/// - Ce controller ne declenche PAS la musique gameplay.
///   La musique gameplay doit etre declenchee par la sequence d'intro / le vrai debut de gameplay
///   (LevelIntroSequenceController / LevelManager), sinon tu vas perdre la synchro "a l'ecran".
/// </summary>
public class LevelBriefingController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("UI")]
    [SerializeField] private IntroLevelUI introLevelUI;

    [Header("Music")]
    [Tooltip("Si true, joue la musique de briefing quand l'UI briefing s'affiche.")]
    [SerializeField] private bool playBriefingMusic = true;

    [SerializeField] private float briefingMusicFadeOutSec = 0.8f;
    [SerializeField] private float briefingMusicFadeInSec = 0.8f;

    // Valeurs runtime de hull (cache) - fallback dev-only
    private int runtimeHull = -1;
    private int runtimeMaxHull = -1;

    private float runtimeShieldSeconds = -1f;

    private bool debugSkip;

    public void SetShipRuntimeHull(int currentHull, int maxHull)
    {
        runtimeHull = Mathf.Max(-1, currentHull);
        runtimeMaxHull = Mathf.Max(-1, maxHull);

        if (introLevelUI != null)
            introLevelUI.SetShipRuntimeHull(runtimeHull, runtimeMaxHull);
    }

    public void SetShipRuntimeShield(float shieldSeconds)
    {
        runtimeShieldSeconds = Mathf.Max(-1f, shieldSeconds);

        if (introLevelUI != null)
            introLevelUI.SetShipRuntimeShield(runtimeShieldSeconds);
    }

    public void SetDebugSkip(bool value)
    {
        debugSkip = value;
    }

    public void Show(
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData levelData,
        PhasePlanInfo[] phasePlanInfos,
        Action onPlay,
        Action onMenu = null)
    {
        // Debug skip : on ne montre rien, donc pas de musique briefing.
        if (debugSkip)
        {
            onPlay?.Invoke();
            return;
        }

        if (levelData == null)
        {
            Debug.LogError("[LevelBriefingController] LevelData est null. Briefing obligatoire mais data manquante.");
            onPlay?.Invoke();
            return;
        }

        if (introLevelUI == null)
        {
            Debug.LogError("[LevelBriefingController] IntroLevelUI non assigne. Briefing obligatoire mais UI manquante.");
            onPlay?.Invoke();
            return;
        }

        // ------------------------------------------------------------
        // HULL : source de verite = RunSessionState
        // ------------------------------------------------------------
        if (runSession != null)
        {
            introLevelUI.SetShipRuntimeHull(runSession.Hull, runSession.HullMax);
        }
        else if (runtimeHull >= 0 && runtimeMaxHull > 0)
        {
            // Fallback dev-only si runSession non assigne
            introLevelUI.SetShipRuntimeHull(runtimeHull, runtimeMaxHull);
        }

        // Shield (injecte par LevelManager via SetShipRuntimeShield)
        if (runtimeShieldSeconds >= 0f)
            introLevelUI.SetShipRuntimeShield(runtimeShieldSeconds);

        string worldName = levelMeta != null ? WorldCatalogService.GetWorldDisplayName(levelMeta.worldId) : "";
        string title = levelMeta != null ? levelMeta.title : "";

        // ------------------------------------------------------------
        // MUSIQUE BRIEFING
        // ------------------------------------------------------------
        if (playBriefingMusic && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(MusicId.MainBriefing, briefingMusicFadeOutSec, briefingMusicFadeInSec);
        }

        // ------------------------------------------------------------
        // UI
        // ------------------------------------------------------------
        introLevelUI.Show(
            levelData,
            phasePlanInfos,
            worldName,
            title,
            onStart: () =>
            {
                // On quitte le briefing.
                // NOTE : on ne change pas la musique ici.
                // La musique gameplay doit etre declenchee par la sequence d'intro / debut gameplay.
                introLevelUI.Hide();
                onPlay?.Invoke();
            },
            onMenu: () =>
            {
                onMenu?.Invoke();
            }
        );
    }
}