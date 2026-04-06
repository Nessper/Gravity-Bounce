using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Gère toute la cérémonie de fin de niveau.
///
/// Responsabilités :
/// - afficher l'overlay de cérémonie,
/// - révéler progressivement les blocs (raw score, goals, bonus),
/// - gérer le hold-to-skip,
/// - forcer un état final cohérent en cas de skip,
/// - déclencher la musique de cérémonie,
/// - construire puis émettre le résultat final via OnCeremonyFinished.
///
/// Important :
/// - ce script orchestre la mise en scène,
/// - il ne calcule pas lui-même les règles métier du score final,
/// - il s'appuie sur EndLevelOutcomeBuilder, linesBuilder et totalsPresenter.
///
/// Règle importante sur l'accordéon :
/// - état de départ de cérémonie : Goals ouvert, Bonus fermé,
/// - état final de cérémonie : Goals fermé, Bonus ouvert,
/// - le chemin normal et le chemin skip doivent aboutir exactement au même état final.
/// </summary>
public class EndLevelUI : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // ROOT / HUD
    // ---------------------------------------------------------------------

    [Header("Root")]
    [SerializeField] private GameObject endLevelOverlay;

    [Header("HUD Bottom")]
    [SerializeField] private GameObject hudBottom;

    [Header("Hold To Skip")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

    // ---------------------------------------------------------------------
    // MAIN UI BLOCKS
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // TIMING
    // ---------------------------------------------------------------------

    [Header("Timing")]
    [SerializeField] private float lineDelay = 0.35f;
    [SerializeField] private float blockIntroDelay = 0.35f;
    [SerializeField] private float blockOutroDelay = 0.55f;
    [SerializeField] private float afterFoldDelay = 0.35f;

    // ---------------------------------------------------------------------
    // MUSIC
    // ---------------------------------------------------------------------

    [Header("Music")]
    [SerializeField] private bool playCeremonyMusicOnShow = true;
    [SerializeField] private MusicId ceremonyMusicId = MusicId.MainEndSequence;
    [SerializeField] private float ceremonyFadeOutSec = 2.0f;
    [SerializeField] private float ceremonyFadeInSec = 1.5f;

    // ---------------------------------------------------------------------
    // OUTPUT
    // ---------------------------------------------------------------------

    /// <summary>
    /// Émis à la fin de la cérémonie, normale ou skippée.
    /// Le token est renvoyé si injecté auparavant.
    /// </summary>
    public Action<EndLevelOutcome, EndLevelToken> OnCeremonyFinished;

    public string CurrentLevelId { get; private set; }

    // ---------------------------------------------------------------------
    // RUNTIME DATA
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------------------

    /// <summary>
    /// Injecte le token scellé du niveau.
    /// Il sera renvoyé lors de OnCeremonyFinished.
    /// </summary>
    public void SetEndLevelToken(EndLevelToken t)
    {
        token = t;
        hasToken = true;
    }

    /// <summary>
    /// Point d'entrée principal de la cérémonie.
    /// Initialise l'état runtime, démarre la musique si nécessaire,
    /// coupe les anciennes coroutines éventuelles, puis lance RevealRoutine.
    /// </summary>
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

    /// <summary>
    /// Masque complètement l'UI de cérémonie et remet l'état runtime à zéro.
    /// </summary>
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

    /// <summary>
    /// Coupe immédiatement la cérémonie en cours.
    /// Utilisé lorsqu'un autre flow prioritaire prend la main
    /// (par exemple un GameOver).
    /// </summary>
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

    /// <summary>
    /// Affiche uniquement le header.
    /// Utilisé pour certains flows spéciaux comme un GameOver forcé.
    /// </summary>
    public void ShowHeaderOnly(string levelId, LevelCatalogService.LevelCatalogEntry levelMeta)
    {
        currentLevelMeta = levelMeta;

        LevelData fakeLevelData = new LevelData();
        fakeLevelData.LevelID = levelId;

        SetupHeader(fakeLevelData);
    }

    /// <summary>
    /// Callback du hold-to-skip.
    /// Ne stoppe pas brutalement la cérémonie ici :
    /// on pose seulement un flag, puis RevealRoutine bifurque proprement.
    /// </summary>
    public void OnSkipCeremonyRequested()
    {
        if (ceremonyAborted || skipRequested)
            return;

        skipRequested = true;

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    // ---------------------------------------------------------------------
    // STATE HELPERS
    // ---------------------------------------------------------------------

    private bool ShouldAbortCeremony()
    {
        return ceremonyAborted;
    }

    private bool ShouldSkipCeremony()
    {
        return skipRequested;
    }

    // ---------------------------------------------------------------------
    // MUSIC
    // ---------------------------------------------------------------------

    /// <summary>
    /// Démarre la musique de cérémonie une seule fois.
    /// Important :
    /// - on ne remonte plus explicitement le volume ici,
    /// - on laisse le multiplicateur courant tel quel,
    /// - seul le morceau change.
    /// </summary>
    private void StartCeremonyMusicOnce()
    {
        if (ceremonyMusicStarted)
            return;

        ceremonyMusicStarted = true;

        if (!playCeremonyMusicOnShow || AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayMusic(ceremonyMusicId, ceremonyFadeOutSec, ceremonyFadeInSec);
    }

    // ---------------------------------------------------------------------
    // HEADER
    // ---------------------------------------------------------------------

    /// <summary>
    /// Remplit le header de la cérémonie :
    /// - level id,
    /// - nom du monde,
    /// - titre du niveau.
    /// </summary>
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

    // ---------------------------------------------------------------------
    // MAIN CEREMONY ROUTINE
    // ---------------------------------------------------------------------

    /// <summary>
    /// Routine principale de révélation.
    ///
    /// Déroulé :
    /// 1. initialisation visuelle,
    /// 2. raw score,
    /// 3. bloc Goals,
    /// 4. fermeture Goals,
    /// 5. ouverture Bonus,
    /// 6. total final,
    /// 7. finalisation normale.
    ///
    /// À chaque étape, on vérifie si la cérémonie doit être abort ou skip.
    /// </summary>
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

        // -------------------------------------------------
        // RAW SCORE
        // -------------------------------------------------

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

    // ---------------------------------------------------------------------
    // SKIP / ABORT BRANCHING
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne true si RevealRoutine doit s'arrêter maintenant.
    /// - abort : on coupe sans rien finaliser,
    /// - skip : on bifurque vers la routine de fin instantanée.
    /// </summary>
    private bool HandleAbortOrSkip(LevelData levelData, MainObjectiveResult mainObj)
    {
        if (ShouldAbortCeremony())
            return true;

        if (!ShouldSkipCeremony())
            return false;

        StartCoroutine(FinishCeremonyFromSkipRoutine(levelData, mainObj));
        return true;
    }

    // ---------------------------------------------------------------------
    // NORMAL END
    // ---------------------------------------------------------------------

    /// <summary>
    /// Finalisation normale de la cérémonie.
    ///
    /// Important :
    /// on force explicitement le même état final d'accordéon
    /// que dans le chemin skip, pour éviter toute divergence visuelle.
    /// </summary>
    private void FinalizeCeremonyNormally(LevelData levelData, MainObjectiveResult mainObj, EndLevelScoreBreakdown breakdown)
    {
        if (accordionUI != null)
        {
            accordionUI.RefreshGoalsCachedHeight();
            accordionUI.RefreshBonusCachedHeight();
            accordionUI.ForceCeremonyEndStateInstant();
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

    // ---------------------------------------------------------------------
    // SKIP END
    // ---------------------------------------------------------------------

    /// <summary>
    /// Finalisation instantanée en cas de skip.
    /// Reconstruit l'état final sans rejouer toute la mise en scène.
    /// </summary>
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

    // ---------------------------------------------------------------------
    // THRESHOLDS
    // ---------------------------------------------------------------------

    /// <summary>
    /// Relit les seuils Bronze / Silver / Gold depuis le LevelData.
    /// Stockés ici pour rester disponibles côté UI si besoin.
    /// </summary>
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

    // ---------------------------------------------------------------------
    // SKIPPABLE WAIT HELPERS
    // ---------------------------------------------------------------------

    private IEnumerator WaitBlockIntroSkippable()
    {
        yield return StartCoroutine(WaitRealtimeSkippable(blockIntroDelay));
    }

    private IEnumerator WaitBlockOutroSkippable()
    {
        yield return StartCoroutine(WaitRealtimeSkippable(blockOutroDelay));
    }

    /// <summary>
    /// Attente temps réel interrompable par abort ou skip.
    /// </summary>
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

    /// <summary>
    /// Exécute une coroutine "enfant" mais l'interrompt si la cérémonie
    /// doit être abort ou skip.
    /// </summary>
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

    // ---------------------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------------------

    private void OnDisable()
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }
}