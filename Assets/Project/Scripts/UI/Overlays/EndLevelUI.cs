using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EndLevelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject endLevelOverlay;

    [Header("HUD Bottom (Next only)")]
    [SerializeField] private GameObject hudBottom;

    [Header("Modules UI")]
    [SerializeField] private EndLevelAccordionUI accordionUI;
    [SerializeField] private EndLevelLinesBuilderUI linesBuilder;
    [SerializeField] private EndLevelTotalsPresenterUI totalsPresenter;

    [Header("Header")]
    [SerializeField] private TMP_Text levelIdText;
    [SerializeField] private TMP_Text worldLevelText;
    [SerializeField] private TMP_Text titleText;

    [Header("Panels")]
    [SerializeField] private Transform statsContainer;
    [SerializeField] private Transform goalsContainer;
    [SerializeField] private Transform bonusContainer;

    [Header("Rythme")]
    [SerializeField] private float lineDelay = 0.35f;
    [SerializeField] private float blockIntroDelay = 0.35f;
    [SerializeField] private float blockOutroDelay = 0.55f;
    [SerializeField] private float afterFoldDelay = 0.35f;

    [Header("Music - Ceremony")]
    [Tooltip("Si true, lance une musique dediee a la ceremonie des que Show() est appele.")]
    [SerializeField] private bool playCeremonyMusicOnShow = true;

    [SerializeField] private MusicId ceremonyMusicId = MusicId.MainEndSequence;

    [SerializeField] private float ceremonyFadeOutSec = 0.8f;
    [SerializeField] private float ceremonyFadeInSec = 0.8f;

    // CHANGE: event avec token
    public Action<EndLevelOutcome, EndLevelToken> OnCeremonyFinished;

    public string CurrentLevelId { get; private set; }

    private List<SecondaryObjectiveResult> secondaryResults;
    private LevelCatalogService.LevelCatalogEntry currentLevelMeta;

    private int bronzeThreshold;
    private int silverThreshold;
    private int goldThreshold;

    // NEW: token scellé injecté par LevelManager
    private bool hasToken;
    private EndLevelToken token;

    // NEW: garde-fou musique (evite de relancer si Show() est appele plusieurs fois)
    private bool ceremonyMusicStarted;

    /// <summary>
    /// Injecte le token scellé (créé par LevelManager) avant la reveal routine.
    /// </summary>
    public void SetEndLevelToken(EndLevelToken t)
    {
        token = t;
        hasToken = true;
    }

    public void Show(
        EndLevelStats stats,
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData levelData,
        MainObjectiveResult mainObj,
        List<SecondaryObjectiveResult> secondaryObjectiveResults)
    {
        currentLevelMeta = levelMeta;
        secondaryResults = secondaryObjectiveResults;

        CurrentLevelId = levelData != null ? levelData.LevelID : null;

        StartCeremonyMusicOnce();

        StopAllCoroutines();
        StartCoroutine(RevealRoutine(stats, levelData, mainObj));
    }

    public void Hide()
    {
        StopAllCoroutines();

        if (endLevelOverlay != null)
            endLevelOverlay.SetActive(false);

        if (hudBottom != null)
            hudBottom.SetActive(false);

        // Reset pour la prochaine ceremonie
        ceremonyMusicStarted = false;
        hasToken = false;
        token = default;
    }

    private void StartCeremonyMusicOnce()
    {
        if (ceremonyMusicStarted)
            return;

        ceremonyMusicStarted = true;

        if (!playCeremonyMusicOnShow)
            return;

        if (AudioManager.Instance == null)
            return;

        // Safety : si un multiplier trainait d'une phase precedente, on revient normal.
        AudioManager.Instance.SetMusicVolumeMultiplier(1f, 0f);

        AudioManager.Instance.PlayMusic(ceremonyMusicId, ceremonyFadeOutSec, ceremonyFadeInSec);
    }

    private void SetupHeader(LevelData levelData)
    {
        if (levelData == null)
            return;

        if (levelIdText != null)
            levelIdText.text = string.IsNullOrEmpty(levelData.LevelID) ? "-" : levelData.LevelID;

        string worldName = currentLevelMeta != null ? WorldCatalogService.GetWorldDisplayName(currentLevelMeta.worldId) : "";
        string title = currentLevelMeta != null ? currentLevelMeta.title : "";

        if (worldLevelText != null)
            worldLevelText.text = string.IsNullOrEmpty(worldName) ? "" : worldName;

        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(title) ? "" : title;
    }

    private IEnumerator RevealRoutine(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        if (endLevelOverlay != null)
            endLevelOverlay.SetActive(true);

        if (hudBottom != null)
            hudBottom.SetActive(false);

        if (linesBuilder != null)
            linesBuilder.ClearAll();

        if (totalsPresenter != null)
            totalsPresenter.ResetAll(levelData);

        ReadThresholdsFromLevelData(levelData);

        if (statsContainer != null) statsContainer.gameObject.SetActive(true);
        if (goalsContainer != null) goalsContainer.gameObject.SetActive(false);
        if (bonusContainer != null) bonusContainer.gameObject.SetActive(false);

        if (accordionUI != null)
        {
            accordionUI.SetInteractable(false);
            accordionUI.SetState(goalsExpandedValue: true, combosExpandedValue: true, instant: true);
        }

        SetupHeader(levelData);

        EndLevelScoreBreakdown breakdown = new EndLevelScoreBreakdown();
        breakdown.RawScore = (stats != null) ? Mathf.Max(0, stats.RawScore) : 0;
        breakdown.GoalsBonus = 0;
        breakdown.CombosBonus = 0;
        breakdown.FinalScore = breakdown.RawScore;

        yield return WaitBlockIntro();

        if (totalsPresenter != null)
            totalsPresenter.ShowRawScoreLine();

        yield return new WaitForSecondsRealtime(lineDelay);

        if (totalsPresenter != null)
            yield return StartCoroutine(totalsPresenter.AnimateOrSetRawScore(breakdown.RawScore));

        yield return new WaitForSecondsRealtime(lineDelay);

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(totalsPresenter.WaitForFinalScoreAnimations());
        }

        yield return WaitBlockOutro();

        if (goalsContainer != null)
            goalsContainer.gameObject.SetActive(true);

        yield return WaitBlockIntro();

        if (linesBuilder != null)
            linesBuilder.AddMainObjectiveLine(mainObj);

        yield return new WaitForSecondsRealtime(lineDelay);

        if (secondaryResults != null && secondaryResults.Count > 0)
        {
            for (int i = 0; i < secondaryResults.Count; i++)
            {
                if (linesBuilder != null)
                    linesBuilder.AddSecondaryObjectiveLine(secondaryResults[i]);

                yield return new WaitForSecondsRealtime(lineDelay);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(lineDelay);
        }

        int totalGoalsBonus = (linesBuilder != null)
            ? linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults)
            : 0;

        if (linesBuilder != null)
            linesBuilder.ShowGoalsTotalLine();

        yield return new WaitForSecondsRealtime(lineDelay);

        if (totalsPresenter != null)
            yield return StartCoroutine(totalsPresenter.AnimateGoalsBonus(totalGoalsBonus));

        yield return new WaitForSecondsRealtime(lineDelay);

        breakdown.GoalsBonus = Mathf.Max(0, totalGoalsBonus);
        breakdown.FinalScore = breakdown.RawScore + breakdown.GoalsBonus;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(totalsPresenter.WaitForFinalScoreAnimations());
        }

        if (accordionUI != null)
            accordionUI.RefreshGoalsCachedHeight();

        yield return WaitBlockOutro();

        if (accordionUI != null)
        {
            accordionUI.SetGoalsExpanded(false, instant: false);

            float foldDur = accordionUI.GoalsToggleDurationSec;
            if (foldDur > 0f)
                yield return new WaitForSecondsRealtime(foldDur);

            if (afterFoldDelay > 0f)
                yield return new WaitForSecondsRealtime(afterFoldDelay);
        }

        if (bonusContainer != null)
            bonusContainer.gameObject.SetActive(true);

        if (accordionUI != null)
            accordionUI.SetCombosExpanded(true, instant: true);

        yield return WaitBlockIntro();

        if (linesBuilder != null)
            yield return StartCoroutine(linesBuilder.RevealCombos(stats, lineDelay));

        int totalComboPoints = (linesBuilder != null) ? linesBuilder.LastComboPoints : 0;

        if (accordionUI != null)
            accordionUI.RefreshCombosCachedHeight();

        if (linesBuilder != null)
            linesBuilder.ShowCombosTotalLine();

        yield return new WaitForSecondsRealtime(lineDelay);

        if (totalsPresenter != null)
            yield return StartCoroutine(totalsPresenter.AnimateCombosBonus(totalComboPoints));

        yield return new WaitForSecondsRealtime(lineDelay);

        breakdown.CombosBonus = Mathf.Max(0, totalComboPoints);
        breakdown.FinalScore = breakdown.RawScore + breakdown.GoalsBonus + breakdown.CombosBonus;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(totalsPresenter.WaitForFinalScoreAnimations());
        }

        yield return WaitBlockOutro();

        if (accordionUI != null)
            accordionUI.SetInteractable(true);

        EndLevelOutcome outcome = EndLevelOutcomeBuilder.Build(levelData, mainObj.Achieved, breakdown.FinalScore);

        bronzeThreshold = outcome.BronzeThreshold;
        silverThreshold = outcome.SilverThreshold;
        goldThreshold = outcome.GoldThreshold;

        // CHANGE: on renvoie le token si injecté, sinon default (mais log)
        if (!hasToken)
            Debug.LogWarning("[EndLevelUI] Aucun EndLevelToken injecté. OnCeremonyFinished enverra default.");

        OnCeremonyFinished?.Invoke(outcome, hasToken ? token : default);

        if (hudBottom != null)
            hudBottom.SetActive(true);
    }

    private void ReadThresholdsFromLevelData(LevelData levelData)
    {
        bronzeThreshold = 0;
        silverThreshold = 0;
        goldThreshold = 0;

        if (levelData == null || levelData.ScoreGoals == null)
            return;

        for (int i = 0; i < levelData.ScoreGoals.Length; i++)
        {
            ScoreGoalsData g = levelData.ScoreGoals[i];
            if (g == null)
                continue;

            string t = g.Type;
            if (string.IsNullOrEmpty(t))
                continue;

            int pts = Mathf.Max(0, g.Points);

            if (StringEqualsIgnoreCase(t, "Bronze"))
                bronzeThreshold = pts;
            else if (StringEqualsIgnoreCase(t, "Silver"))
                silverThreshold = pts;
            else if (StringEqualsIgnoreCase(t, "Gold"))
                goldThreshold = pts;
        }
    }

    private bool StringEqualsIgnoreCase(string a, string b)
    {
        return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator WaitBlockIntro()
    {
        if (blockIntroDelay > 0f)
            yield return new WaitForSecondsRealtime(blockIntroDelay);
    }

    private IEnumerator WaitBlockOutro()
    {
        if (blockOutroDelay > 0f)
            yield return new WaitForSecondsRealtime(blockOutroDelay);
    }
}