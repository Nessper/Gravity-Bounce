using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construit et gère les listes de lignes Goals et Bonus de fin de niveau :
/// - Clear des conteneurs
/// - Ajout des lignes d'objectifs (principal + secondaires)
/// - Reveal des lignes bonus de cérémonie
/// - Calcul des totaux (goals, bonus)
///
/// IMPORTANT :
/// - Ne gère pas la cérémonie (timings globaux, victoire/défaite).
/// - Ne gère pas le score final ni la progress bar.
/// - La source de vérité affichée pour la section bonus est EndLevelStats.BonusLines.
/// </summary>
public class EndLevelLinesBuilderUI : MonoBehaviour
{
    // ----------------------------------------------------------
    // GOALS
    // ----------------------------------------------------------

    [Header("Goals")]
    [SerializeField] private RectTransform goalsContent;
    [SerializeField] private GameObject goalLinePrefab;
    [SerializeField] private LineEntryFinalUI totalGoalsLine;

    // ----------------------------------------------------------
    // BONUS
    // ----------------------------------------------------------

    [Header("Bonus")]
    [SerializeField] private RectTransform bonusContent;
    [SerializeField] private GameObject bonusLinePrefab;
    [SerializeField] private LineEntryFinalUI totalBonusLine;

    [Header("Combos style")]
    [SerializeField] private FinalComboStyleProvider finalComboStyle;

    // ----------------------------------------------------------
    // STYLE
    // ----------------------------------------------------------

    [Header("Style")]
    [SerializeField] private Color grayText = new Color(0.6f, 0.6f, 0.6f, 1f);

    // ----------------------------------------------------------
    // STATE
    // ----------------------------------------------------------

    private int lastBonusPoints = 0;

    public int LastBonusPoints
    {
        get { return lastBonusPoints; }
    }

    // ----------------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------------

    public void ClearAll()
    {
        ClearGoals();
        ClearBonusLines();
    }

    public void ClearGoals()
    {
        if (goalsContent != null)
            ClearChildren(goalsContent);
    }

    public void ClearBonusLines()
    {
        if (bonusContent != null)
            ClearChildren(bonusContent);

        lastBonusPoints = 0;
    }

    public void AddMainObjectiveLine(MainObjectiveResult mainObj)
    {
        if (goalsContent == null || goalLinePrefab == null)
            return;

        GameObject go = Object.Instantiate(goalLinePrefab, goalsContent);
        LineEntryUI ui = go.GetComponent<LineEntryUI>();
        if (ui == null)
            return;

        ui.label.text = mainObj.Text;
        ui.value.text = mainObj.BonusApplied.ToString();

        Color c = mainObj.Achieved ? Color.white : grayText;
        ui.label.color = c;
        ui.value.color = c;
    }

    public void AddSecondaryObjectiveLine(SecondaryObjectiveResult obj)
    {
        if (goalsContent == null || goalLinePrefab == null)
            return;

        GameObject go = Object.Instantiate(goalLinePrefab, goalsContent);
        LineEntryUI ui = go.GetComponent<LineEntryUI>();
        if (ui == null)
            return;

        ui.label.text = obj.Text;

        int displayedScore = obj.Achieved ? obj.AwardedScore : 0;
        ui.value.text = displayedScore.ToString();

        Color c = obj.Achieved ? Color.white : grayText;
        ui.label.color = c;
        ui.value.color = c;
    }

    public int ComputeTotalGoalsBonus(MainObjectiveResult mainObj, List<SecondaryObjectiveResult> secondary)
    {
        int total = 0;

        if (mainObj.Achieved && mainObj.BonusApplied > 0)
            total += mainObj.BonusApplied;

        if (secondary != null)
        {
            for (int i = 0; i < secondary.Count; i++)
            {
                SecondaryObjectiveResult obj = secondary[i];
                if (obj.Achieved && obj.AwardedScore > 0)
                    total += obj.AwardedScore;
            }
        }

        return total;
    }

    public void ShowGoalsTotalLine()
    {
        if (totalGoalsLine == null)
            return;

        totalGoalsLine.gameObject.SetActive(true);

        if (totalGoalsLine.value != null)
            totalGoalsLine.value.text = "0";
    }

    public void ShowBonusTotalLine()
    {
        if (totalBonusLine == null)
            return;

        totalBonusLine.gameObject.SetActive(true);

        if (totalBonusLine.label != null)
            totalBonusLine.label.text = "Score";

        if (totalBonusLine.value != null)
            totalBonusLine.value.text = "0";
    }

