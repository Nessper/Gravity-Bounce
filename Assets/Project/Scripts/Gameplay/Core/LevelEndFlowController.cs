using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gere tout le flow metier de fin de niveau.
///
/// Responsabilites :
/// - recevoir la fin de la Results Ceremony
/// - preparer et commit les consequences de run
/// - resoudre le type final : Victory / Defeat / GameOver
/// - envoyer analytics
/// - attendre le bouton Next de la Results Ceremony
/// - demander a MainUIController la transition globale vers EndResult
/// - demander a EndResultOverlayController d afficher l overlay finale
/// - gerer les callbacks Menu / Retry / Next
///
/// IMPORTANT :
/// - la transition vers EndResult n est PAS automatique apres la ceremony
/// - c est le bouton Next de la Results Ceremony qui doit appeler OnResultsCeremonyNextPressed
/// </summary>
public class LevelEndFlowController : MonoBehaviour
{
    [Header("Results Ceremony")]
    [SerializeField] private ResultsCeremonyOverlayController resultsCeremonyOverlayController;

    [Header("Main UI")]
    [SerializeField] private MainUIController mainUIController;

    [Header("End Result Overlay")]
    [SerializeField] private EndResultOverlayController endResultOverlayController;

    [Header("Run State")]
    [SerializeField] private RunSessionState runSessionState;

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

    private bool navigationLocked;
    private bool forcedGameOver;

    private EndType lastEndType;
    private int lastRemainingContractLives;

    private bool commitPrepared;
    private FinalCommitSnapshot commitSnapshot;

