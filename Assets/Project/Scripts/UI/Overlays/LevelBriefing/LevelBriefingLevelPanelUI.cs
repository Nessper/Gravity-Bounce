using TMPro;
using UnityEngine;

/// <summary>
/// Composant UI du bloc "Level Info" dans le briefing.
///
/// Responsabilites :
/// - afficher les donnees statiques du niveau
/// - afficher le texte de scan deja resolu par le controller parent
///
/// IMPORTANT :
/// - ne lit rien dans SaveManager
/// - ne lit rien dans RunSessionState
/// - ne connait pas ModuleRuntimeStats
/// - ne fait qu afficher ce qu on lui donne
/// </summary>
public class LevelBriefingLevelPanelUI : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TMP_Text levelTitleText;

    [Header("Main Objective")]
    [SerializeField] private TMP_Text mainObjectiveText;

    [Header("Bonus Objectives")]
    [SerializeField] private TMP_Text secondaryObjectiveText1;
    [SerializeField] private TMP_Text secondaryObjectiveText2;
    [SerializeField] private TMP_Text secondaryObjectiveText3;

    [Header("Score Targets")]
    [SerializeField] private TMP_Text bronzeScoreText;
    [SerializeField] private TMP_Text silverScoreText;
    [SerializeField] private TMP_Text goldScoreText;

    [Header("Scan Analysis")]
    [SerializeField] private TMP_Text scanText;

    [Header("Fallbacks")]
    [SerializeField] private string noMainObjectiveText = "-";
    [SerializeField] private string noScoreGoalText = "-";
    [SerializeField] private string defaultScanText = "no data available";

    public void Bind(
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData data,
        string resolvedScanText)
    {
        RefreshLevelTitle(levelMeta, data);
        RefreshMainObjective(data);
        RefreshSecondaryObjectives(data);
        RefreshScoreGoals(data);
        RefreshScanAnalysis(resolvedScanText);
    }

    public void Clear()
    {
        SetText(levelTitleText, string.Empty);
        SetText(mainObjectiveText, noMainObjectiveText);

        HideSecondaryObjectiveLine(secondaryObjectiveText1);
        HideSecondaryObjectiveLine(secondaryObjectiveText2);
        HideSecondaryObjectiveLine(secondaryObjectiveText3);

        SetText(bronzeScoreText, noScoreGoalText);
        SetText(silverScoreText, noScoreGoalText);
        SetText(goldScoreText, noScoreGoalText);

        SetText(scanText, defaultScanText);
    }

    private void RefreshLevelTitle(LevelCatalogService.LevelCatalogEntry levelMeta, LevelData data)
    {
        string text = string.Empty;

        if (levelMeta != null && !string.IsNullOrWhiteSpace(levelMeta.title))
            text = levelMeta.title;
        else if (data != null && !string.IsNullOrWhiteSpace(data.LevelID))
            text = data.LevelID;

        SetText(levelTitleText, text);
    }

    private void RefreshMainObjective(LevelData data)
    {
        string text = noMainObjectiveText;

        if (data != null &&
            data.MainObjective != null &&
            !string.IsNullOrWhiteSpace(data.MainObjective.Text))
        {
            text = data.MainObjective.Text;
        }

        SetText(mainObjectiveText, text);
    }

    private void RefreshSecondaryObjectives(LevelData data)
    {
        TMP_Text[] lines =
        {
            secondaryObjectiveText1,
            secondaryObjectiveText2,
            secondaryObjectiveText3
        };

        for (int i = 0; i < lines.Length; i++)
            HideSecondaryObjectiveLine(lines[i]);

        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        int writeIndex = 0;

        for (int i = 0; i < data.SecondaryObjectives.Length; i++)
        {
            if (writeIndex >= lines.Length)
                break;

            SecondaryObjectiveData objective = data.SecondaryObjectives[i];
            if (objective == null)
                continue;

            if (string.IsNullOrWhiteSpace(objective.UiText))
                continue;

            ShowSecondaryObjectiveLine(lines[writeIndex], objective.UiText);
            writeIndex++;
        }
    }

    private void RefreshScoreGoals(LevelData data)
    {
        int? bronze = null;
        int? silver = null;
        int? gold = null;

        if (data != null && data.ScoreGoals != null)
        {
            for (int i = 0; i < data.ScoreGoals.Length; i++)
            {
                ScoreGoalsData goal = data.ScoreGoals[i];
                if (goal == null || string.IsNullOrWhiteSpace(goal.Type))
                    continue;

                string type = goal.Type.Trim().ToLowerInvariant();

                if (type == "bronze")
                    bronze = goal.Points;
                else if (type == "silver")
                    silver = goal.Points;
                else if (type == "gold")
                    gold = goal.Points;
            }
        }

        SetText(bronzeScoreText, bronze.HasValue ? bronze.Value.ToString() : noScoreGoalText);
        SetText(silverScoreText, silver.HasValue ? silver.Value.ToString() : noScoreGoalText);
        SetText(goldScoreText, gold.HasValue ? gold.Value.ToString() : noScoreGoalText);
    }

    private void RefreshScanAnalysis(string resolvedScanText)
    {
        if (string.IsNullOrWhiteSpace(resolvedScanText))
        {
            SetText(scanText, defaultScanText);
            return;
        }

        string clean = resolvedScanText.Replace("\n", " ").Trim();
        SetText(scanText, clean);
    }

    private void ShowSecondaryObjectiveLine(TMP_Text target, string value)
    {
        if (target == null)
            return;

        target.gameObject.SetActive(true);
        target.text = value ?? string.Empty;
    }

    private void HideSecondaryObjectiveLine(TMP_Text target)
    {
        if (target == null)
            return;

        target.text = string.Empty;
        target.gameObject.SetActive(false);
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;

        target.text = value ?? string.Empty;
    }
}