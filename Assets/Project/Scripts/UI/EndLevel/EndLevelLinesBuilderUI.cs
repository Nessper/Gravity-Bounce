using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EndLevelStats;

/// <summary>
/// Construit et gere les listes de lignes Goals et Bonus de fin de niveau :
/// - Clear des conteneurs
/// - Creation des lignes Goals / Bonus
/// - Preparation des lignes et TotalLines cachees (alpha 0)
/// - Reveal progressif des lignes deja presentes
/// - Construction instantanee pour le skip
/// - Calcul des totaux Goals / Bonus
///
/// IMPORTANT :
/// - Ne gere pas la ceremony globale.
/// - Ne gere pas le score final ni la progress bar.
/// - La source de verite affichee pour la section Bonus est EndLevelStats.BonusLines.
/// - Les objets restent actifs et sont pilotes via CanvasGroup.alpha.
/// </summary>
public class EndLevelLinesBuilderUI : MonoBehaviour
{
    [Header("Goals")]
    [SerializeField] private RectTransform goalsContent;
    [SerializeField] private GameObject goalLinePrefab;
    [SerializeField] private LineEntryFinalUI totalGoalsLine;

    [Header("Bonus")]
    [SerializeField] private RectTransform bonusContent;
    [SerializeField] private GameObject bonusLinePrefab;
    [SerializeField] private LineEntryFinalUI totalBonusLine;

    [Header("Combos style")]
    [SerializeField] private FinalComboStyleProvider finalComboStyle;

    [Header("Style")]
    [SerializeField] private Color grayText = new Color(0.6f, 0.6f, 0.6f, 1f);

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
        HideTotals();
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

