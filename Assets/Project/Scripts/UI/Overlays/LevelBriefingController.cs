using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Contrôleur dédié au briefing de niveau (Level Intro Overlay).
/// - Reçoit LevelData + PhasePlanInfo[].
/// - Affiche IntroLevelUI.
/// - Gère les boutons Play / Back.
/// LevelManager ne connaît plus les détails d'affichage.
/// </summary>
public class LevelBriefingController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private IntroLevelUI introLevelUI;

    [Header("Navigation")]
    [SerializeField] private string titleSceneName = "Title";

    /// <summary>
    /// Affiche le briefing du niveau.
    /// Le briefing est considéré comme obligatoire :
    /// si data ou introLevelUI sont manquants, on log une erreur et on appelle quand même onPlay
    /// pour éviter de bloquer en dev.
    /// </summary>
    public void Show(
        LevelData levelData,
        PhasePlanInfo[] phasePlanInfos,
        Action onPlay,
        Action onBack = null)
    {
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

        introLevelUI.Show(
            levelData,
            phasePlanInfos,
            onPlay: () =>
            {
                // Le joueur confirme le depart -> on cache le briefing
                introLevelUI.Hide();

                // On notifie le caller (LevelManager) qu'on peut lancer la suite (intro narrative + countdown).
                onPlay?.Invoke();
            },
            onBack: () =>
            {
                // Evite de rejouer l'intro du Title
                if (RunConfig.Instance != null)
                    RunConfig.Instance.SkipTitleIntroOnce = true;

                // Retour propre au menu Title
                if (!string.IsNullOrEmpty(titleSceneName))
                {
                    SceneManager.LoadScene(titleSceneName);
                }

                // Callback optionnel vers le caller (pour stats ou flow meta plus tard)
                onBack?.Invoke();
            }
        );
    }
}