    private enum EndType
    {
        Victory,
        Defeat,
        GameOver
    }

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
        public bool CommitWasAccepted;
    }

    private const string ModulesPackName = "modules";

    private void OnEnable()
    {
        hasOutcome = false;
        hasToken = false;

        navigationLocked = false;
        forcedGameOver = false;

        commitPrepared = false;
        commitSnapshot = default;

        if (resultsCeremonyOverlayController != null)
            resultsCeremonyOverlayController.OnCeremonyFinished += HandleCeremonyFinished;
    }

    private void OnDisable()
    {
        if (resultsCeremonyOverlayController != null)
            resultsCeremonyOverlayController.OnCeremonyFinished -= HandleCeremonyFinished;
    }

    /// <summary>
    /// Callback appele a la fin de la Results Ceremony.
    /// IMPORTANT :
    /// - ne passe PAS automatiquement a EndResult
    /// - le bouton Next de la ceremony doit appeler OnResultsCeremonyNextPressed
    /// </summary>
    private void HandleCeremonyFinished(EndLevelOutcome outcome, EndLevelToken token)
    {
        lastOutcome = outcome;
        hasOutcome = true;

        lastToken = token;
        hasToken = true;

        navigationLocked = false;

        PrepareAndCommitOnce(outcome, token);
        SendAnalytics(outcome, token);
    }

    /// <summary>
    /// Point d entree appele par le bouton Next de la Results Ceremony.
    /// </summary>
    public void OnResultsCeremonyNextPressed()
    {
        ShowEndResultOverlay();
    }

    public void AbortPendingCeremony()
    {
        if (resultsCeremonyOverlayController != null)
            resultsCeremonyOverlayController.AbortCeremony();

        if (mainUIController != null)
            mainUIController.HideResultsCeremonyView();
    }

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

    private void ShowEndResultOverlay()
    {
        if (!hasOutcome || !commitPrepared)
        {
            Debug.LogWarning("[LevelEndFlowController] ShowEndResultOverlay : outcome/commit non pret.");
            return;
        }

        CleanupBallsOnce();

        if (mainUIController != null)
        {
            mainUIController.ShowEndResultView(this, PlayEndResultOverlay);
        }
        else
        {
            PlayEndResultOverlay();
        }
    }

    private void PlayEndResultOverlay()
    {
        if (endResultOverlayController == null)
        {
            Debug.LogWarning("[LevelEndFlowController] EndResultOverlayController manquant.");
            return;
        }

        EndResultOverlayController.EndResultType uiType = ConvertToEndResultType(lastEndType);

        bool showMenu = true;
        bool showRetry = lastEndType == EndType.Defeat && lastRemainingContractLives > 0;
        bool showNext = lastEndType == EndType.Victory;

        string levelName = ResolveLevelTitleForUI();

        bool showMoney = lastOutcome.IsVictory && commitSnapshot.RevealMoney;
        int displayedMoney = showMoney ? commitSnapshot.MoneyAfter : 0;

        List<EndResultOverlayController.MoneyRewardLineData> rewardLines =
            BuildMoneyRewardLineData(commitSnapshot.MoneyRewardLines);

        endResultOverlayController.Play(
            levelName: levelName,
            resultType: uiType,
            levelScore: lastOutcome.FinalScore,
            campaignScoreBefore: commitSnapshot.CampaignBefore,
            campaignScoreAfter: commitSnapshot.CampaignAfter,
            medal: lastOutcome.FinalMedal,
            showMoney: showMoney,
            moneyBefore: commitSnapshot.MoneyBefore,
            moneyAfter: commitSnapshot.MoneyAfter,
            moneyRewardLines: rewardLines,
            dialogSequenceId: commitSnapshot.SequenceId,
            showMenu: showMenu,
            showRetry: showRetry,
            showNext: showNext,
            onMenuClicked: OnClickMenu,
            onRetryClicked: showRetry ? OnClickRetry : null,
            onNextClicked: showNext ? OnClickNext : null
        );
    }

    private List<EndResultOverlayController.MoneyRewardLineData> BuildMoneyRewardLineData(List<MoneyRewardLine> src)
    {
        if (src == null || src.Count == 0)
            return null;

        List<EndResultOverlayController.MoneyRewardLineData> result =
            new List<EndResultOverlayController.MoneyRewardLineData>(src.Count);

        for (int i = 0; i < src.Count; i++)
        {
            result.Add(new EndResultOverlayController.MoneyRewardLineData
            {
                Label = src[i].Label,
                Amount = src[i].Amount
            });
        }

        return result;
    }

    private string ResolveLevelTitleForUI()
    {
        if (hasToken &&
            !string.IsNullOrEmpty(lastToken.LevelId) &&
            LevelCatalogService.TryGet(lastToken.LevelId, out var meta) &&
            meta != null &&
            !string.IsNullOrEmpty(meta.title))
        {
            return meta.title;
        }

        if (runSessionState != null)
        {
            runSessionState.EnsurePlanLoaded();

            RunNode node = runSessionState.CurrentPlayableNode;
            if (node != null &&
                !string.IsNullOrEmpty(node.levelId) &&
                LevelCatalogService.TryGet(node.levelId, out var nodeMeta) &&
                nodeMeta != null &&
                !string.IsNullOrEmpty(nodeMeta.title))
            {
                return nodeMeta.title;
            }
        }

        return string.Empty;
    }

    private void SendAnalytics(EndLevelOutcome outcome, EndLevelToken token)
    {
        if (AlphaAnalytics.Instance == null)
            return;

        string levelId = token.LevelId;

        if (string.IsNullOrEmpty(levelId) && runSessionState != null)
        {
            runSessionState.EnsurePlanLoaded();
            RunNode node = runSessionState.CurrentPlayableNode;
            if (node != null)
                levelId = node.levelId;
        }

        AlphaAnalytics.Instance.SendLevelEnd(
            levelId,
            outcome.IsVictory ? "victory" : "defeat",
            outcome.FinalMedal.ToString().ToLower()
        );

        if (commitSnapshot.RunCompletedAfterCommit)
        {
            AlphaAnalytics.Instance.SendRunEnd(
                levelId,
                true,
                true
            );
        }
        else if (lastEndType == EndType.GameOver)
        {
            AlphaAnalytics.Instance.SendRunEnd(
                levelId,
                false,
                false
            );
        }
    }

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

    private string ResolveSequenceId(EndType type, int remainingContractLives)
    {
        if (type == EndType.Victory)
            return "contract_victory";

        if (type == EndType.GameOver)
        {
            if (forcedGameOver)
                return "hull_destroyed";

            return "contract_gameover";
        }

        if (remainingContractLives >= 2)
            return "contract_defeat_2";

        if (remainingContractLives == 1)
            return "contract_defeat_1";

        return "contract_gameover";
    }

    private EndResultOverlayController.EndResultType ConvertToEndResultType(EndType t)
    {
        if (t == EndType.Victory)
            return EndResultOverlayController.EndResultType.Victory;

        if (t == EndType.GameOver)
            return EndResultOverlayController.EndResultType.GameOver;

        return EndResultOverlayController.EndResultType.Defeat;
    }

    private void UpdateContractLivesInSave(int contractLives)
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Current == null ||
            SaveManager.Instance.Current.runState == null)
        {
            return;
        }

        RunStateData run = SaveManager.Instance.Current.runState;

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
    /// Cette branche continue d afficher directement EndResult.
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

        ShowEndResultOverlay();
    }
}