using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construit et gère les listes de lignes Goals et Combos (bonus) :
/// - Clear des conteneurs
/// - Ajout des lignes (objectif principal, objectifs secondaires)
/// - Reveal des combos (avec délai par ligne)
/// - Calcul des totaux (goals, combos)
///
/// IMPORTANT :
/// - Ne gère pas la cérémonie (timings globaux, victoire/défaite).
/// - Ne gère pas le score final ni la progress bar.
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
    // COMBOS
    // ----------------------------------------------------------

    [Header("Combos")]
    [SerializeField] private RectTransform combosContent;
    [SerializeField] private GameObject combosLinePrefab;
    [SerializeField] private LineEntryFinalUI totalCombosLine;

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

    private int lastComboPoints = 0;

    public int LastComboPoints
    {
        get { return lastComboPoints; }
    }

    // ----------------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------------

    public void ClearAll()
    {
        ClearGoals();
        ClearCombos();
    }

    public void ClearGoals()
    {
        if (goalsContent != null)
            ClearChildren(goalsContent);
    }

    public void ClearCombos()
    {
        if (combosContent != null)
            ClearChildren(combosContent);

        lastComboPoints = 0;
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

    public void ShowCombosTotalLine()
    {
        if (totalCombosLine == null)
            return;

        totalCombosLine.gameObject.SetActive(true);

        if (totalCombosLine.label != null)
            totalCombosLine.label.text = "Score";

        if (totalCombosLine.value != null)
            totalCombosLine.value.text = "0";
    }

    /// <summary>
    /// Révèle les combos ligne par ligne, en appliquant un délai constant.
    /// Met à jour LastComboPoints.
    /// </summary>
    public IEnumerator RevealCombos(EndLevelStats stats, float lineDelaySec)
    {
        lastComboPoints = 0;

        // IMPORTANT : on conserve exactement ta logique "var" pour coller au type réel de stats.Combos
        var combos = (stats != null) ? stats.Combos : null;
        if (combos == null || combos.Count == 0)
        {
            if (lineDelaySec > 0f)
                yield return new WaitForSecondsRealtime(lineDelaySec);

            yield break;
        }

        for (int i = 0; i < combos.Count; i++)
        {
            var comboData = combos[i];

            GameObject go = Object.Instantiate(combosLinePrefab, combosContent);
            LineEntryUI ui = go.GetComponent<LineEntryUI>();
            if (ui != null)
            {
                string displayLabel = comboData.Label;
                if (finalComboStyle != null)
                    displayLabel = finalComboStyle.GetLabel(comboData.Label);

                ui.label.text = displayLabel;
                ui.value.text = comboData.Total.ToString("N0");
            }

            lastComboPoints += comboData.Total;

            if (lineDelaySec > 0f)
                yield return new WaitForSecondsRealtime(lineDelaySec);
        }
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
}
