using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class EndLevelUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LevelManager levelManagerRef;
    [SerializeField] private bool autoBindLevelManager = true;
    // levelManagerRef : utilisé pour s'abonner à l'event OnEndComputed et récupérer les objectifs secondaires.
    // autoBindLevelManager : si true et ref vide, on cherche automatiquement un LevelManager dans la scène.

    [Header("Root")]
    [SerializeField] private GameObject endLevelOverlay;

    [Header("Header")]
    [SerializeField] private TMP_Text levelIdText;
    [SerializeField] private TMP_Text worldLevelText;
    [SerializeField] private TMP_Text titleText;

    [Header("Panels")]
    [SerializeField] private Transform panelContainer;
    // endLevelOverlay : overlay global de fin de niveau (fond + panel stats).
    // panelContainer : conteneur principal des stats (permet de masquer les stats sans éteindre l'overlay complet).

    [Header("Stats")]
    [SerializeField] private GameObject scoreTitlePanel;
    [SerializeField] private GameObject scoreTitleSeparator;
    [SerializeField] private LineEntryFinalUI rawScoreLine;
    // rawScoreLine : panel RawScore_Panel avec label + valeur, basé sur LineEntryFinalUI.

    [Header("Animated score")]
    [SerializeField] private AnimatedIntText rawScoreAnimated;
    [SerializeField] private AnimatedIntText goalsBonusAnimated;
    [SerializeField] private AnimatedIntText combosBonusAnimated;
    [SerializeField] private AnimatedIntText finalScoreAnimated;
    // rawScoreAnimated : animation pour la valeur de score brut (associée à rawScoreLine.value).
    // goalsBonusAnimated : animation pour la ligne "Bonus objectifs" (totalGoalsLine).
    // combosBonusAnimated : animation pour la ligne "Combos cachés" (totalCombosLine).
    // finalScoreAnimated : animation pour le score final (finalScoreLine.value).

    [Header("Goals")]
    [SerializeField] private GameObject goalsTitlePanel;
    [SerializeField] private GameObject goalTitleSeparator;
    [SerializeField] private RectTransform goalsContent;
    [SerializeField] private GameObject goalLinePrefab;
    // goalsTitlePanel : panel titre "Objectifs", affiché entre les stats et la liste d'objectifs.
    // goalsContent : parent pour les lignes d'objectifs (principal + secondaires).
    // goalLinePrefab : prefab d'une ligne générique (LineEntryUI : label + value).

    [Header("Goals summary")]
    [SerializeField] private LineEntryFinalUI totalGoalsLine;
    // totalGoalsLine : ligne récapitulative "Bonus objectifs" (LineEntryFinalUI),
    // affichant la somme du bonus principal + secondaires réussis.

    [Header("Combos style")]
    [SerializeField] private FinalComboStyleProvider finalComboStyle;
    // finalComboStyle : fournit les libellés lisibles pour les combos finaux
    // à partir de leurs identifiants techniques (PerfectRun, WhiteMaster, etc.).

    [Header("Combos")]
    [SerializeField] private GameObject combosTitlePanel;
    [SerializeField] private GameObject combosTitleSeparator;
    [SerializeField] private RectTransform combosContent;
    [SerializeField] private GameObject combosLinePrefab;
    // combosTitlePanel : panel titre "Combos cachés", affiché avant la liste des combos.
    // combosContent : parent pour les lignes de combos.
    // combosLinePrefab : prefab d'une ligne générique (LineEntryUI : label + value).

    [Header("Combos summary")]
    [SerializeField] private LineEntryFinalUI totalCombosLine;
    // totalCombosLine : ligne récapitulative "Combos cachés" (LineEntryFinalUI),
    // affichant la somme des points de combos cachés.

    [Header("Final score + ProgressBar")]
    [SerializeField] private LineEntryFinalUI finalScoreLine;
    //[SerializeField] private GameObject finalScoreTitleSeparator;
    // Ligne "Score final" (label + valeur).

    [Header("Medals")]
    [SerializeField] private EndLevelMedalsUI medalsUI;

    [SerializeField] private FinalScoreBarUI finalScoreBar;
    // Contrôleur logique de la barre finale (wrapp SegmentedFinalScoreBarUI).

    private int progressMax = 0;
    // progressMax : valeur de score correspondant à une barre remplie à 100% (Gold + marge, par ex. +20%).

    // Seuils de médailles (récupérés depuis le LevelData au démarrage de la séquence).
    private int bronzeThreshold = 0;
    private int silverThreshold = 0;
    private int goldThreshold = 0;

    // Flags pour ne déclencher chaque milestone qu'une seule fois.
    private bool bronzePassed = false;
    private bool silverPassed = false;
    private bool goldPassed = false;

    // Score final calculé à la fin de la séquence (après stats + objectifs + combos).
    private int finalScore = 0;
 
    [Header("Sequence")]
    [SerializeField] private float lineDelay = 0.7f;
    [SerializeField] private float failDelaySec = 1f;
    // lineDelay : délai entre l'apparition de chaque bloc (stats, objectifs, etc.).
    // failDelaySec : délai après la révélation de l'objectif principal en cas d'échec avant d'envoyer OnSequenceFailed.

    [Header("Events")]
    public UnityEvent OnSequenceFailed;
    public UnityEvent OnVictory;
    // OnSequenceFailed : appelé quand l'objectif principal n'est pas atteint et que la séquence d'affichage d'échec est terminée.
    // OnVictory : appelé quand la séquence de victoire est complète.

    // Gris neutre pour l’échec de l’objectif principal ou des objectifs secondaires.
    private static readonly Color GrayText = new Color(0.6f, 0.6f, 0.6f, 1f);

    // Résultats d'objectifs secondaires récupérés auprès du LevelManager en fin de niveau.
    private List<SecondaryObjectiveResult> secondaryResults;

  
    // =====================================================================
    // CYCLE UNITY
    // =====================================================================

    private void Awake()
    {
        // Option de binding automatique du LevelManager si la référence n'est pas renseignée dans l'Inspector.
        if (autoBindLevelManager && levelManagerRef == null)
            levelManagerRef = FindFirstObjectByType<LevelManager>();

        // On s'abonne à l'événement de fin de niveau pour recevoir les stats et lancer la séquence de fin.
        if (levelManagerRef != null)
            levelManagerRef.OnEndComputed.AddListener(HandleEndComputed);
    }

    private void OnDestroy()
    {
        // On se désabonne proprement pour éviter les références invalides au moment de la destruction.
        if (levelManagerRef != null)
            levelManagerRef.OnEndComputed.RemoveListener(HandleEndComputed);
    }

    // =====================================================================
    // ENTRY POINTS
    // =====================================================================

    // Callback appelé par le LevelManager lorsque les stats de fin de niveau sont prêtes.
    private void HandleEndComputed(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        // On récupère, si elles existent, les infos d'objectifs secondaires calculées par le LevelManager.
        secondaryResults = null;
        if (levelManagerRef != null)
            secondaryResults = levelManagerRef.GetSecondaryObjectiveResults();

        ShowSequenced(stats, levelData, mainObj);
    }

    // Méthode publique pour lancer la séquence de fin de niveau (révélation progressive).
    public void ShowSequenced(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        StopAllCoroutines();
        StartCoroutine(RevealRoutine(stats, levelData, mainObj));
    }

    // Cache l’overlay de fin (utile en cas de retour menu, retry, etc.).
    public void Hide()
    {
        StopAllCoroutines();
        if (endLevelOverlay)
            endLevelOverlay.SetActive(false);
    }

    private void SetupHeader(LevelData levelData)
    {
        if (levelData == null)
            return;

        // Identifiant du niveau (ex : "W1-L1")
        if (levelIdText != null)
        {
            string id = string.IsNullOrEmpty(levelData.LevelID) ? "-" : levelData.LevelID;
            levelIdText.text = id;
        }

        // Monde / secteur (ex : "ASTEROIDS FIELD XF-2B" ou "WORLD 1")
        if (worldLevelText != null)
        {
            string world = string.IsNullOrEmpty(levelData.World) ? "" : levelData.World;
            worldLevelText.text = world;
        }

        // Titre lisible du niveau (ex : "FIRST CONTACT")
        if (titleText != null)
        {
            string title = string.IsNullOrEmpty(levelData.Title) ? "" : levelData.Title;
            titleText.text = title;
        }
    }

    // =====================================================================
    // SEQUENCE PRINCIPALE (DANS L'ORDRE D'AFFICHAGE)
    // =====================================================================

    private IEnumerator RevealRoutine(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        // 0. Activation overlay global + nettoyage des anciennes lignes.
        if (endLevelOverlay)
            endLevelOverlay.SetActive(true);

        if (goalsContent)
            ClearChildren(goalsContent);
        if (combosContent)
            ClearChildren(combosContent);

        if (goalsTitlePanel != null)
            goalsTitlePanel.SetActive(false);
        if (totalGoalsLine != null)
            totalGoalsLine.gameObject.SetActive(false);

        if (combosTitlePanel != null)
            combosTitlePanel.SetActive(false);
        if (totalCombosLine != null)
            totalCombosLine.gameObject.SetActive(false);

        // Header (ID / Monde / Titre)
        SetupHeader(levelData);

        // ------------------------------------------------------------------
        // Score final + barre : état de base (0 + barre vide)
        // ------------------------------------------------------------------

        progressMax = 0;

        // Reset des seuils et des flags de milestones
        bronzeThreshold = 0;
        silverThreshold = 0;
        goldThreshold = 0;

        bronzePassed = false;
        silverPassed = false;
        goldPassed = false;

        // Score final texte = 0
        if (finalScoreAnimated != null)
        {
            finalScoreAnimated.SetInstant(0);
        }
        else if (finalScoreLine != null && finalScoreLine.value != null)
        {
            finalScoreLine.value.text = "0";
        }

        // La barre finale sera remise à zéro après configuration (plus bas)
        finalScore = 0;


        // ------------------------------------------------------------------
        // Récupération des seuils de médailles depuis le LevelData
        // ------------------------------------------------------------------
        if (levelData != null && levelData.ScoreGoals != null)
        {
            for (int i = 0; i < levelData.ScoreGoals.Length; i++)
            {
                var g = levelData.ScoreGoals[i];
                if (g == null)
                    continue;

                if (g.Type == "Bronze")
                    bronzeThreshold = g.Points;
                else if (g.Type == "Silver")
                    silverThreshold = g.Points;
                else if (g.Type == "Gold")
                    goldThreshold = g.Points;
            }
        }

        if (goldThreshold > 0)
        {
            progressMax = Mathf.RoundToInt(goldThreshold * 1.2f);

        }
        else
        {
            progressMax = 0;
        }

        // Configuration de la barre finale à partir des thresholds.
        // (ProgressMax = Gold * 1.2f, même logique qu'avant.)
        if (finalScoreBar != null && progressMax > 0)
        {
            finalScoreBar.Configure(bronzeThreshold, silverThreshold, goldThreshold, progressMax);
        }
        else if (finalScoreBar != null)
        {
            // Cas extrême : pas de Gold configuré, on évite les divisions par zéro.
            finalScoreBar.Configure(0, 0, 0, 1);
        }

        if (finalScoreBar != null)
        {
            finalScoreBar.ResetInstant();
        }


        // Score cumulé utilisé pour le "Score final" + barre.
        int runningScore = 0;

        // ===================================================================================
        // 1) BLOC STATS : Raw Score (séquencé)
        // ===================================================================================

        if (scoreTitlePanel != null)
        {
            scoreTitlePanel.SetActive(true);
            scoreTitleSeparator.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(lineDelay);
        // Ligne 1 : Score brut (panel final + affichage 0)
        if (rawScoreLine != null)
        {
            rawScoreLine.gameObject.SetActive(true);

            if (rawScoreAnimated != null)
            {
                rawScoreAnimated.SetInstant(0);
            }
            else if (rawScoreLine.value != null)
            {
                rawScoreLine.value.text = "0";
            }
        }
        yield return new WaitForSecondsRealtime(lineDelay);

        // Animation du Raw Score 0 -> RawScore
        if (rawScoreAnimated != null)
        {
            rawScoreAnimated.AnimateTo(stats.RawScore);
            // On attend que l'animation locale soit terminée.
            yield return StartCoroutine(WaitForLocalLineAnimation(rawScoreAnimated));
        }
        else if (rawScoreLine != null && rawScoreLine.value != null)
        {
            rawScoreLine.value.text = stats.RawScore.ToString("N0");
        }

        yield return new WaitForSecondsRealtime(lineDelay);

        // Mise à jour du score final + barre après le score brut.
        int previousScore = runningScore;
        runningScore = stats.RawScore;

        HandleScoreMilestones(previousScore, runningScore);
        RefreshFinalScoreUI(runningScore, true);
        yield return StartCoroutine(WaitForFinalScoreAnimations());


        // ===================================================================================
        // 2) TITRE "OBJECTIFS"
        // ===================================================================================

        if (goalsTitlePanel != null)
        {
            goalsTitlePanel.SetActive(true);
            goalTitleSeparator.SetActive(true);
        }
            

        yield return new WaitForSecondsRealtime(lineDelay);

        // ===================================================================================
        // 3) OBJECTIF PRINCIPAL
        // ===================================================================================

        AddMainObjectiveLine(mainObj);
        yield return new WaitForSecondsRealtime(lineDelay);

        // Cas échec : on s'arrête ici, après un court délai.
        // On fige quand même un "score de niveau" pour la défaite (ici : le score brut).
        if (!mainObj.Achieved)
        {
            // runningScore vaut déjà stats.RawScore à ce stade.
            finalScore = runningScore;

            yield return new WaitForSecondsRealtime(failDelaySec);
            OnSequenceFailed?.Invoke();
            yield break;
        }


        // ===================================================================================
        // 4) OBJECTIFS SECONDAIRES (si présents, un par un avec délai)
        // ===================================================================================

        if (secondaryResults != null && secondaryResults.Count > 0)
        {
            foreach (var obj in secondaryResults)
            {
                AddSecondaryObjectiveLine(obj);
                yield return new WaitForSecondsRealtime(lineDelay);
            }
        }
        else
        {
            // Petit délai même s'il n'y a pas de secondaires, pour garder un rythme cohérent.
            yield return new WaitForSecondsRealtime(lineDelay);
        }

        // ===================================================================================
        // 5) TOTAL DES BONUS D'OBJECTIFS (Bonus objectifs)
        // ===================================================================================

        int totalGoalsBonus = ComputeTotalGoalsBonus(mainObj, secondaryResults);

        if (totalGoalsLine != null)
        {
            totalGoalsLine.gameObject.SetActive(true);

            if (goalsBonusAnimated != null)
            {
                goalsBonusAnimated.SetInstant(0);
            }
            else if (totalGoalsLine.value != null)
            {
                totalGoalsLine.value.text = "0";
            }
        }

        yield return new WaitForSecondsRealtime(lineDelay);

        // Animation du total des objectifs 0 -> totalGoalsBonus
        if (goalsBonusAnimated != null)
        {
            goalsBonusAnimated.AnimateTo(totalGoalsBonus);
            // On attend que la ligne locale ait fini son animation.
            yield return StartCoroutine(WaitForLocalLineAnimation(goalsBonusAnimated));
        }
        else if (totalGoalsLine != null && totalGoalsLine.value != null)
        {
            totalGoalsLine.value.text = totalGoalsBonus.ToString("N0");
        }

        yield return new WaitForSecondsRealtime(lineDelay);

        previousScore = runningScore;
        runningScore += totalGoalsBonus;

        HandleScoreMilestones(previousScore, runningScore);
        RefreshFinalScoreUI(runningScore, true);
        yield return StartCoroutine(WaitForFinalScoreAnimations());


        // ===================================================================================
        // 6) COMBOS CACHES
        // ===================================================================================

        var combos = stats.Combos;

        if (combosTitlePanel != null)
        {
            combosTitlePanel.SetActive(true);
            combosTitleSeparator.SetActive(true);
        }
            

        yield return new WaitForSecondsRealtime(lineDelay);

        int totalComboPoints = 0;

        if (combos != null && combos.Count > 0)
        {
            for (int i = 0; i < combos.Count; i++)
            {
                var comboData = combos[i];

                var go = Object.Instantiate(combosLinePrefab, combosContent);
                var ui = go.GetComponent<LineEntryUI>();
                if (ui != null)
                {
                    string displayLabel = comboData.Label;

                    if (finalComboStyle != null)
                        displayLabel = finalComboStyle.GetLabel(comboData.Label);

                    ui.label.text = displayLabel;
                    ui.value.text = comboData.Total.ToString("N0");
                }
                totalComboPoints += comboData.Total;

                yield return new WaitForSecondsRealtime(lineDelay);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(lineDelay);
        }

        if (totalCombosLine != null)
        {
            totalCombosLine.gameObject.SetActive(true);

            if (totalCombosLine.label != null)
                totalCombosLine.label.text = "Score";

            if (combosBonusAnimated != null)
            {
                combosBonusAnimated.SetInstant(0);
            }
            else if (totalCombosLine.value != null)
            {
                totalCombosLine.value.text = "0";
            }
        }
        yield return new WaitForSecondsRealtime(lineDelay);

        // Animation du total des combos 0 -> totalComboPoints
        if (combosBonusAnimated != null)
        {
            combosBonusAnimated.AnimateTo(totalComboPoints);
            // On attend que la ligne de combos ait terminé son animation.
            yield return StartCoroutine(WaitForLocalLineAnimation(combosBonusAnimated));
        }
        else if (totalCombosLine != null && totalCombosLine.value != null)
        {
            totalCombosLine.value.text = totalComboPoints.ToString("N0");
        }

        yield return new WaitForSecondsRealtime(lineDelay);

        previousScore = runningScore;
        runningScore += totalComboPoints;

        HandleScoreMilestones(previousScore, runningScore);
        RefreshFinalScoreUI(runningScore, true);
        yield return StartCoroutine(WaitForFinalScoreAnimations());

        // Score final (brut + objectifs + combos) utilisé pour la progression.
        finalScore = runningScore;

        // ===================================================================================
        // 7) FIN DE SÉQUENCE : VICTOIRE
        // ===================================================================================

        OnVictory?.Invoke();
    }

    // =====================================================================
    // OBJECTIFS (LIGNES)
    // =====================================================================

    private void AddMainObjectiveLine(MainObjectiveResult mainObj)
    {
        if (!goalsContent || !goalLinePrefab)
            return;

        var go = Object.Instantiate(goalLinePrefab, goalsContent);
        var ui = go.GetComponent<LineEntryUI>();
        if (!ui)
            return;

        ui.label.text = mainObj.Text;
        ui.value.text = mainObj.BonusApplied.ToString();

        var color = mainObj.Achieved ? Color.white : GrayText;
        ui.label.color = color;
        ui.value.color = color;
    }

    private void AddSecondaryObjectiveLine(SecondaryObjectiveResult obj)
    {
        if (!goalsContent || !goalLinePrefab)
            return;

        var go = Object.Instantiate(goalLinePrefab, goalsContent);
        var ui = go.GetComponent<LineEntryUI>();
        if (!ui)
            return;

        ui.label.text = obj.Text;

        int displayedScore = obj.Achieved ? obj.AwardedScore : 0;
        ui.value.text = displayedScore.ToString();

        var color = obj.Achieved ? Color.white : GrayText;
        ui.label.color = color;
        ui.value.color = color;
    }

    private int ComputeTotalGoalsBonus(MainObjectiveResult mainObj, List<SecondaryObjectiveResult> secondary)
    {
        int total = 0;

        if (mainObj.Achieved && mainObj.BonusApplied > 0)
            total += mainObj.BonusApplied;

        if (secondary != null)
        {
            for (int i = 0; i < secondary.Count; i++)
            {
                var obj = secondary[i];
                if (obj.Achieved && obj.AwardedScore > 0)
                    total += obj.AwardedScore;
            }
        }

        return total;
    }

    // =====================================================================
    // UTILITAIRES
    // =====================================================================

    private static void ClearChildren(RectTransform parent)
    {
        if (!parent)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    public void HideStatsPanel()
    {
        if (panelContainer != null)
            panelContainer.gameObject.SetActive(false);
    }

    private void RefreshFinalScoreUI(int currentScore, bool animate)
    {
        // Texte du score final
        if (finalScoreAnimated != null)
        {
            if (animate)
                finalScoreAnimated.AnimateTo(currentScore);
            else
                finalScoreAnimated.SetInstant(currentScore);
        }
        else if (finalScoreLine != null && finalScoreLine.value != null)
        {
            finalScoreLine.value.text = currentScore.ToString("N0");
        }

        // Barre finale segmentée (score / progressMax)
        if (finalScoreBar != null && progressMax > 0)
        {
            finalScoreBar.SetScore(currentScore);
        }
    }

    private IEnumerator WaitForFinalScoreAnimations()
    {
        while (true)
        {
            bool stillAnimating = false;

            if (finalScoreAnimated != null && finalScoreAnimated.IsAnimating)
                stillAnimating = true;

            // On ne bloque plus sur la barre finale : elle a sa propre anim step-by-step,
            // mais on ne synchronise pas finement sa fin ici.

            if (!stillAnimating)
                break;

            yield return null;
        }
    }

    private IEnumerator WaitForLocalLineAnimation(AnimatedIntText anim)
    {
        if (anim == null)
            yield break;

        while (anim.IsAnimating)
            yield return null;
    }

    /// <summary>
    /// Vérifie si le score vient de franchir un ou plusieurs seuils (Bronze / Silver / Gold).
    /// Déclenche un log + une petite animation de tick au moment du franchissement.
    /// </summary>
    private void HandleScoreMilestones(int previousScore, int newScore)
    {
        // Bronze
        if (!bronzePassed && bronzeThreshold > 0 &&
            previousScore < bronzeThreshold && newScore >= bronzeThreshold)
        {
            bronzePassed = true;
           // OnScoreMilestoneReached("Bronze", bronzeThreshold, tickBronze);
        }

        // Silver
        if (!silverPassed && silverThreshold > 0 &&
            previousScore < silverThreshold && newScore >= silverThreshold)
        {
            silverPassed = true;
           // OnScoreMilestoneReached("Silver", silverThreshold, tickSilver);
        }

        // Gold
        if (!goldPassed && goldThreshold > 0 &&
            previousScore < goldThreshold && newScore >= goldThreshold)
        {
            goldPassed = true;
            //OnScoreMilestoneReached("Gold", goldThreshold, tickGold);
        }
    }

    /// <summary>
    /// Retourne le score final calculé à la fin de la séquence de victoire.
    /// Si la séquence est interrompue (défaite), la valeur restera 0.
    /// </summary>
    public int GetFinalScore()
    {
        return finalScore;
    }


}
// Résultat de l'objectif principal calculé en fin de niveau.
[System.Serializable]
public struct MainObjectiveResult
{
    public string Text;
    public int ThresholdPct;
    public int Required;
    public int Collected;
    public bool Achieved;
    public int BonusApplied;
}
