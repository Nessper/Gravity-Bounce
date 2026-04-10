using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gere tout le flow de fin de niveau :
/// - recupere l outcome final depuis EndLevelUI
/// - prepare et commit les consequences de run
/// - affiche le panneau final
/// - joue les dialogues de fin
/// - revele score / campaign score / medal / money
/// - affiche les boutons finaux
///
/// Cette version integre un hold-to-skip partage pour la reveal routine finale.
/// Le skip :
/// - coupe le dialogue si necessaire
/// - coupe les delais
/// - coupe les animations en cours
/// - force le panneau final dans son etat final
/// - affiche les boutons
/// </summary>
public class LevelEndFlowController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private RunSessionState runSessionState;

    [Header("Dialogs")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Hold To Skip")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

    [Header("Root (parent de finalPanelUI)")]
    [SerializeField] private GameObject endLevelRoot;

    [Header("HUD Gameplay (a masquer avant endLevelRoot)")]
    [SerializeField] private GameObject hudGameplay;

    [Header("Final Panel UI")]
    [SerializeField] private FinalPanelUI finalPanelUI;

    [Header("Final Score Bar UI")]
    [SerializeField] private FinalScoreBarUI finalScoreBarUI;

    [Header("Panels UI (switch HUD_Bottom -> Final_Panel)")]
    [SerializeField] private GameObject principalPanel;
    [SerializeField] private GameObject hudBottom;
    [SerializeField] private GameObject finalPanel;

    [Header("Dialog sequences (ids du JSON)")]
    [SerializeField] private string victorySequenceId = "contract_victory";
    [SerializeField] private string defeatTwoLivesSequenceId = "contract_defeat_2";
    [SerializeField] private string defeatOneLifeSequenceId = "contract_defeat_1";
    [SerializeField] private string gameOverSequenceId = "contract_gameover";
    [SerializeField] private string hullDestroyedSequenceId = "hull_destroyed";

    [Header("Reveal Timing (unscaled)")]
    [SerializeField] private float revealStepDelay = 0.25f;

    [Header("HUD EndLevel Buttons")]
    [SerializeField] private EndLevelButtonsUI endLevelButtonsUI;

    [Header("Economy")]
    [SerializeField] private EconomyConfig economyConfig;

    [Header("Next Transition")]
    [SerializeField] private NextTransitionController nextTransition;

    [Header("Cleanup")]
    [SerializeField] private VoidScrappers.Gameplay.Balls.BallsCleanupService ballsCleanupService;

    private EndLevelOutcome lastOutcome;
    private bool hasOutcome;

    private EndLevelToken lastToken;
    private bool hasToken;

    private bool isRunning;
    private bool navigationLocked;
    private bool forcedGameOver;

    private EndType lastEndType;
    private int lastRemainingContractLives;

    private bool commitPrepared;
    private FinalCommitSnapshot commitSnapshot;

    // Etat runtime du hold-to-skip pour la reveal finale.
    private bool revealSkipRequested;

    private LocalizationManager Loc => LocalizationManager.Instance;

    private enum EndType
    {
        Victory,
        Defeat,
        GameOver
    }

    /// <summary>
    /// Represente une ligne de recompense d argent revelee
    /// une par une dans le panneau final.
    /// </summary>
    private struct MoneyRewardLine
    {
        public string Label;
        public int Amount;
    }

    private struct FinalCommitSnapshot
    {
        public EndType EndType;
        public int RemainingContractLives;

        public int CampaignBefore;
        public int CampaignAfter;

        public int MoneyBefore;
        public int MoneyAfter;
        public bool RevealMoney;
        public List<MoneyRewardLine> MoneyRewardLines;

        public string SequenceId;

        public bool RunCompletedAfterCommit;

        // Debug / safety
        public bool CommitWasAccepted;
    }

    private void OnEnable()
    {
        if (endLevelUI == null)
        {
            Debug.LogError("[LevelEndFlowController] EndLevelUI manquant.");
            return;
        }

        hasOutcome = false;
        hasToken = false;

        isRunning = false;
        navigationLocked = false;
        forcedGameOver = false;

        commitPrepared = false;
        commitSnapshot = default;

        revealSkipRequested = false;

        endLevelUI.OnCeremonyFinished += HandleCeremonyFinished;
    }

    private void OnDisable()
    {
        if (endLevelUI != null)
            endLevelUI.OnCeremonyFinished -= HandleCeremonyFinished;

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Callback appele a la fin de la ceremonie normale.
    /// </summary>
    private void HandleCeremonyFinished(EndLevelOutcome outcome, EndLevelToken token)
    {
        lastOutcome = outcome;
        hasOutcome = true;

        lastToken = token;
        hasToken = true;

        navigationLocked = false;
        isRunning = false;

        PrepareAndCommitOnce(outcome, token);

        if (AlphaAnalytics.Instance != null)
        {
            AlphaAnalytics.Instance.SendLevelEnd(
                token.LevelId,
                outcome.IsVictory ? "victory" : "defeat",
                outcome.FinalMedal.ToString().ToLower()
            );

            if (commitSnapshot.RunCompletedAfterCommit)
            {
                AlphaAnalytics.Instance.SendRunEnd(
                    token.LevelId,
                    true,
                    true
                );
            }
            else if (lastEndType == EndType.GameOver)
            {
                AlphaAnalytics.Instance.SendRunEnd(
                    token.LevelId,
                    false,
                    false
                );
            }
        }
    }

    /// <summary>
    /// Annule explicitement toute ceremonie / flow de fin en cours.
    /// Cette methode est utilisee par le GameOver Hull pour prendre la main.
    /// </summary>
    public void AbortPendingCeremony()
    {
        if (endLevelUI != null)
            endLevelUI.AbortCeremony();

        StopAllCoroutines();

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        revealSkipRequested = false;
        isRunning = false;
        navigationLocked = false;
    }

    /// <summary>
    /// Affiche le panneau final (Victory / Defeat / GameOver).
    /// </summary>
    public void OnClickShowFinalPanel()
    {
        if (isRunning)
            return;

        if (!hasOutcome || !commitPrepared)
        {
            Debug.LogWarning("[LevelEndFlowController] OnClickShowFinalPanel: outcome/commit non pret.");
            return;
        }

        if (hudGameplay != null)
            hudGameplay.SetActive(false);

        if (endLevelRoot != null && !endLevelRoot.activeSelf)
            endLevelRoot.SetActive(true);

        if (principalPanel != null)
            principalPanel.SetActive(false);

        if (hudBottom != null)
            hudBottom.SetActive(false);

        if (finalPanel != null)
            finalPanel.SetActive(true);

        endLevelButtonsUI?.HideAll();
        CleanupBallsOnce();

        StopAllCoroutines();

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        revealSkipRequested = false;

        StartCoroutine(FinalRoutine(lastOutcome, commitSnapshot));
    }

    /// <summary>
    /// Callback du hold-to-skip pendant la reveal finale.
    /// </summary>
    public void OnFinalRevealSkipRequested()
    {
        if (!isRunning || revealSkipRequested)
            return;

        revealSkipRequested = true;

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Prepare les consequences de fin de niveau et les commits de progression.
    /// Cette methode ne doit s executer qu une seule fois par fin de niveau.
    /// </summary>
    private void PrepareAndCommitOnce(EndLevelOutcome outcome, EndLevelToken token)
    {
        if (commitPrepared)
            return;

        int remainingContractLives = runSessionState != null ? runSessionState.ContractLives : 0;

        EndType endType = ResolveAndApplyEndTypeOnce(out remainingContractLives, outcome);

        lastEndType = endType;
        lastRemainingContractLives = remainingContractLives;

        int campaignBefore = ReadCampaignScore();
        int campaignAfter = campaignBefore;

        int moneyBefore = ReadMoney();
        int moneyAfter = moneyBefore;

        bool revealMoney = false;
        bool runCompletedAfterCommit = false;
        bool commitAccepted = true;

        List<MoneyRewardLine> moneyRewardLines = new List<MoneyRewardLine>();

        if (!outcome.IsVictory && commitAccepted && SaveManager.Instance != null)
        {
            SaveManager.Instance.MarkPendingEndSnapshotCommitted(token);
        }

        if (outcome.IsVictory && commitAccepted)
        {
            campaignAfter = campaignBefore + Mathf.Max(0, outcome.FinalScore);
            WriteCampaignScore(campaignAfter);

            int moneyReward = 0;

            if (economyConfig != null)
                moneyReward = Mathf.Max(0, economyConfig.GetMoneyReward(outcome.FinalMedal));

            if (moneyReward > 0)
            {
                moneyRewardLines.Add(new MoneyRewardLine
                {
                    Label = "Medal " + outcome.FinalMedal,
                    Amount = moneyReward
                });
            }

            if (ModuleRuntimeStats.Instance != null)
            {
                var sustain = ModuleRuntimeStats.Instance.GetEndLevelSustainBonus();

                if (sustain.moneyGain > 0)
                {
                    ModuleDefinition sustainMod = ModuleRuntimeStats.Instance.GetEndLevelSustainModule();

                    string label = BuildModuleMoneyLabel(sustainMod);

                    moneyRewardLines.Add(new MoneyRewardLine
                    {
                        Label = label,
                        Amount = sustain.moneyGain
                    });
                }
            }

            int totalMoneyReward = 0;
            for (int i = 0; i < moneyRewardLines.Count; i++)
            {
                totalMoneyReward += Mathf.Max(0, moneyRewardLines[i].Amount);
            }

            if (totalMoneyReward > 0 && runSessionState != null)
            {
                runSessionState.AddMoney(totalMoneyReward);

                moneyAfter = moneyBefore + totalMoneyReward;
                revealMoney = true;
            }

            if (runSessionState == null)
            {
                Debug.LogError("[LevelEndFlowController] RunSessionState manquant : impossible de commit la victoire.");
                commitAccepted = false;
            }
            else
            {
                bool ok = runSessionState.CommitVictoryAndAdvanceNode();

                if (!ok)
                {
                    commitAccepted = false;
                    Debug.LogWarning("[LevelEndFlowController] CommitVictoryAndAdvanceNode a echoue.");
                }

                runSessionState.EnsurePlanLoaded();
                runCompletedAfterCommit = runSessionState.IsRunCompleted;

                if (commitAccepted && SaveManager.Instance != null)
                {
                    SaveManager.Instance.MarkPendingEndSnapshotCommitted(token);
                }
            }
        }

        string sequenceId = ResolveSequenceId(endType, remainingContractLives);

        commitSnapshot = new FinalCommitSnapshot
        {
            EndType = endType,
            RemainingContractLives = remainingContractLives,
            CampaignBefore = campaignBefore,
            CampaignAfter = campaignAfter,
            MoneyBefore = moneyBefore,
            MoneyAfter = moneyAfter,
            RevealMoney = revealMoney,
            MoneyRewardLines = moneyRewardLines,
            SequenceId = sequenceId,
            RunCompletedAfterCommit = runCompletedAfterCommit,
            CommitWasAccepted = commitAccepted
        };

        commitPrepared = true;
    }

    private bool TryValidateTokenAgainstSave(EndLevelToken token)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return false;

        if (string.IsNullOrEmpty(run.runId) || string.IsNullOrEmpty(token.RunId))
            return false;

        if (!string.Equals(run.runId, token.RunId, StringComparison.Ordinal))
            return false;

        if (!string.Equals(run.worldId, token.WorldId, StringComparison.Ordinal))
            return false;

        if (run.currentNodeIndex != token.NodeIndex)
            return false;

        return true;
    }

    /// <summary>
    /// Routine de reveal du panneau final.
    /// Le hold-to-skip est arme pendant toute cette routine.
    /// Si le joueur skip, on force l etat final du panneau.
    /// </summary>
    private IEnumerator FinalRoutine(EndLevelOutcome outcome, FinalCommitSnapshot snap)
    {
        isRunning = true;
        revealSkipRequested = false;

        if (finalPanelUI == null)
        {
            Debug.LogError("[LevelEndFlowController] FinalPanelUI manquant.");
            isRunning = false;
            yield break;
        }

        finalPanelUI.ResetAll();
        finalPanelUI.SetMedalInstant(EndMedal.None);

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Show(this, OnFinalRevealSkipRequested);

        yield return StartCoroutine(PlayDialogByIdSkippable(snap.SequenceId));

        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        yield return StartCoroutine(StepDelaySkippable());
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        yield return StartCoroutine(RunSkippable(finalPanelUI.ShowStamp(Convert(snap.EndType))));
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        yield return StartCoroutine(StepDelaySkippable());
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        finalPanelUI.ShowLevelScorePanel(outcome.FinalScore);

        yield return StartCoroutine(StepDelaySkippable());
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        finalPanelUI.ShowCampaignScorePanelInstant(snap.CampaignBefore);

        if (snap.CampaignAfter != snap.CampaignBefore)
        {
            yield return StartCoroutine(
                RunSkippable(finalPanelUI.AnimateCampaignScore(snap.CampaignBefore, snap.CampaignAfter))
            );

            if (revealSkipRequested)
            {
                ApplyFinalRevealState(outcome, snap);
                EndFinalRoutine();
                yield break;
            }
        }

        yield return StartCoroutine(StepDelaySkippable());
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        yield return StartCoroutine(RunSkippable(finalPanelUI.ShowMedal(outcome.FinalMedal)));
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        if (outcome.IsVictory && snap.RevealMoney)
        {
            int currentMoney = snap.MoneyBefore;

            yield return StartCoroutine(StepDelaySkippable());
            if (revealSkipRequested)
            {
                ApplyFinalRevealState(outcome, snap);
                EndFinalRoutine();
                yield break;
            }

            finalPanelUI.ShowMoneyPanelInstant(currentMoney);

            if (snap.MoneyRewardLines != null && snap.MoneyRewardLines.Count > 0)
            {
                for (int i = 0; i < snap.MoneyRewardLines.Count; i++)
                {
                    MoneyRewardLine line = snap.MoneyRewardLines[i];
                    if (line.Amount <= 0)
                        continue;

                    yield return StartCoroutine(StepDelaySkippable());
                    if (revealSkipRequested)
                    {
                        ApplyFinalRevealState(outcome, snap);
                        EndFinalRoutine();
                        yield break;
                    }

                    yield return StartCoroutine(
                        RunSkippable(finalPanelUI.ShowMoneyRewardToast(line.Label, line.Amount))
                    );

                    if (revealSkipRequested)
                    {
                        ApplyFinalRevealState(outcome, snap);
                        EndFinalRoutine();
                        yield break;
                    }

                    int nextMoney = currentMoney + line.Amount;

                    yield return StartCoroutine(
                        RunSkippable(finalPanelUI.AnimateMoney(currentMoney, nextMoney))
                    );

                    if (revealSkipRequested)
                    {
                        ApplyFinalRevealState(outcome, snap);
                        EndFinalRoutine();
                        yield break;
                    }

                    currentMoney = nextMoney;
                }
            }
        }

        yield return StartCoroutine(StepDelaySkippable());
        if (revealSkipRequested)
        {
            ApplyFinalRevealState(outcome, snap);
            EndFinalRoutine();
            yield break;
        }

        ShowEndButtons(snap.EndType);
        EndFinalRoutine();
    }

    /// <summary>
    /// Termine proprement la reveal routine.
    /// </summary>
    private void EndFinalRoutine()
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        isRunning = false;
        revealSkipRequested = false;
    }

    /// <summary>
    /// Force le panneau final dans son etat final.
    /// Utilise les methodes instantanees disponibles.
    /// </summary>
    private void ApplyFinalRevealState(EndLevelOutcome outcome, FinalCommitSnapshot snap)
    {
        if (finalPanelUI == null)
            return;

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        finalPanelUI.ResetAll();
        finalPanelUI.SetStampInstant(Convert(snap.EndType));
        finalPanelUI.ShowLevelScorePanel(outcome.FinalScore);
        finalPanelUI.ShowCampaignScorePanelInstant(snap.CampaignAfter);
        finalPanelUI.SetMedalInstant(outcome.FinalMedal);

        if (outcome.IsVictory && snap.RevealMoney)
            finalPanelUI.ShowMoneyPanelInstant(snap.MoneyAfter);

        ShowEndButtons(snap.EndType);
    }

    /// <summary>
    /// Attend un delai unscaled, mais peut etre interrompu par le skip.
    /// </summary>
    private IEnumerator StepDelaySkippable()
    {
        if (revealStepDelay <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < revealStepDelay)
        {
            if (revealSkipRequested)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Execute une coroutine skippable.
    /// Si le skip arrive, on stoppe la coroutine enfant immediatement.
    /// </summary>
    private IEnumerator RunSkippable(IEnumerator routine)
    {
        if (routine == null)
            yield break;

        bool done = false;
        Coroutine child = StartCoroutine(WrapRoutine(routine, () => done = true));

        while (!done)
        {
            if (revealSkipRequested)
            {
                if (child != null)
                    StopCoroutine(child);

                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Wrapper utilitaire pour savoir quand une coroutine est terminee.
    /// </summary>
    private IEnumerator WrapRoutine(IEnumerator routine, Action onDone)
    {
        yield return StartCoroutine(routine);
        onDone?.Invoke();
    }

    private void ShowEndButtons(EndType endType)
    {
        if (endLevelButtonsUI == null)
            return;

        if (endType == EndType.Victory)
            endLevelButtonsUI.ShowVictory();
        else if (endType == EndType.Defeat)
            endLevelButtonsUI.ShowDefeat();
        else
            endLevelButtonsUI.ShowGameOver();
    }

    /// <summary>
    /// Nettoie les billes restantes de maniere idempotente.
    /// </summary>
    private void CleanupBallsOnce()
    {
        if (ballsCleanupService == null)
            return;

        ballsCleanupService.ClearAllBalls();
    }

    private void CleanupAndNavigate(Action navigationAction)
    {
        CleanupBallsOnce();
        navigationAction?.Invoke();
    }

    public void OnClickMenu()
    {
        if (navigationLocked)
            return;

        navigationLocked = true;
        CleanupAndNavigate(() => BootRoot.GameFlow.GoToTitle());
    }

    public void OnClickRetry()
    {
        if (navigationLocked)
            return;

        if (lastEndType != EndType.Defeat)
            return;

        if (lastRemainingContractLives <= 0)
            return;

        navigationLocked = true;
        CleanupAndNavigate(() => BootRoot.GameFlow.RetryLevel());
    }

    public void OnClickNext()
    {
        if (navigationLocked)
            return;

        if (lastEndType != EndType.Victory)
            return;

        navigationLocked = true;

        Action go = () =>
        {
            if (runSessionState == null)
            {
                Debug.LogError("[LevelEndFlowController] RunSessionState manquant.");
                BootRoot.GameFlow.GoToTitle();
                return;
            }

            runSessionState.EnsurePlanLoaded();

            RunNode node = runSessionState.CurrentPlayableNode;
            if (node != null && node.type == RunNodeType.Ending)
            {
                Debug.Log("[LevelEndFlowController] Next -> Ending -> StartCredits.");
                BootRoot.GameFlow.StartCredits();
                return;
            }

            if (runSessionState.IsRunCompleted)
            {
                Debug.Log("[LevelEndFlowController] Run completed -> StartCredits.");
                BootRoot.GameFlow.StartCredits();
                return;
            }

            BootRoot.GameFlow.GoToRunHub();
        };

        CleanupBallsOnce();

        if (nextTransition != null)
            nextTransition.PlayOutroAndFinish(go);
        else
            go();
    }

    /// <summary>
    /// Resout le type de fin.
    /// - Victory : aucune perte de vie de contrat.
    /// - Defeat : on retire 1 vie de contrat.
    /// - GameOver force : priorite absolue.
    /// </summary>
    private EndType ResolveAndApplyEndTypeOnce(out int remainingContractLives, EndLevelOutcome outcome)
    {
        remainingContractLives = runSessionState != null ? runSessionState.ContractLives : 0;

        if (forcedGameOver)
            return EndType.GameOver;

        if (outcome.IsVictory)
            return EndType.Victory;

        if (runSessionState != null)
        {
            runSessionState.LoseContractLife(1);
            remainingContractLives = runSessionState.ContractLives;
            UpdateContractLivesInSave(remainingContractLives);
        }

        return remainingContractLives > 0 ? EndType.Defeat : EndType.GameOver;
    }

    /// <summary>
    /// Joue une sequence de dialogue de fin si elle existe.
    /// Cette version est skippable via le hold-to-skip.
    /// </summary>
    private IEnumerator PlayDialogByIdSkippable(string sequenceId)
    {
        if (dialogSequenceRunner == null)
            yield break;

        if (string.IsNullOrWhiteSpace(sequenceId))
            yield break;

        if (Loc == null)
        {
            Debug.LogError("[LevelEndFlowController] LocalizationManager.Instance est null.");
            yield break;
        }

        while (!Loc.IsReady)
        {
            if (revealSkipRequested)
                yield break;

            yield return null;
        }

        DialogSequence sequence = Loc.GetSequenceById(sequenceId);
        if (sequence == null)
            yield break;

        DialogLine[] lines = Loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;

        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () => done = true
        );

        while (!done)
        {
            if (revealSkipRequested)
            {
                dialogSequenceRunner.StopAndHide();
                yield break;
            }

            yield return null;
        }
    }

    private string ResolveSequenceId(EndType type, int remainingContractLives)
    {
        if (type == EndType.Victory)
            return victorySequenceId;

        if (type == EndType.GameOver)
        {
            if (forcedGameOver)
                return hullDestroyedSequenceId;

            return gameOverSequenceId;
        }

        if (remainingContractLives >= 2)
            return defeatTwoLivesSequenceId;

        if (remainingContractLives == 1)
            return defeatOneLifeSequenceId;

        return gameOverSequenceId;
    }

    private FinalPanelUI.FinalEndType Convert(EndType t)
    {
        if (t == EndType.Victory)
            return FinalPanelUI.FinalEndType.Victory;

        if (t == EndType.GameOver)
            return FinalPanelUI.FinalEndType.GameOver;

        return FinalPanelUI.FinalEndType.Defeat;
    }

    /// <summary>
    /// Persiste proprement les vies de contrat restantes.
    /// </summary>
    private void UpdateContractLivesInSave(int contractLives)
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Current == null ||
            SaveManager.Instance.Current.runState == null)
        {
            return;
        }

        var run = SaveManager.Instance.Current.runState;

        int clamped = Mathf.Max(0, contractLives);
        run.remainingContractLives = clamped;

        if (clamped <= 0)
            run.hasOngoingRun = false;

        SaveManager.Instance.Save();
    }

    private int ReadCampaignScore()
    {
        if (runSessionState != null)
            return Mathf.Max(0, runSessionState.RunScore);

        if (SaveManager.Instance == null)
            return 0;

        return SaveManager.Instance.GetCurrentRunScore();
    }

    private void WriteCampaignScore(int newScore)
    {
        int clamped = Mathf.Max(0, newScore);

        if (runSessionState != null)
        {
            runSessionState.SetRunScore(clamped);
            return;
        }

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetCurrentRunScore(clamped);
    }

    private int ReadMoney()
    {
        if (runSessionState != null)
            return Mathf.Max(0, runSessionState.Money);

        if (SaveManager.Instance == null)
            return 0;

        return SaveManager.Instance.GetMoney();
    }

    /// <summary>
    /// Construit le label lisible pour une ligne de recompense money issue d un module.
    /// </summary>
    private const string ModulesPackName = "modules";

    private string BuildModuleMoneyLabel(ModuleDefinition mod)
    {
        if (mod == null)
            return "Module";

        string displayName = GetLocalizedModuleName(mod);
        int tier = Mathf.Max(0, mod.tier);

        if (tier <= 0)
            return "Module " + displayName;

        return "Module " + displayName + " T" + tier;
    }

    private string GetLocalizedModuleName(ModuleDefinition mod)
    {
        if (mod == null)
            return "Unknown";

        if (string.IsNullOrWhiteSpace(mod.displayNameLocKey))
            return "Unknown";

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            return mod.displayNameLocKey;

        return LocalizationManager.Instance.GetTextOrKey(ModulesPackName, mod.displayNameLocKey);
    }

    /// <summary>
    /// Branche speciale GameOver Hull.
    /// - force le type de fin a GameOver
    /// - prepare un outcome minimal
    /// - rafraichit le header de l overlay
    /// - affiche ensuite le panneau final
    /// </summary>
    public void TriggerGameOverFinalRoutine(int finalScore)
    {
        forcedGameOver = true;

        lastOutcome = new EndLevelOutcome
        {
            IsVictory = false,
            FinalScore = Mathf.Max(0, finalScore),
            BronzeThreshold = 0,
            SilverThreshold = 0,
            GoldThreshold = 0,
            FinalMedal = EndMedal.None
        };

        hasOutcome = true;

        hasToken = false;
        lastToken = default;

        commitPrepared = false;
        commitSnapshot = default;

        string finalLevelId = string.Empty;

        if (runSessionState != null)
        {
            runSessionState.EnsurePlanLoaded();

            RunNode node = runSessionState.CurrentPlayableNode;
            if (node != null && !string.IsNullOrEmpty(node.levelId))
                finalLevelId = node.levelId;
        }

        if (endLevelUI != null && !string.IsNullOrEmpty(finalLevelId))
        {
            if (LevelCatalogService.TryGet(finalLevelId, out var meta))
                endLevelUI.ShowHeaderOnly(finalLevelId, meta);
            else
                endLevelUI.ShowHeaderOnly(finalLevelId, null);
        }

        PrepareAndCommitOnce(lastOutcome, default);

        if (AlphaAnalytics.Instance != null)
        {
            AlphaAnalytics.Instance.SendLevelEnd(
                finalLevelId,
                "gameover",
                "none"
            );

            AlphaAnalytics.Instance.SendRunEnd(
                finalLevelId,
                false,
                false
            );
        }

        OnClickShowFinalPanel();
    }
}