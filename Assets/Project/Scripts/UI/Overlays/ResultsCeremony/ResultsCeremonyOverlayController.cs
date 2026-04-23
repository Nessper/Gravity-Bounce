using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controle la phase "Results Ceremony" de fin de niveau.
///
/// Responsabilites:
/// - initialiser les donnees runtime de la ceremony
/// - jouer la reveal routine du score
/// - gerer skip / abort
/// - construire puis emettre le resultat final via OnCeremonyFinished
/// - jouer la musique de ceremony si activee
///
/// IMPORTANT:
/// - ne pilote pas le root global de l overlay
/// - ne pilote pas le HUD global
/// - MainUIController doit deja avoir affiche l overlay
/// - les sous-panels internes restent actifs et sont pilotes en alpha
/// </summary>
public class ResultsCeremonyOverlayController : MonoBehaviour
{
    [Header("Hold To Skip")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

    [Header("Modules UI")]
    [SerializeField] private EndLevelAccordionUI accordionUI;
    [SerializeField] private EndLevelLinesBuilderUI linesBuilder;
    [SerializeField] private EndLevelTotalsPresenterUI totalsPresenter;

    [Header("Header")]
    [SerializeField] private TMP_Text titleText;

    [Header("Panels")]
    [SerializeField] private RectTransform goalsContainer;
    [SerializeField] private RectTransform bonusContainer;

    [Header("Panel Canvas Groups")]
    [SerializeField] private CanvasGroup scorePanelGroup;
    [SerializeField] private CanvasGroup goalsPanelGroup;
    [SerializeField] private CanvasGroup finalScoreGoalsPanelGroup;
    [SerializeField] private CanvasGroup bonusPanelGroup;
    [SerializeField] private CanvasGroup finalScoreBonusPanelGroup;
    [SerializeField] private CanvasGroup finalScorePanelGroup;

    [Header("Timing")]
    [SerializeField] private float lineDelay = 0.35f;
    [SerializeField] private float blockIntroDelay = 0.35f;
    [SerializeField] private float blockOutroDelay = 0.55f;
    [SerializeField] private float afterFoldDelay = 0.35f;
    [SerializeField] private float panelFadeDuration = 0.15f;

    [Header("Line Reveal")]
    [SerializeField] private float lineRevealFadeDuration = 0.12f;

    [Header("Buttons")]
    [SerializeField] private CanvasGroup buttonsPanelGroup;

    [Header("Music")]
    [SerializeField] private bool playCeremonyMusicOnPlay = true;
    [SerializeField] private MusicId ceremonyMusicId = MusicId.MainEndSequence;
    [SerializeField] private float ceremonyFadeOutSec = 2.0f;
    [SerializeField] private float ceremonyFadeInSec = 1.5f;

    public Action<EndLevelOutcome, EndLevelToken> OnCeremonyFinished;

    private List<SecondaryObjectiveResult> secondaryResults;
    private LevelCatalogService.LevelCatalogEntry currentLevelMeta;

    private bool hasToken;
    private EndLevelToken token;

    private bool ceremonyMusicStarted;
    private bool ceremonyAborted;
    private bool skipRequested;

    private EndLevelStats currentStats;

    private void Awake()
    {
        PrepareCeremonyVisualState();
    }

    public void SetEndLevelToken(EndLevelToken t)
    {
        token = t;
        hasToken = true;
    }

    /// <summary>
    /// Point d entree principal de la ceremony.
    /// IMPORTANT:
    /// - l overlay doit deja etre visible via MainUIController
    /// - ce script joue seulement la sequence interne
    /// </summary>
    public void Play(
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

        StartCeremonyMusicOnce();

        StopAllCoroutines();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        PrepareCeremonyVisualState();

        StartCoroutine(PlayCeremonyRoutine(stats, levelData, mainObj));
    }

    public void Hide()
    {
        StopAllCoroutines();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        ceremonyMusicStarted = false;
        hasToken = false;
        token = default;
        ceremonyAborted = false;
        skipRequested = false;
        currentStats = null;

        PrepareCeremonyVisualState();
    }

    public void AbortCeremony()
    {
        ceremonyAborted = true;
        skipRequested = false;

        StopAllCoroutines();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
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

        if (!playCeremonyMusicOnPlay || AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayMusic(ceremonyMusicId, ceremonyFadeOutSec, ceremonyFadeInSec);
    }

    public void SetupHeader(LevelData levelData)
    {
        if (levelData == null)
            return;

        string title = currentLevelMeta != null
            ? currentLevelMeta.title
            : string.Empty;

        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(title) ? string.Empty : title;
    }

    private void PrepareCeremonyVisualState()
    {
        SetCanvasGroupInstant(scorePanelGroup, 0f, false, false);
        SetCanvasGroupInstant(goalsPanelGroup, 0f, false, false);
        SetCanvasGroupInstant(finalScoreGoalsPanelGroup, 0f, false, false);
        SetCanvasGroupInstant(bonusPanelGroup, 0f, false, false);
        SetCanvasGroupInstant(finalScoreBonusPanelGroup, 0f, false, false);
        SetCanvasGroupInstant(finalScorePanelGroup, 0f, false, false);
        SetCanvasGroupInstant(buttonsPanelGroup, 0f, false, false);
    }

    private void SetCanvasGroupInstant(
        CanvasGroup group,
        float alpha,
        bool interactable = false,
        bool blocksRaycasts = false)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = blocksRaycasts;
    }

    private IEnumerator FadeCanvasGroup(
        CanvasGroup group,
        float to,
        float duration,
        bool interactableAtEnd = false,
        bool blocksRaycastsAtEnd = false)
    {
        if (group == null)
            yield break;

        float from = group.alpha;

        if (duration <= 0f)
        {
            group.alpha = to;
            group.interactable = interactableAtEnd;
            group.blocksRaycasts = blocksRaycastsAtEnd;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
        group.interactable = interactableAtEnd;
        group.blocksRaycasts = blocksRaycastsAtEnd;
    }

    private IEnumerator PlayCeremonyRoutine(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        if (linesBuilder != null)
            linesBuilder.ClearAll();

        if (totalsPresenter != null)
            totalsPresenter.ResetAll(levelData);

        SetupHeader(levelData);

        if (accordionUI != null)
        {
            accordionUI.SetInteractable(false);
            accordionUI.ForceCeremonyStartStateInstant();
        }

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Show(this, OnSkipCeremonyRequested);

        if (ShouldAbortCeremony())
            yield break;

        EndLevelScoreBreakdown breakdown = new EndLevelScoreBreakdown
        {
            RawScore = stats != null ? Mathf.Max(0, stats.RawScore) : 0,
            GoalsBonus = 0,
            BonusTotal = 0
        };
        breakdown.FinalScore = breakdown.RawScore;

        // -------------------------------------------------
        // SCORE BLOCK
        // -------------------------------------------------

        yield return StartCoroutine(FadeCanvasGroup(scorePanelGroup, 1f, panelFadeDuration));
        yield return StartCoroutine(FadeCanvasGroup(finalScorePanelGroup, 1f, panelFadeDuration));

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

        // -------------------------------------------------
        // GOALS BLOCK
        // -------------------------------------------------

        yield return StartCoroutine(FadeCanvasGroup(goalsPanelGroup, 1f, panelFadeDuration, true, true));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        List<GameObject> goalLines = null;
        if (linesBuilder != null)
            goalLines = linesBuilder.BuildGoalsHidden(mainObj, secondaryResults);

        if (linesBuilder != null)
            linesBuilder.PrepareGoalsTotalLineHidden();

        Canvas.ForceUpdateCanvases();
        if (goalsContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(goalsContainer);

        yield return null;

        yield return StartCoroutine(FadeCanvasGroup(finalScoreGoalsPanelGroup, 1f, panelFadeDuration));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitBlockIntroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        int totalGoalsBonus = (linesBuilder != null)
            ? linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults)
            : 0;

        if (linesBuilder != null)
            yield return StartCoroutine(linesBuilder.RevealGoalsTotalLine(lineRevealFadeDuration));

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (goalLines != null && goalLines.Count > 0)
        {
            for (int i = 0; i < goalLines.Count; i++)
            {
                yield return StartCoroutine(linesBuilder.RevealLine(goalLines[i], lineRevealFadeDuration));

                if (HandleAbortOrSkip(levelData, mainObj))
                    yield break;

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

        // -------------------------------------------------
        // TRANSITION GOALS -> BONUS
        // -------------------------------------------------

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

        // -------------------------------------------------
        // BONUS BLOCK
        // -------------------------------------------------

        if (accordionUI != null)
        {
            accordionUI.SetBonusExpanded(true, instant: false);

            float bonusOpenDur = accordionUI.BonusToggleDurationSec;
            if (bonusOpenDur > 0f)
                yield return StartCoroutine(WaitRealtimeSkippable(bonusOpenDur));

            if (HandleAbortOrSkip(levelData, mainObj))
                yield break;
        }

        yield return StartCoroutine(FadeCanvasGroup(bonusPanelGroup, 1f, panelFadeDuration, true, true));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        List<GameObject> bonusLines = null;
        if (linesBuilder != null)
            bonusLines = linesBuilder.BuildBonusHidden(stats);

        if (linesBuilder != null)
            linesBuilder.PrepareBonusTotalLineHidden();

        Canvas.ForceUpdateCanvases();
        if (bonusContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bonusContainer);

        yield return null;

        yield return StartCoroutine(FadeCanvasGroup(finalScoreBonusPanelGroup, 1f, panelFadeDuration));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitBlockIntroSkippable());
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        int totalBonusPoints = linesBuilder != null ? linesBuilder.LastBonusPoints : 0;

        if (linesBuilder != null)
            yield return StartCoroutine(linesBuilder.RevealBonusTotalLine(lineRevealFadeDuration));

        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
        if (HandleAbortOrSkip(levelData, mainObj))
            yield break;

        if (bonusLines != null && bonusLines.Count > 0)
        {
            for (int i = 0; i < bonusLines.Count; i++)
            {
                yield return StartCoroutine(linesBuilder.RevealLine(bonusLines[i], lineRevealFadeDuration));

                if (HandleAbortOrSkip(levelData, mainObj))
                    yield break;

                yield return StartCoroutine(WaitRealtimeSkippable(lineDelay));
                if (HandleAbortOrSkip(levelData, mainObj))
                    yield break;
            }
        }

        if (accordionUI != null)
            accordionUI.RefreshBonusCachedHeight();

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

        yield return StartCoroutine(FadeCanvasGroup(buttonsPanelGroup, 1f, panelFadeDuration, true, true));
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
            accordionUI.ForceCeremonyEndStateInstant();
            accordionUI.SetInteractable(true);
        }

        EndLevelOutcome outcome = EndLevelOutcomeBuilder.Build(levelData, mainObj.Achieved, breakdown.FinalScore);

        if (!hasToken)
            Debug.LogWarning("[ResultsCeremonyOverlayController] Aucun EndLevelToken injecte. OnCeremonyFinished enverra default.");

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        OnCeremonyFinished?.Invoke(outcome, hasToken ? token : default);
    }

    private IEnumerator FinishCeremonyFromSkipRoutine(LevelData levelData, MainObjectiveResult mainObj)
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        SetCanvasGroupInstant(scorePanelGroup, 1f, false, false);
        SetCanvasGroupInstant(goalsPanelGroup, 1f, true, true);
        SetCanvasGroupInstant(finalScoreGoalsPanelGroup, 1f, false, false);
        SetCanvasGroupInstant(bonusPanelGroup, 1f, true, true);
        SetCanvasGroupInstant(finalScoreBonusPanelGroup, 1f, false, false);
        SetCanvasGroupInstant(finalScorePanelGroup, 1f, false, false);
        SetCanvasGroupInstant(buttonsPanelGroup, 1f, true, true);

        int rawScore = currentStats != null ? Mathf.Max(0, currentStats.RawScore) : 0;

        int totalGoalsBonus = 0;
        int totalBonusPoints = 0;

        if (linesBuilder != null)
        {
            linesBuilder.BuildGoalsInstant(mainObj, secondaryResults);
            totalGoalsBonus = linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults);

            linesBuilder.BuildBonusInstant(currentStats);
            totalBonusPoints = linesBuilder.LastBonusPoints;
        }

        if (accordionUI != null)
        {
            accordionUI.ForceCeremonyEndStateInstant();
        }

        Canvas.ForceUpdateCanvases();

        if (goalsContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(goalsContainer);

        if (bonusContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bonusContainer);

        yield return null;

        if (accordionUI != null)
        {
            accordionUI.SetInteractable(true);
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

        EndLevelOutcome outcome = EndLevelOutcomeBuilder.Build(levelData, mainObj.Achieved, finalScore);

        if (!hasToken)
            Debug.LogWarning("[ResultsCeremonyOverlayController] Aucun EndLevelToken injecte. OnCeremonyFinished enverra default.");

        OnCeremonyFinished?.Invoke(outcome, hasToken ? token : default);
    }

    private void ReadThresholdsFromLevelData(LevelData levelData)
    {
        if (levelData == null || levelData.ScoreGoals == null)
            return;

        for (int i = 0; i < levelData.ScoreGoals.Length; i++)
        {
            ScoreGoalsData g = levelData.ScoreGoals[i];
            if (g == null)
                continue;
        }
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