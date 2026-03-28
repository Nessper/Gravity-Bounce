using System.Collections;
using UnityEngine;

/// <summary>
/// Présente et anime les totaux de fin de niveau :
/// - Raw score (ligne)
/// - Bonus Goals (AnimatedIntText)
/// - Bonus Combos (AnimatedIntText)
/// - Final score (AnimatedIntText + ProgressBar)
/// - Configuration des thresholds Bronze/Silver/Gold via LevelData
///
/// IMPORTANT :
/// - Ne gère pas la cérémonie.
/// - Ne construit pas les listes Goals/Combos (c'est EndLevelLinesBuilderUI).
/// </summary>
public class EndLevelTotalsPresenterUI : MonoBehaviour
{
    // ----------------------------------------------------------
    // LIGNES (UI)
    // ----------------------------------------------------------

    [Header("Lines")]
    [SerializeField] private LineEntryFinalUI rawScoreLine;
    [SerializeField] private LineEntryFinalUI finalScoreLine;

    // ----------------------------------------------------------
    // SCORE BAR
    // ----------------------------------------------------------

    [Header("Progress Bar")]
    [SerializeField] private FinalScoreBarUI finalScoreBar;

    // ----------------------------------------------------------
    // ANIMATED TEXTS
    // ----------------------------------------------------------

    [Header("Animated Texts")]
    [SerializeField] private AnimatedIntText rawScoreAnimated;
    [SerializeField] private AnimatedIntText goalsBonusAnimated; 
    [SerializeField] private AnimatedIntText bonusTotalAnimated;
    [SerializeField] private AnimatedIntText finalScoreAnimated;

    // ----------------------------------------------------------
    // STATE (THRESHOLDS)
    // ----------------------------------------------------------

    private int progressMax = 0;
    private int bronzeThreshold = 0;
    private int silverThreshold = 0;
    private int goldThreshold = 0;

    public int BronzeThreshold
    {
        get { return bronzeThreshold; }
    }

    public int SilverThreshold
    {
        get { return silverThreshold; }
    }

    public int GoldThreshold
    {
        get { return goldThreshold; }
    }

    public int ProgressMax
    {
        get { return progressMax; }
    }

    // ----------------------------------------------------------
    // RESET / CONFIG
    // ----------------------------------------------------------

    public void ResetAll(LevelData levelData)
    {
        bronzeThreshold = 0;
        silverThreshold = 0;
        goldThreshold = 0;
        progressMax = 0;

        ReadThresholds(levelData);
        ConfigureBar();
        ResetScoreDisplays();
    }

    private void ReadThresholds(LevelData levelData)
    {
        if (levelData == null || levelData.ScoreGoals == null)
            return;

        for (int i = 0; i < levelData.ScoreGoals.Length; i++)
        {
            var g = levelData.ScoreGoals[i];
            if (g == null)
                continue;

            if (g.Type == "Bronze") bronzeThreshold = g.Points;
            else if (g.Type == "Silver") silverThreshold = g.Points;
            else if (g.Type == "Gold") goldThreshold = g.Points;
        }

        progressMax = (goldThreshold > 0) ? Mathf.RoundToInt(goldThreshold * 1.2f) : 0;
    }

    private void ConfigureBar()
    {
        if (finalScoreBar == null)
            return;

        if (progressMax > 0)
            finalScoreBar.Configure(bronzeThreshold, silverThreshold, goldThreshold, progressMax);
        else
            finalScoreBar.Configure(0, 0, 0, 1);

        finalScoreBar.ResetInstant();
    }

    private void ResetScoreDisplays()
    {
        SetRawScoreInstant(0);
        SetGoalsBonusInstant(0);
        SetBonusTotalInstant(0);
        SetFinalScoreInstant(0);
    }

    // ----------------------------------------------------------
    // RAW SCORE
    // ----------------------------------------------------------

    public void ShowRawScoreLine()
    {
        if (rawScoreLine == null)
            return;

        rawScoreLine.gameObject.SetActive(true);
        SetRawScoreInstant(0);
    }

    public IEnumerator AnimateOrSetRawScore(int rawScore)
    {
        if (rawScoreAnimated != null)
        {
            rawScoreAnimated.AnimateTo(rawScore);
            yield return StartCoroutine(WaitForLocalLineAnimation(rawScoreAnimated));
            yield break;
        }

        if (rawScoreLine != null && rawScoreLine.value != null)
            rawScoreLine.value.text = rawScore.ToString("N0");
    }

    private void SetRawScoreInstant(int value)
    {
        if (rawScoreAnimated != null)
        {
            rawScoreAnimated.SetInstant(value);
            return;
        }

        if (rawScoreLine != null && rawScoreLine.value != null)
            rawScoreLine.value.text = value.ToString("N0");
    }

    // ----------------------------------------------------------
    // GOALS BONUS
    // ----------------------------------------------------------

    public void SetGoalsBonusInstant(int value)
    {
        if (goalsBonusAnimated != null)
        {
            goalsBonusAnimated.SetInstant(value);
            return;
        }
    }

    public IEnumerator AnimateGoalsBonus(int value)
    {
        if (goalsBonusAnimated != null)
        {
            goalsBonusAnimated.AnimateTo(value);
            yield return StartCoroutine(WaitForLocalLineAnimation(goalsBonusAnimated));
            yield break;
        }
    }

    // ----------------------------------------------------------
    // BONUS
    // ----------------------------------------------------------

    public void SetBonusTotalInstant(int value)
    {
        if (bonusTotalAnimated != null)
        {
            bonusTotalAnimated.SetInstant(value);
            return;
        }
    }

    public IEnumerator AnimateBonusTotal(int value)
    {
        if (bonusTotalAnimated != null)
        {
            bonusTotalAnimated.AnimateTo(value);
            yield return StartCoroutine(WaitForLocalLineAnimation(bonusTotalAnimated));
            yield break;
        }
    }

    // ----------------------------------------------------------
    // FINAL SCORE + BAR
    // ----------------------------------------------------------

    public void SetFinalScore(int currentScore, bool animate)
    {
        if (finalScoreAnimated != null)
        {
            if (animate) finalScoreAnimated.AnimateTo(currentScore);
            else finalScoreAnimated.SetInstant(currentScore);
        }
        else if (finalScoreLine != null && finalScoreLine.value != null)
        {
            finalScoreLine.value.text = currentScore.ToString("N0");
        }

        if (finalScoreBar != null && progressMax > 0)
            finalScoreBar.SetScore(currentScore);
    }

    public void SetFinalScoreInstant(int value)
    {
        SetFinalScore(value, animate: false);
    }

    public IEnumerator WaitForFinalScoreAnimations()
    {
        while (finalScoreAnimated != null && finalScoreAnimated.IsAnimating)
            yield return null;
    }

    // ----------------------------------------------------------
    // UTILS
    // ----------------------------------------------------------

    private IEnumerator WaitForLocalLineAnimation(AnimatedIntText anim)
    {
        if (anim == null)
            yield break;

        while (anim.IsAnimating)
            yield return null;
    }
}
