using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Contrôleur dédié au briefing de niveau (Level Intro Overlay).
/// - Reçoit LevelData + PhasePlanInfo[].
/// - Affiche IntroLevelUI.
/// - Gère les boutons Play / Back.
/// </summary>
public class LevelBriefingController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private IntroLevelUI introLevelUI;

    [Header("Navigation")]
    [SerializeField] private string titleSceneName = "Title";

    // Valeurs runtime de Hull injectées par le LevelManager
    private int runtimeHull = -1;
    private int runtimeMaxHull = -1;

    /// <summary>
    /// Appelé par le LevelManager quand il reçoit une mise à jour de Hull.
    /// Permet d'afficher la valeur courante (ex: 9 / 10) dans l'intro.
    /// </summary>
    public void SetShipRuntimeHull(int currentHull, int maxHull)
    {
        runtimeHull = Mathf.Max(-1, currentHull);
        runtimeMaxHull = Mathf.Max(-1, maxHull);

        if (introLevelUI != null)
        {
            introLevelUI.SetShipRuntimeHull(runtimeHull, runtimeMaxHull);
        }
    }

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
            Debug.LogError("[LevelBriefingController] IntroLevelUI non assigné. Briefing obligatoire mais UI manquante.");
            onPlay?.Invoke();
            return;
        }

        // On s'assure que l'IntroLevelUI possède les bonnes valeurs de hull
        if (runtimeHull >= 0 && runtimeMaxHull > 0)
        {
            introLevelUI.SetShipRuntimeHull(runtimeHull, runtimeMaxHull);
        }

        introLevelUI.Show(
            levelData,
            phasePlanInfos,
            onPlay: () =>
            {
                introLevelUI.Hide();
                onPlay?.Invoke();
            },
            onBack: () =>
            {
                if (RunConfig.Instance != null)
                    RunConfig.Instance.SkipTitleIntroOnce = true;

                if (!string.IsNullOrEmpty(titleSceneName))
                {
                    SceneManager.LoadScene(titleSceneName);
                }

                onBack?.Invoke();
            }
        );
    }
}
