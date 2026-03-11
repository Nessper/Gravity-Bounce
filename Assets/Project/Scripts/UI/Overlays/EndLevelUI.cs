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
    [SerializeField] private float ceremonyFadeOutSec = 2.0f;
    [SerializeField] private float ceremonyFadeInSec = 1.5f;

    // Event emis a la fin de la ceremonie, avec le token scelle associe.
    public Action<EndLevelOutcome, EndLevelToken> OnCeremonyFinished;

    public string CurrentLevelId { get; private set; }

    private List<SecondaryObjectiveResult> secondaryResults;
    private LevelCatalogService.LevelCatalogEntry currentLevelMeta;

    private int bronzeThreshold;
    private int silverThreshold;
    private int goldThreshold;

    // Token de fin de niveau injecte par le LevelManager.
    private bool hasToken;
    private EndLevelToken token;

    // Garde-fou pour eviter de relancer plusieurs fois la musique de ceremonie.
    private bool ceremonyMusicStarted;

    // Flag d'annulation de ceremonie.
    // S'il passe a true, la reveal routine doit s'arreter proprement.
    private bool ceremonyAborted;

    /// <summary>
    /// Injecte le token scelle (cree par LevelManager) avant la reveal routine.
    /// </summary>
    public void SetEndLevelToken(EndLevelToken t)
    {
        token = t;
        hasToken = true;
    }

    /// <summary>
    /// Lance la ceremonie de fin normale.
    /// </summary>
    public void Show(
        EndLevelStats stats,
        LevelCatalogService.LevelCatalogEntry levelMeta,
        LevelData levelData,
        MainObjectiveResult mainObj,
        List<SecondaryObjectiveResult> secondaryObjectiveResults)
    {
        // Toute nouvelle ceremonie repart d'un etat non annule.
        ceremonyAborted = false;

        currentLevelMeta = levelMeta;
        secondaryResults = secondaryObjectiveResults;

        CurrentLevelId = levelData != null ? levelData.LevelID : null;

        StartCeremonyMusicOnce();

        StopAllCoroutines();
        StartCoroutine(RevealRoutine(stats, levelData, mainObj));
    }

    /// <summary>
    /// Masque l'UI et remet les flags dans un etat propre.
    /// </summary>
    public void Hide()
    {
        StopAllCoroutines();

        if (endLevelOverlay != null)
            endLevelOverlay.SetActive(false);

        if (hudBottom != null)
            hudBottom.SetActive(false);

        // Reset propre pour la prochaine ceremonie.
        ceremonyMusicStarted = false;
        hasToken = false;
        token = default;
        ceremonyAborted = false;
    }

    /// <summary>
    /// Annule explicitement la ceremonie en cours.
    /// Utilise par le flow GameOver Hull pour prendre la priorite
    /// sur une ceremonie deja en train de se derouler.
    /// </summary>
    public void AbortCeremony()
    {
        ceremonyAborted = true;
        StopAllCoroutines();

        // On masque le HUD bottom pour eviter qu'il reste visible
        // si la ceremonie est coupee brutalement.
        if (hudBottom != null)
            hudBottom.SetActive(false);
    }

    /// <summary>
    /// Permet de rafraichir uniquement le header sans lancer toute la ceremonie.
    /// Utile pour la branche GameOver directe qui bypass la reveal routine normale.
    /// </summary>
    public void ShowHeaderOnly(string levelId, LevelCatalogService.LevelCatalogEntry levelMeta)
    {
        currentLevelMeta = levelMeta;

        LevelData fakeLevelData = new LevelData();
        fakeLevelData.LevelID = levelId;

        SetupHeader(fakeLevelData);
    }

    /// <summary>
    /// Retourne true si la ceremonie doit etre interrompue.
    /// </summary>
    private bool ShouldAbortCeremony()
    {
        return ceremonyAborted;
    }

    /// <summary>
    /// Lance la musique de ceremonie une seule fois.
    /// </summary>
    private void StartCeremonyMusicOnce()
    {
        if (ceremonyMusicStarted)
            return;

        ceremonyMusicStarted = true;

        if (!playCeremonyMusicOnShow)
            return;

        if (AudioManager.Instance == null)
            return;

        // Securite : remet le multiplicateur a 1 si quelque chose trainait d'avant.
        AudioManager.Instance.SetMusicVolumeMultiplier(1f, 0.5f);
        AudioManager.Instance.PlayMusic(ceremonyMusicId, ceremonyFadeOutSec, ceremonyFadeInSec);
    }

    /// <summary>
    /// Remplit le header de l'overlay de fin :
    /// - id du niveau
    /// - nom du monde
    /// - titre du niveau
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

    /// <summary>
    /// Coroutine principale de reveal de la ceremonie.
    /// On ajoute plusieurs garde-fous "ShouldAbortCeremony"
    /// pour pouvoir couper proprement le flow si un GameOver Hull prend la main.
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

        if (statsContainer != null) statsContainer.gameObject.SetActive(true);
        if (goalsContainer != null) goalsContainer.gameObject.SetActive(false);
        if (bonusContainer != null) bonusContainer.gameObject.SetActive(false);

        if (accordionUI != null)
        {
            accordionUI.SetInteractable(false);
            accordionUI.SetState(goalsExpandedValue: true, combosExpandedValue: true, instant: true);
        }

        SetupHeader(levelData);

        // Si la ceremonie a ete annulee entre-temps, on s'arrete ici.
        if (ShouldAbortCeremony())
            yield break;

        EndLevelScoreBreakdown breakdown = new EndLevelScoreBreakdown();
        breakdown.RawScore = (stats != null) ? Mathf.Max(0, stats.RawScore) : 0;
        breakdown.GoalsBonus = 0;
        breakdown.CombosBonus = 0;
        breakdown.FinalScore = breakdown.RawScore;

        yield return WaitBlockIntro();

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
            totalsPresenter.ShowRawScoreLine();

        yield return new WaitForSecondsRealtime(lineDelay);

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
            yield return StartCoroutine(totalsPresenter.AnimateOrSetRawScore(breakdown.RawScore));

        yield return new WaitForSecondsRealtime(lineDelay);

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(totalsPresenter.WaitForFinalScoreAnimations());
        }

        yield return WaitBlockOutro();

        if (ShouldAbortCeremony())
            yield break;

        if (goalsContainer != null)
            goalsContainer.gameObject.SetActive(true);

        yield return WaitBlockIntro();

        if (ShouldAbortCeremony())
            yield break;

        if (linesBuilder != null)
            linesBuilder.AddMainObjectiveLine(mainObj);

        yield return new WaitForSecondsRealtime(lineDelay);

        if (ShouldAbortCeremony())
            yield break;

        if (secondaryResults != null && secondaryResults.Count > 0)
        {
            for (int i = 0; i < secondaryResults.Count; i++)
            {
                if (linesBuilder != null)
                    linesBuilder.AddSecondaryObjectiveLine(secondaryResults[i]);

                yield return new WaitForSecondsRealtime(lineDelay);

                if (ShouldAbortCeremony())
                    yield break;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(lineDelay);

            if (ShouldAbortCeremony())
                yield break;
        }

        int totalGoalsBonus = (linesBuilder != null)
            ? linesBuilder.ComputeTotalGoalsBonus(mainObj, secondaryResults)
            : 0;

        if (linesBuilder != null)
            linesBuilder.ShowGoalsTotalLine();

        yield return new WaitForSecondsRealtime(lineDelay);

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
            yield return StartCoroutine(totalsPresenter.AnimateGoalsBonus(totalGoalsBonus));

        yield return new WaitForSecondsRealtime(lineDelay);

        breakdown.GoalsBonus = Mathf.Max(0, totalGoalsBonus);
        breakdown.FinalScore = breakdown.RawScore + breakdown.GoalsBonus;

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(totalsPresenter.WaitForFinalScoreAnimations());
        }

        if (accordionUI != null)
            accordionUI.RefreshGoalsCachedHeight();

        yield return WaitBlockOutro();

        if (ShouldAbortCeremony())
            yield break;

        if (accordionUI != null)
        {
            accordionUI.SetGoalsExpanded(false, instant: false);

            float foldDur = accordionUI.GoalsToggleDurationSec;
            if (foldDur > 0f)
                yield return new WaitForSecondsRealtime(foldDur);

            if (afterFoldDelay > 0f)
                yield return new WaitForSecondsRealtime(afterFoldDelay);
        }

        if (ShouldAbortCeremony())
            yield break;

        if (bonusContainer != null)
            bonusContainer.gameObject.SetActive(true);

        if (accordionUI != null)
            accordionUI.SetCombosExpanded(true, instant: true);

        yield return WaitBlockIntro();

        if (ShouldAbortCeremony())
            yield break;

        if (linesBuilder != null)
            yield return StartCoroutine(linesBuilder.RevealCombos(stats, lineDelay));

        int totalComboPoints = (linesBuilder != null) ? linesBuilder.LastComboPoints : 0;

        if (accordionUI != null)
            accordionUI.RefreshCombosCachedHeight();

        if (linesBuilder != null)
            linesBuilder.ShowCombosTotalLine();

        yield return new WaitForSecondsRealtime(lineDelay);

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
            yield return StartCoroutine(totalsPresenter.AnimateCombosBonus(totalComboPoints));

        yield return new WaitForSecondsRealtime(lineDelay);

        breakdown.CombosBonus = Mathf.Max(0, totalComboPoints);
        breakdown.FinalScore = breakdown.RawScore + breakdown.GoalsBonus + breakdown.CombosBonus;

        if (ShouldAbortCeremony())
            yield break;

        if (totalsPresenter != null)
        {
            totalsPresenter.SetFinalScore(breakdown.FinalScore, animate: true);
            yield return StartCoroutine(totalsPresenter.WaitForFinalScoreAnimations());
        }

        yield return WaitBlockOutro();

        if (ShouldAbortCeremony())
            yield break;

        if (accordionUI != null)
            accordionUI.SetInteractable(true);

        EndLevelOutcome outcome = EndLevelOutcomeBuilder.Build(levelData, mainObj.Achieved, breakdown.FinalScore);

        bronzeThreshold = outcome.BronzeThreshold;
        silverThreshold = outcome.SilverThreshold;
        goldThreshold = outcome.GoldThreshold;

        // Si le token n'a pas ete injecte, on loggue un warning defensif.
        if (!hasToken)
            Debug.LogWarning("[EndLevelUI] Aucun EndLevelToken injecté. OnCeremonyFinished enverra default.");

        if (ShouldAbortCeremony())
            yield break;

        OnCeremonyFinished?.Invoke(outcome, hasToken ? token : default);

        if (hudBottom != null)
            hudBottom.SetActive(true);
    }

    /// <summary>
    /// Lit les thresholds Bronze / Silver / Gold depuis le LevelData.
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