    /// <summary>
    /// Révèle les lignes bonus de fin de niveau, une par une,
    /// en appliquant un délai constant.
    ///
    /// IMPORTANT :
    /// - stats.BonusLines est la source de vérité de la section bonus.
    /// - Certaines lignes correspondent à d'anciens final combos :
    ///   dans ce cas, FinalComboStyleProvider peut convertir leur id
    ///   technique en label UI plus propre.
    /// - Les autres lignes (ex : modules) gardent simplement leur label brut.
    /// </summary>
    public IEnumerator RevealBonusLines(EndLevelStats stats, float lineDelaySec)
    {
        lastBonusPoints = 0;

        var bonusLines = (stats != null) ? stats.BonusLines : null;
        if (bonusLines == null || bonusLines.Count == 0)
        {
            if (lineDelaySec > 0f)
                yield return new WaitForSecondsRealtime(lineDelaySec);

            yield break;
        }

        for (int i = 0; i < bonusLines.Count; i++)
        {
            var lineData = bonusLines[i];

            GameObject go = Object.Instantiate(bonusLinePrefab, bonusContent);
            LineEntryUI ui = go.GetComponent<LineEntryUI>();
            if (ui != null)
            {
                string displayLabel = ResolveBonusDisplayLabel(lineData.Label);

                ui.label.text = displayLabel;
                ui.value.text = lineData.Total.ToString("N0");
            }

            lastBonusPoints += lineData.Total;

            if (lineDelaySec > 0f)
                yield return new WaitForSecondsRealtime(lineDelaySec);
        }
    }

    // ----------------------------------------------------------
    // INTERNALS
    // ----------------------------------------------------------

    /// <summary>
    /// Résout le label affiché pour une ligne bonus.
    ///
    /// Règle :
    /// - si FinalComboStyleProvider connaît ce label, on utilise
    ///   la version stylée ;
    /// - sinon, on conserve le label brut.
    ///
    /// Cela permet de garder les anciens ids techniques de final combos
    /// tout en affichant correctement les lignes déjà prêtes, comme
    /// les bonus modules.
    /// </summary>
    private string ResolveBonusDisplayLabel(string rawLabel)
    {
        if (string.IsNullOrEmpty(rawLabel))
            return string.Empty;

        if (finalComboStyle == null)
            return rawLabel;

        string styled = finalComboStyle.GetLabel(rawLabel);

        if (string.IsNullOrEmpty(styled))
            return rawLabel;

        return styled;
    }

    // ----------------------------------------------------------
    // UTILS
    // ----------------------------------------------------------

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    /// <summary>
    /// Construit instantanement toute la section Goals :
    /// - objectif principal
    /// - objectifs secondaires
    /// - ligne Total
    /// </summary>
    public void BuildGoalsInstant(MainObjectiveResult mainObj, List<SecondaryObjectiveResult> secondary)
    {
        ClearGoals();

        AddMainObjectiveLine(mainObj);

        if (secondary != null)
        {
            for (int i = 0; i < secondary.Count; i++)
                AddSecondaryObjectiveLine(secondary[i]);
        }

        ShowGoalsTotalLine();

        if (totalGoalsLine != null && totalGoalsLine.value != null)
        {
            int total = ComputeTotalGoalsBonus(mainObj, secondary);
            totalGoalsLine.value.text = total.ToString();
        }
    }

    /// <summary>
    /// Construit instantanement toute la section Bonus :
    /// - toutes les lignes BonusLines
    /// - ligne Total
    /// </summary>
    public void BuildBonusInstant(EndLevelStats stats)
    {
        ClearBonusLines();

        var bonusLines = (stats != null) ? stats.BonusLines : null;
        lastBonusPoints = 0;

        if (bonusLines != null && bonusLines.Count > 0)
        {
            for (int i = 0; i < bonusLines.Count; i++)
            {
                var lineData = bonusLines[i];

                GameObject go = Object.Instantiate(bonusLinePrefab, bonusContent);
                LineEntryUI ui = go.GetComponent<LineEntryUI>();
                if (ui != null)
                {
                    string displayLabel = ResolveBonusDisplayLabel(lineData.Label);
                    ui.label.text = displayLabel;
                    ui.value.text = lineData.Total.ToString("N0");
                }

                lastBonusPoints += lineData.Total;
            }
        }

        ShowBonusTotalLine();

        if (totalBonusLine != null && totalBonusLine.value != null)
            totalBonusLine.value.text = lastBonusPoints.ToString("N0");
    }
}