    public void HideTotals()
    {
        if (totalGoalsLine != null)
        {
            totalGoalsLine.gameObject.SetActive(true);
            SetObjectAlpha(totalGoalsLine.gameObject, 0f);

            if (totalGoalsLine.value != null)
                totalGoalsLine.value.text = "0";
        }

        if (totalBonusLine != null)
        {
            totalBonusLine.gameObject.SetActive(true);
            SetObjectAlpha(totalBonusLine.gameObject, 0f);

            if (totalBonusLine.label != null)
                totalBonusLine.label.text = "Score";

            if (totalBonusLine.value != null)
                totalBonusLine.value.text = "0";
        }
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

    // ----------------------------------------------------------
    // GOALS LINES
    // ----------------------------------------------------------

    public List<GameObject> BuildGoalsHidden(MainObjectiveResult mainObj, List<SecondaryObjectiveResult> secondary)
    {
        List<GameObject> lines = new List<GameObject>();

        if (goalsContent == null || goalLinePrefab == null)
            return lines;

        GameObject mainLine = CreateMainObjectiveLine(mainObj, hidden: true);
        if (mainLine != null)
            lines.Add(mainLine);

        if (secondary != null)
        {
            for (int i = 0; i < secondary.Count; i++)
            {
                GameObject line = CreateSecondaryObjectiveLine(secondary[i], hidden: true);
                if (line != null)
                    lines.Add(line);
            }
        }

        return lines;
    }

    private GameObject CreateMainObjectiveLine(MainObjectiveResult mainObj, bool hidden)
    {
        if (goalsContent == null || goalLinePrefab == null)
            return null;

        GameObject go = Object.Instantiate(goalLinePrefab, goalsContent);
        LineEntryUI ui = go.GetComponent<LineEntryUI>();
        if (ui != null)
        {
            ui.label.text = mainObj.Text;
            ui.value.text = mainObj.BonusApplied.ToString();

            Color c = mainObj.Achieved ? Color.white : grayText;
            ui.label.color = c;
            ui.value.color = c;
        }

        SetObjectAlpha(go, hidden ? 0f : 1f);
        return go;
    }

    private GameObject CreateSecondaryObjectiveLine(SecondaryObjectiveResult obj, bool hidden)
    {
        if (goalsContent == null || goalLinePrefab == null)
            return null;

        GameObject go = Object.Instantiate(goalLinePrefab, goalsContent);
        LineEntryUI ui = go.GetComponent<LineEntryUI>();
        if (ui != null)
        {
            ui.label.text = obj.Text;

            int displayedScore = obj.Achieved ? obj.AwardedScore : 0;
            ui.value.text = displayedScore.ToString();

            Color c = obj.Achieved ? Color.white : grayText;
            ui.label.color = c;
            ui.value.color = c;
        }

        SetObjectAlpha(go, hidden ? 0f : 1f);
        return go;
    }

    public void PrepareGoalsTotalLineHidden()
    {
        if (totalGoalsLine == null)
            return;

        totalGoalsLine.gameObject.SetActive(true);

        if (totalGoalsLine.value != null)
            totalGoalsLine.value.text = "0";

        SetObjectAlpha(totalGoalsLine.gameObject, 0f);
    }

    public IEnumerator RevealGoalsTotalLine(float fadeDuration)
    {
        if (totalGoalsLine == null)
            yield break;

        yield return RevealLine(totalGoalsLine.gameObject, fadeDuration);
    }

    // ----------------------------------------------------------
    // BONUS LINES
    // ----------------------------------------------------------

    public List<GameObject> BuildBonusHidden(EndLevelStats stats)
    {
        List<GameObject> lines = new List<GameObject>();
        lastBonusPoints = 0;

        if (bonusContent == null || bonusLinePrefab == null)
            return lines;

        List<EndLevelBonusLine> bonusLines = stats != null ? stats.BonusLines : null;
        if (bonusLines == null || bonusLines.Count == 0)
            return lines;

        for (int i = 0; i < bonusLines.Count; i++)
        {
            EndLevelBonusLine lineData = bonusLines[i];
            GameObject line = CreateBonusLine(lineData, hidden: true);
            if (line != null)
                lines.Add(line);

            lastBonusPoints += lineData.Total;
        }

        return lines;
    }

    private GameObject CreateBonusLine(EndLevelBonusLine lineData, bool hidden)
    {
        if (bonusContent == null || bonusLinePrefab == null)
            return null;

        GameObject go = Object.Instantiate(bonusLinePrefab, bonusContent);
        LineEntryUI ui = go.GetComponent<LineEntryUI>();
        if (ui != null)
        {
            string displayLabel = ResolveBonusDisplayLabel(lineData.Label);
            ui.label.text = displayLabel;
            ui.value.text = lineData.Total.ToString("N0");
        }

        SetObjectAlpha(go, hidden ? 0f : 1f);
        return go;
    }

    public void PrepareBonusTotalLineHidden()
    {
        if (totalBonusLine == null)
            return;

        totalBonusLine.gameObject.SetActive(true);

        if (totalBonusLine.label != null)
            totalBonusLine.label.text = "Score";

        if (totalBonusLine.value != null)
            totalBonusLine.value.text = "0";

        SetObjectAlpha(totalBonusLine.gameObject, 0f);
    }

    public IEnumerator RevealBonusTotalLine(float fadeDuration)
    {
        if (totalBonusLine == null)
            yield break;

        yield return RevealLine(totalBonusLine.gameObject, fadeDuration);
    }

    // ----------------------------------------------------------
    // REVEAL HELPERS
    // ----------------------------------------------------------

    public IEnumerator RevealLine(GameObject lineObject, float fadeDuration)
    {
        if (lineObject == null)
            yield break;

        CanvasGroup cg = EnsureCanvasGroup(lineObject);
        if (cg == null)
            yield break;

        float from = cg.alpha;

        if (fadeDuration <= 0f)
        {
            cg.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            cg.alpha = Mathf.Lerp(from, 1f, t);
            yield return null;
        }

        cg.alpha = 1f;
    }

    // ----------------------------------------------------------
    // INSTANT BUILD FOR SKIP
    // ----------------------------------------------------------

    /// <summary>
    /// Construit instantanement toute la section Goals :
    /// - objectif principal
    /// - objectifs secondaires
    /// - total line visible
    /// </summary>
    public void BuildGoalsInstant(MainObjectiveResult mainObj, List<SecondaryObjectiveResult> secondary)
    {
        ClearGoals();

        List<GameObject> lines = BuildGoalsHidden(mainObj, secondary);
        for (int i = 0; i < lines.Count; i++)
            SetObjectAlpha(lines[i], 1f);

        int total = ComputeTotalGoalsBonus(mainObj, secondary);

        PrepareGoalsTotalLineHidden();
        SetObjectAlpha(totalGoalsLine != null ? totalGoalsLine.gameObject : null, 1f);

        if (totalGoalsLine != null && totalGoalsLine.value != null)
            totalGoalsLine.value.text = total.ToString();
    }

    /// <summary>
    /// Construit instantanement toute la section Bonus :
    /// - toutes les lignes BonusLines
    /// - total line visible
    /// </summary>
    public void BuildBonusInstant(EndLevelStats stats)
    {
        ClearBonusLines();

        List<GameObject> lines = BuildBonusHidden(stats);
        for (int i = 0; i < lines.Count; i++)
            SetObjectAlpha(lines[i], 1f);

        PrepareBonusTotalLineHidden();
        SetObjectAlpha(totalBonusLine != null ? totalBonusLine.gameObject : null, 1f);

        if (totalBonusLine != null && totalBonusLine.value != null)
            totalBonusLine.value.text = lastBonusPoints.ToString("N0");
    }

    // ----------------------------------------------------------
    // INTERNALS
    // ----------------------------------------------------------

    private string ResolveBonusDisplayLabel(string rawLabel)
    {
        if (string.IsNullOrEmpty(rawLabel))
            return string.Empty;

        if (finalComboStyle == null)
            return rawLabel;

        string styled = finalComboStyle.GetLabel(rawLabel);
        return string.IsNullOrEmpty(styled) ? rawLabel : styled;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null)
            return null;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();

        return cg;
    }

    private static void SetObjectAlpha(GameObject go, float alpha)
    {
        if (go == null)
            return;

        CanvasGroup cg = EnsureCanvasGroup(go);
        if (cg == null)
            return;

        cg.alpha = alpha;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }
}