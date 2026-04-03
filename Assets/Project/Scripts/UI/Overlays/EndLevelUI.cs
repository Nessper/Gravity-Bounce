using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Gere la ceremonie de fin de niveau.
/// Supporte un hold-to-skip qui coupe la mise en scene
/// et force directement l etat final de la ceremonie.
/// </summary>
public class EndLevelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject endLevelOverlay;

    [Header("HUD Bottom")]
    [SerializeField] private GameObject hudBottom;

    [Header("Hold To Skip")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

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

    [Header("Timing")]
    [SerializeField] private float lineDelay = 0.35f;
    [SerializeField] private float blockIntroDelay = 0.35f;
    [SerializeField] private float blockOutroDelay = 0.55f;
    [SerializeField] private float afterFoldDelay = 0.35f;

    [Header("Music")]
    [SerializeField] private bool playCeremonyMusicOnShow = true;
    [SerializeField] private MusicId ceremonyMusicId = MusicId.MainEndSequence;
    [SerializeField] private float ceremonyFadeOutSec = 2.0f;
    [SerializeField] private float ceremonyFadeInSec = 1.5f;

    public Action<EndLevelOutcome, EndLevelToken> OnCeremonyFinished;

    public string CurrentLevelId { get; private set; }

    private List<SecondaryObjectiveResult> secondaryResults;
    private LevelCatalogService.LevelCatalogEntry currentLevelMeta;

    private int bronzeThreshold;
    private int silverThreshold;
    private int goldThreshold;

    private bool hasToken;
    private EndLevelToken token;

    private bool ceremonyMusicStarted;
    private bool ceremonyAborted;
    private bool skipRequested;

    private EndLevelStats currentStats;

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
        ceremonyAborted = false;
        skipRequested = false;

        currentStats = stats;
        currentLevelMeta = levelMeta;
        secondaryResults = secondaryObjectiveResults;

        CurrentLevelId = levelData != null ? levelData.LevelID : null;

        StartCeremonyMusicOnce();

        StopAllCoroutines();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        StartCoroutine(RevealRoutine(stats, levelData, mainObj));
    }

    public void Hide()
    {
        StopAllCoroutines();

        if (endLevelOverlay != null)
            endLevelOverlay.SetActive(false);

        if (hudBottom != null)
            hudBottom.SetActive(false);

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        ceremonyMusicStarted = false;
        hasToken = false;
        token = default;
        ceremonyAborted = false;
        skipRequested = false;
        currentStats = null;
    }

    public void AbortCeremony()
    {
        ceremonyAborted = true;
        skipRequested = false;

        StopAllCoroutines();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        if (hudBottom != null)
            hudBottom.SetActive(false);
    }

    public void ShowHeaderOnly(string levelId, LevelCatalogService.LevelCatalogEntry levelMeta)
    {
        currentLevelMeta = levelMeta;

        LevelData fakeLevelData = new LevelData();
        fakeLevelData.LevelID = levelId;

        SetupHeader(fakeLevelData);
    }

    public void OnSkipCeremonyRequested()
    {
        if (ceremonyAborted || skipRequested)
            return;

        skipRequested = true;

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    private bool ShouldAbortCeremony()
    {
        return ceremonyAborted;
    }

    private bool ShouldSkipCeremony()
    {
        return skipRequested;
    }

    private void StartCeremonyMusicOnce()
    {
        if (ceremonyMusicStarted)
            return;

        ceremonyMusicStarted = true;

        if (!playCeremonyMusicOnShow || AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetMusicVolumeMultiplier(1f, 0.5f);
        AudioManager.Instance.PlayMusic(ceremonyMusicId, ceremonyFadeOutSec, ceremonyFadeInSec);
    }

    public void SetupHeader(LevelData levelData)
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

        if (statsContainer != null)
            statsContainer.gameObject.SetActive(true);

        if (goalsContainer != null)
            goalsContainer.gameObject.SetActive(false);

        if (bonusContainer != null)
            bonusContainer.gameObject.SetActive(false);

        if (accordionUI != null)
        {
            accordionUI.SetInteractable(false);
            accordionUI.ForceCeremonyStartStateInstant();
        }

        SetupHeader(levelData);

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Show(this, OnSkipCeremonyRequested);

        if (ShouldAbortCeremony())
            yield break;

        EndLevelScoreBreakdown breakdown = new EndLevelScoreBreakdown();
        breakdown.RawScore = (stats != null) ? Mathf.Max(0, stats.RawScore) : 0;
        breakdown.GoalsBonus = 0;
        breakdown.BonusTotal = 0;
        breakdown.FinalScore = breakdown.RawScore;

        yield return StartCoroutine(WaitBlockIntroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (totalsPresenter != null)
            totalsPresenter.ShowRawScoreLine();

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (totalsPresenter != null)
            yield return StartCoroutine(RunSkippable(totalsPresenter.AnimateOrSetRawScore(breakdown.RawScore)));

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(RunSkippable(totalsPresenter.WaitForFinalScoreAnimations()));
        }

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitBlockOutroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (goalsContainer != null)
            goalsContainer.gameObject.SetActive(true);

        yield return StartCoroutine(WaitBlockIntroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (linesBuilder != null)
            linesBuilder.AddMainObjectiveLine(mainObj);

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (secondaryResults != null && secondaryResults.Count > 0)
        {
            for (int i = 0; i < secondaryResults.Count; i++)
            {
                if (linesBuilder != null)
                    linesBuilder.AddSecondaryObjectiveLine(secondaryResults[i]);

                yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
                if (HandleAbortOrSkip(levelData, mainObj))
                    yield break;
            }
        }
        else
        {
            yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
            if (HandleAbortOrSkip(levelData, mainObj))
                yield break;
        }

        int totalGoalsBonus = (linesBuilder != null)
            ? linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults)
            : 0;

        if (linesBuilder != null)
            linesBuilder.ShowGoalsTotalLine();

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (totalsPresenter != null)
            yield return StartCoroutine(RunSkippable(totalsPresenter.AnimateGoalsBonus(totalGoalsBonus)));

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        breakdown.GoalsBonus = Mathf.Max(0, totalGoalsBonus);
        breakdown.FinalScore = breakdown.RawScore + breakdown.GoalsBonus;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(RunSkippable(totalsPresenter.WaitForFinalScoreAnimations()));
        }

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (accordionUI != null)
            accordionUI.RefreshGoalsCachedHeight();

        yield return StartCoroutine(WaitBlockOutroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (accordionUI != null)
        {
            accordionUI.SetGoalsExpanded(false, instant: false);

            float foldDur = accordionUI.GoalsToggleDurationSec;
            if (foldDur > 0f)
                yield return StartCoroutine(WaitRealtimeSkippable(foldDur));

            if (HandleAbortOrSkip(levelData, mainObj))
                yield break;

            if (afterFoldDelay > 0f)
                yield return StartCoroutine(WaitRealtimeSkippable(afterFoldDelay));
        }

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (bonusContainer != null)
            bonusContainer.gameObject.SetActive(true);

        if (accordionUI != null)
        {
            accordionUI.SetBonusExpanded(true, instant: false);

            float bonusOpenDur = accordionUI.BonusToggleDurationSec;
            if (bonusOpenDur > 0f)
                yield return StartCoroutine(WaitRealtimeSkippable(bonusOpenDur));

            if (HandleAbortOrSkip(levelData, mainObj))
                yield break;
        }

        yield return StartCoroutine(WaitBlockIntroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (linesBuilder != null)
            yield return StartCoroutine(RunSkippable(linesBuilder.RevealBonusLines(stats, lineDelay)));

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        int totalBonusPoints = (linesBuilder != null) ? linesBuilder.LastBonusPoints : 0;

        if (accordionUI != null)
            accordionUI.RefreshBonusCachedHeight();

        if (linesBuilder != null)
            linesBuilder.ShowBonusTotalLine();

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (totalsPresenter != null)
            yield return StartCoroutine(RunSkippable(totalsPresenter.AnimateBonusTotal(totalBonusPoints)));

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        breakdown.BonusTotal = totalBonusPoints;
        breakdown.FinalScore = breakdown.RawScore + breakdown.GoalsBonus + breakdown.BonusTotal;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(RunSkippable(totalsPresenter.WaitForFinalScoreAnimations()));
        }

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitBlockOutroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        FinalizeCeremonyNormally(levelData, mainObj, breakdown);
    }

    private bool HandleAbortOrSkip(LevelData levelData, MainObjectiveResult mainObj)
    {
        if (ShouldAbortCeremony())
            return true;

        if (!ShouldSkipCeremony())
            return false;

        StartCoroutine(FinishCeremonyFromSkipRoutine(levelData, mainObj));
        return true;
    }

    private void FinalizeCeremonyNormally(LevelData levelData, MainObjectiveResult mainObj, EndLevelScoreBreakdown breakdown)
    {
        if (accordionUI != null)
        {
            accordionUI.RefreshGoalsCachedHeight();
            accordionUI.RefreshBonusCachedHeight();
            accordionUI.SetBonusExpanded(true, instant: true);
            accordionUI.SetInteractable(true);
        }

        EndLevelOutcome outcome = EndLevelOutcomeBuilder.Build(levelData, mainObj.Achieved, breakdown.FinalScore);

        bronzeThreshold = outcome.BronzeThreshold;
        silverThreshold = outcome.SilverThreshold;
        goldThreshold = outcome.GoldThreshold;

        if (!hasToken)
            Debug.LogWarning("[EndLevelUI] Aucun EndLevelToken injecte. OnCeremonyFinished enverra default.");

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        OnCeremonyFinished?.Invoke(outcome, hasToken ? token : default);

        if (hudBottom != null)
            hudBottom.SetActive(true);
    }

    private IEnumerator FinishCeremonyFromSkipRoutine(LevelData levelData, MainObjectiveResult mainObj)
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        if (endLevelOverlay != null)
            endLevelOverlay.SetActive(true);

        if (statsContainer != null)
            statsContainer.gameObject.SetActive(true);

        if (goalsContainer != null)
            goalsContainer.gameObject.SetActive(true);

        if (bonusContainer != null)
            bonusContainer.gameObject.SetActive(true);

        int rawScore = (currentStats != null) ? Mathf.Max(0, currentStats.RawScore) : 0;

        int totalGoalsBonus = 0;
        int totalBonusPoints = 0;

        if (linesBuilder != null)
        {
            linesBuilder.BuildGoalsInstant(mainObj, secondaryResults);
            totalGoalsBonus = linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults);

            linesBuilder.BuildBonusInstant(currentStats);
            totalBonusPoints = linesBuilder.LastBonusPoints;
        }

        int finalScore = rawScore + Mathf.Max(0, totalGoalsBonus) + Mathf.Max(0, totalBonusPoints);

        if (totalsPresenter != null)
        {
            totalsPresenter.ShowRawScoreLine();
            totalsPresenter.SetRawScoreInstant(rawScore);
            totalsPresenter.SetGoalsBonusInstant(totalGoalsBonus);
            totalsPresenter.SetBonusTotalInstant(totalBonusPoints);
            totalsPresenter.SetFinalScoreInstant(finalScore);
        }

        if (accordionUI != null)
        {
            accordionUI.SetInteractable(false);
            accordionUI.RefreshGoalsCachedHeight();
            accordionUI.RefreshBonusCachedHeight();
            accordionUI.ForceCeremonyEndStateInstant();
            accordionUI.SetInteractable(true);
        }

        EndLevelOutcome outcome = EndLevelOutcomeBuilder.Build(levelData, mainObj.Achieved, finalScore);

        bronzeThreshold = outcome.BronzeThreshold;
        silverThreshold = outcome.SilverThreshold;
        goldThreshold = outcome.GoldThreshold;

        if (!hasToken)
            Debug.LogWarning("[EndLevelUI] Aucun EndLevelToken injecte. OnCeremonyFinished enverra default.");

        OnCeremonyFinished?.Invoke(outcome, hasToken ? token : default);

        if (hudBottom != null)
            hudBottom.SetActive(true);

        yield break;
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
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator WaitBlockIntroSkippable()
    {
        yield return StartCoroutine(WaitRealtimeSkippable(blockIntroDelay));
    }

    private IEnumerator WaitBlockOutroSkippable()
    {
        yield return StartCoroutine(WaitRealtimeSkippable(blockOutroDelay));
    }

    private IEnumerator WaitRealtimeSkippable(float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ShouldAbortCeremony() || ShouldSkipCeremony())
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator RunSkippable(IEnumerator routine)
    {
        if (routine == null)
            yield break;

        bool done = false;
        Coroutine child = StartCoroutine(WrapRoutine(routine, () => done = true));

        while (!done)
        {
            if (ShouldAbortCeremony() || ShouldSkipCeremony())
            {
                if (child != null)
                    StopCoroutine(child);

                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator WrapRoutine(IEnumerator routine, Action onDone)
    {
        yield return StartCoroutine(routine);
        onDone?.Invoke();
    }

    private void OnDisable()
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }
}