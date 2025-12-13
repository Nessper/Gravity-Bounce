using System;
using UnityEngine;

/// <summary>
/// Controleur dedie au briefing de niveau (overlay d intro).
/// - Recoit LevelData et PhasePlanInfo[] depuis le LevelManager.
/// - Transmet les informations a IntroLevelUI.
/// - Gere l affichage et le masquage de l overlay.
/// - Expose deux callbacks:
///   - onPlay : le joueur clique sur Start.
///   - onMenu : le joueur clique sur Menu.
/// Ce script ne change pas de scene et ne parle pas au GameFlow.
/// </summary>
public class LevelBriefingController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private IntroLevelUI introLevelUI;

    // Valeurs runtime de hull injectees par le LevelManager.
    private int runtimeHull = -1;
    private int runtimeMaxHull = -1;

    /// <summary>
    /// Appelle par le LevelManager quand le hull runtime change.
    /// Permet d afficher par exemple "9 / 10" dans le briefing.
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

    /// <summary>
    /// Affiche le briefing pour un niveau donne.
    /// - levelData : description du niveau (titre, objectifs, phases, etc.).
    /// - phasePlanInfos : infos de planning de spawn pour chaque phase.
    /// - onPlay : appele quand le joueur clique sur Start.
    /// - onMenu : appele quand le joueur clique sur Menu.
    /// Si les donnees ou l UI manquent, on appelle onPlay par securite.
    /// </summary>
    public void Show(
        LevelData levelData,
        PhasePlanInfo[] phasePlanInfos,
        Action onPlay,
        Action onMenu = null)
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

        // On s assure que l IntroLevelUI possede les bonnes valeurs de hull.
        if (runtimeHull >= 0 && runtimeMaxHull > 0)
        {
            introLevelUI.SetShipRuntimeHull(runtimeHull, runtimeMaxHull);
        }

        introLevelUI.Show(
            levelData,
            phasePlanInfos,
            onStart: () =>
            {
                // On masque l overlay avant de demarrer le niveau.
                introLevelUI.Hide();
                onPlay?.Invoke();
            },
            onMenu: () =>
            {
                BootRoot.GameFlow.GoToTitle();
            }
        );
    }
}
