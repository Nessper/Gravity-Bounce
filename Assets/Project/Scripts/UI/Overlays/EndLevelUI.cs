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

    [Header("Panels")]
    [SerializeField] private GameObject endLevelOverlay;
    [SerializeField] private Transform panelContainer;
    // endLevelOverlay : overlay global de fin de niveau (fond + panel stats).
    // panelContainer : conteneur principal des stats (permet de masquer les stats sans éteindre l'overlay complet).

    [Header("Stats (bloc 1)")]
    [SerializeField] private LineEntryUI timeLine;
    [SerializeField] private LineEntryUI collectedLine;
    [SerializeField] private LineEntryUI lostLine;
    [SerializeField] private LineEntryFinalUI rawScoreLine;
    // timeLine : ligne affichant la durée du niveau (TimeElapsedSec).
    // collectedLine : ligne affichant le nombre de billes collectées.
    // lostLine : ligne affichant le nombre de billes perdues.
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
    // Ligne "Score final" (label + valeur).

    [SerializeField] private AnimatedFillImage finalScoreBarAnimated;
    // Composant d’animation de la barre de score final (fillAmount).

    [SerializeField] private RectTransform tickBronze;
    [SerializeField] private RectTransform tickSilver;
    [SerializeField] private RectTransform tickGold;
    // Ticks de repère pour les seuils Bronze / Silver / Gold.

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

    [Header("Best score")]
    [SerializeField] private TMP_Text bestScoreValue;
    // bestScoreValue : simple TMP_Text affichant le "Meilleur score" pour ce niveau.
    // Le label "Best" / "Record" sera géré dans l'UI, ici on ne s'occupe que de la valeur.

    // Score final calculé à la fin de la séquence (après stats + objectifs + combos).
    private int finalScore = 0;

    // Best score actuel pour ce niveau, fourni par un système externe (GameFlowController / SaveManager).
    private int initialBestScore = 0;
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

        // Barre = 0
        if (finalScoreBarAnimated != null)
        {
            finalScoreBarAnimated.SetInstant01(0f);
        }

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

            if (tickBronze != null && bronzeThreshold > 0)
                PlaceTick(tickBronze, bronzeThreshold);

            if (tickSilver != null && silverThreshold > 0)
                PlaceTick(tickSilver, silverThreshold);

            if (tickGold != null)
                PlaceTick(tickGold, goldThreshold);
        }
        else
        {
            progressMax = 0;
        }


        // Score cumulé utilisé pour le "Score final" + barre.
        int runningScore = 0;

        // ===================================================================================
        // 1) BLOC STATS : Time, Collected, Lost, Raw Score (séquencé)
        // ===================================================================================

        // Ligne 1 : Temps / durée de niveau (TimeElapsedSec)
        if (timeLine != null && timeLine.value != null)
        {
            int secs = stats.TimeElapsedSec;
            timeLine.value.text = secs.ToString();
            timeLine.gameObject.SetActive(true);
        }
        yield return new WaitForSecondsRealtime(lineDelay);

        // Ligne 2 : Billes collectées
        if (collectedLine != null && collectedLine.value != null)
        {
            collectedLine.value.text = stats.BallsCollected.ToString();
            collectedLine.gameObject.SetActive(true);
        }
        yield return new WaitForSecondsRealtime(lineDelay);

        // Ligne 3 : Billes perdues
        if (lostLine != null && lostLine.value != null)
        {
            lostLine.value.text = stats.BallsLost.ToString();
            lostLine.gameObject.SetActive(true);
        }
        yield return new WaitForSecondsRealtime(lineDelay);

        // Ligne 4 : Score brut (panel final + affichage 0)
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
            goalsTitlePanel.SetActive(true);

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
            combosTitlePanel.SetActive(true);

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

    private void PlaceTick(RectTransform tick, int threshold)
    {
        if (!tick || progressMax <= 0)
            return;

        float x = Mathf.Clamp01((float)threshold / progressMax);

        var a = tick.anchorMin;
        var b = tick.anchorMax;
        a.x = x;
        b.x = x;
        tick.anchorMin = a;
        tick.anchorMax = b;

        tick.anchoredPosition = Vector2.zero;
    }

    public void HideStatsPanel()
    {
        if (panelContainer != null)
            panelContainer.gameObject.SetActive(false);
    }

    private void RefreshFinalScoreUI(int currentScore, bool animate)
    {
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

        if (finalScoreBarAnimated != null && progressMax > 0)
        {
            float ratio = Mathf.Clamp01((float)currentScore / progressMax);

            if (animate)
                finalScoreBarAnimated.AnimateTo01(ratio);
            else
                finalScoreBarAnimated.SetInstant01(ratio);
        }
    }

    private IEnumerator WaitForFinalScoreAnimations()
    {
        while (true)
        {
            bool stillAnimating = false;

            if (finalScoreAnimated != null && finalScoreAnimated.IsAnimating)
                stillAnimating = true;

            if (finalScoreBarAnimated != null && finalScoreBarAnimated.IsAnimating)
                stillAnimating = true;

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
            OnScoreMilestoneReached("Bronze", bronzeThreshold, tickBronze);
        }

        // Silver
        if (!silverPassed && silverThreshold > 0 &&
            previousScore < silverThreshold && newScore >= silverThreshold)
        {
            silverPassed = true;
            OnScoreMilestoneReached("Silver", silverThreshold, tickSilver);
        }

        // Gold
        if (!goldPassed && goldThreshold > 0 &&
            previousScore < goldThreshold && newScore >= goldThreshold)
        {
            goldPassed = true;
            OnScoreMilestoneReached("Gold", goldThreshold, tickGold);
        }
    }


    /// <summary>
    /// Appelé lorsqu'un palier de médaille est franchi.
    /// Pour l'instant :
    /// - Log dans la console.
    /// - Petite animation de "pulse" sur le tick si présent.
    /// </summary>
    private void OnScoreMilestoneReached(string type, int threshold, RectTransform tick)
    {
        Debug.Log($"[EndLevelUI] Score milestone requested: {type}");

        if (finalScoreBarAnimated == null || progressMax <= 0 || tick == null)
            return;

        float targetRatio = Mathf.Clamp01((float)threshold / progressMax);

        StartCoroutine(WaitForBarThenPulse(type, targetRatio, tick));
    }


    private IEnumerator PulseTickRoutine(RectTransform tick)
    {
        if (tick == null)
            yield break;

        Vector3 baseScale = tick.localScale;
        Vector3 targetScale = baseScale * 1.6f; // plus violent pour être bien visible

        float durationUp = 0.15f;
        float durationDown = 0.15f;

        // Log pour vérifier que la coroutine tourne vraiment
        Debug.Log($"[EndLevelUI] PulseTickRoutine start on {tick.name}");

        // Phase montée
        float startTime = Time.unscaledTime;
        while (true)
        {
            float t = (Time.unscaledTime - startTime) / durationUp;
            if (t >= 1f)
                break;

            float eased = Mathf.SmoothStep(0f, 1f, t);
            tick.localScale = Vector3.Lerp(baseScale, targetScale, eased);
            yield return null;
        }

        tick.localScale = targetScale;

        // Phase descente
        startTime = Time.unscaledTime;
        while (true)
        {
            float t = (Time.unscaledTime - startTime) / durationDown;
            if (t >= 1f)
                break;

            float eased = Mathf.SmoothStep(0f, 1f, t);
            tick.localScale = Vector3.Lerp(targetScale, baseScale, eased);
            yield return null;
        }

        tick.localScale = baseScale;

        Debug.Log($"[EndLevelUI] PulseTickRoutine end on {tick.name}");
    }

    private IEnumerator WaitForBarThenPulse(string type, float targetRatio, RectTransform tick)
    {
        if (finalScoreBarAnimated == null)
            yield break;

        // On laisse un timeout de sécurité au cas où
        float timeout = Time.unscaledTime + 5f;

        while (Time.unscaledTime < timeout)
        {
            float current = finalScoreBarAnimated.GetDisplayed01();

            if (current >= targetRatio - 0.001f)
            {
                Debug.Log($"[EndLevelUI] Milestone {type} reached on bar at fill={current:0.00}");
                yield return PulseTickRoutine(tick);
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning($"[EndLevelUI] Milestone {type} timed out before bar reached targetRatio={targetRatio:0.00}");
    }

    /// <summary>
    /// Initialise l'affichage du "Meilleur score" pour ce niveau.
    /// À appeler depuis GameFlowController quand la scène est prête.
    /// </summary>
    public void SetupBestScore(int bestScore)
    {
        initialBestScore = bestScore < 0 ? 0 : bestScore;

        if (bestScoreValue != null)
        {
            bestScoreValue.gameObject.SetActive(true);
            bestScoreValue.text = initialBestScore.ToString("N0");
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
