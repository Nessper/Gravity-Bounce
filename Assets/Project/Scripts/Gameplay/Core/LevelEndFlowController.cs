using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrateur de fin de niveau.
///
/// Responsabilités :
/// - recevoir le snapshot final après la Results Ceremony
/// - appliquer une seule fois les conséquences de run
/// - préparer les données nécessaires à l'overlay EndResult
/// - lancer la transition vers EndResult
/// - gérer les boutons Menu / Retry / Next
///
/// Source de vérité : EndLevelSnapshot.
/// Le token reste uniquement une identité technique anti double-commit.
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
    [SerializeField] private MainExitTransitionController mainExitTransition;

    [Header("Cleanup")]
    [SerializeField] private VoidScrappers.Gameplay.Balls.BallsCleanupService ballsCleanupService;

    private const string ModulesPackName = "modules";

    private EndLevelSnapshot lastSnapshot;
    private EndLevelToken lastToken;
    private bool hasToken;

    private bool navigationLocked;
    private bool forcedGameOver;

    private EndResultState lastEndState;
    private int lastRemainingContractLives;

    private bool commitPrepared;
    private FinalCommitSnapshot commitSnapshot;

    private struct MoneyRewardLine
    {
        public string Label;
        public int Amount;
    }

    /// <summary>
    /// Données préparées après commit pour alimenter l'overlay EndResult.
    /// Ce n'est pas une save, seulement un état runtime d'affichage.
    /// </summary>
    private struct FinalCommitSnapshot
    {
        public EndResultState EndState;
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

    private void OnEnable()
    {
        lastSnapshot = null;
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
    /// Appelé quand la Results Ceremony est terminée.
    /// Ne navigue pas automatiquement : le bouton Next de la cérémonie
    /// appelle ensuite OnResultsCeremonyNextPressed().
    /// </summary>
    private void HandleCeremonyFinished(EndLevelSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        lastSnapshot = snapshot;
        lastToken = snapshot.Token;
        hasToken = true;

        navigationLocked = false;

        PrepareAndCommitOnce(snapshot);
        SendAnalytics(snapshot);
    }

    /// <summary>
    /// Appelé par le bouton Next de la Results Ceremony.
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

    /// <summary>
    /// Applique une seule fois les conséquences de fin de niveau.
    /// Le snapshot donne l'état final ; cette méthode applique seulement
    /// les effets de run : score, money, contrat, progression.
    /// </summary>
    private void PrepareAndCommitOnce(EndLevelSnapshot snapshot)
    {
        if (commitPrepared || snapshot == null)
            return;

        EndLevelToken token = snapshot.Token;
        EndResultState endState = snapshot.EndState;

        int remainingContractLives = runSessionState != null ? runSessionState.ContractLives : 0;

        if (endState != EndResultState.Victory && runSessionState != null)
        {
            runSessionState.LoseContractLife(1);
            remainingContractLives = runSessionState.ContractLives;
            UpdateContractLivesInSave(remainingContractLives);
        }

        lastEndState = endState;
        lastRemainingContractLives = remainingContractLives;

        int campaignBefore = ReadCampaignScore();
        int campaignAfter = campaignBefore;

        int moneyBefore = ReadMoney();
        int moneyAfter = moneyBefore;

        bool revealMoney = false;
        bool runCompletedAfterCommit = false;
        bool commitAccepted = true;

        List<MoneyRewardLine> moneyRewardLines = BuildMoneyRewardLines(snapshot.FinalMedal);
        int totalMoneyReward = ComputeTotalMoneyReward(moneyRewardLines);

        if (endState != EndResultState.Victory)
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.MarkPendingEndSnapshotCommitted(token);
        }
        else
        {
            campaignAfter = campaignBefore + Mathf.Max(0, snapshot.FinalScore);
            WriteCampaignScore(campaignAfter);

            if (totalMoneyReward > 0 && runSessionState != null)
            {
                runSessionState.AddMoney(totalMoneyReward);
                moneyAfter = moneyBefore + totalMoneyReward;
                revealMoney = true;
            }

            if (runSessionState == null)
            {
                Debug.LogError("[LevelEndFlowController] RunSessionState manquant.");
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
                    SaveManager.Instance.MarkPendingEndSnapshotCommitted(token);
            }
        }

        commitSnapshot = new FinalCommitSnapshot
        {
            EndState = endState,
            RemainingContractLives = remainingContractLives,
            CampaignBefore = campaignBefore,
            CampaignAfter = campaignAfter,
            MoneyBefore = moneyBefore,
            MoneyAfter = moneyAfter,
            RevealMoney = revealMoney,
            MoneyRewardLines = moneyRewardLines,
            SequenceId = ResolveSequenceId(endState, remainingContractLives),
            RunCompletedAfterCommit = runCompletedAfterCommit,
            CommitWasAccepted = commitAccepted
        };

        commitPrepared = true;
    }

    private int ComputeTotalMoneyReward(List<MoneyRewardLine> lines)
    {
        if (lines == null || lines.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < lines.Count; i++)
            total += Mathf.Max(0, lines[i].Amount);

        return total;
    }

    private void ShowEndResultOverlay()
    {
        if (lastSnapshot == null || !commitPrepared)
        {
            Debug.LogWarning("[LevelEndFlowController] ShowEndResultOverlay : snapshot/commit non pret.");
            return;
        }

        CleanupBallsOnce();

        if (mainUIController != null)
            mainUIController.ShowEndResultView(this, PlayEndResultOverlay);
        else
            PlayEndResultOverlay();
    }

    private void PlayEndResultOverlay()
    {
        if (endResultOverlayController == null)
        {
            Debug.LogWarning("[LevelEndFlowController] EndResultOverlayController manquant.");
            return;
        }

        EndResultOverlayController.EndResultType uiType = ConvertToEndResultType(lastEndState);

        bool showMenu = true;
        bool showRetry = lastEndState == EndResultState.Retry && lastRemainingContractLives > 0;
        bool showNext = lastEndState == EndResultState.Victory;

        bool showMoney =
            lastSnapshot.EndState == EndResultState.Victory &&
            commitSnapshot.RevealMoney;

        endResultOverlayController.Play(
            levelName: ResolveLevelTitleForUI(),
            resultType: uiType,
            levelScore: lastSnapshot.FinalScore,
            campaignScoreBefore: commitSnapshot.CampaignBefore,
            campaignScoreAfter: commitSnapshot.CampaignAfter,
            medal: lastSnapshot.FinalMedal,
            showMoney: showMoney,
            moneyBefore: commitSnapshot.MoneyBefore,
            moneyAfter: commitSnapshot.MoneyAfter,
            moneyRewardLines: BuildMoneyRewardLineData(commitSnapshot.MoneyRewardLines),
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

    private List<MoneyRewardLine> BuildMoneyRewardLines(EndMedal medal)
    {
        List<MoneyRewardLine> lines = new List<MoneyRewardLine>();

        int medalReward = economyConfig != null
            ? Mathf.Max(0, economyConfig.GetMoneyReward(medal))
            : 0;

        if (medalReward > 0)
        {
            lines.Add(new MoneyRewardLine
            {
                Label = "Medal " + medal,
                Amount = medalReward
            });
        }

        // --------------------------------------------------
        // Famille F : bonus money selon médaille
        // --------------------------------------------------
        if (ModuleRuntimeStats.Instance != null)
        {
            int moduleReward = 0;

            switch (medal)
            {
                case EndMedal.Bronze:
                    moduleReward = ModuleRuntimeStats.Instance.MedalBronzeMoney;
                    break;

                case EndMedal.Silver:
                    moduleReward = ModuleRuntimeStats.Instance.MedalSilverMoney;
                    break;

                case EndMedal.Gold:
                    moduleReward = ModuleRuntimeStats.Instance.MedalGoldMoney;
                    break;
            }

            if (moduleReward > 0)
            {
                lines.Add(new MoneyRewardLine
                {
                    Label = "Medal Bonus",
                    Amount = moduleReward
                });
            }
        }

        if (ModuleRuntimeStats.Instance != null)
        {
            var sustain = ModuleRuntimeStats.Instance.GetEndLevelSustainBonus();

            if (sustain.moneyGain > 0)
            {
                ModuleDefinition sustainMod = ModuleRuntimeStats.Instance.GetEndLevelSustainModule();

                lines.Add(new MoneyRewardLine
                {
                    Label = BuildModuleMoneyLabel(sustainMod),
                    Amount = sustain.moneyGain
                });
            }
        }

        return lines;
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

    private void SendAnalytics(EndLevelSnapshot snapshot)
    {
        if (snapshot == null || AlphaAnalytics.Instance == null)
            return;

        string levelId = snapshot.Token.LevelId;

        if (string.IsNullOrEmpty(levelId) && runSessionState != null)
        {
            runSessionState.EnsurePlanLoaded();

            RunNode node = runSessionState.CurrentPlayableNode;
            if (node != null)
                levelId = node.levelId;
        }

        string result =
            snapshot.EndState == EndResultState.Victory
                ? "victory"
                : snapshot.EndState == EndResultState.GameOver
                    ? "gameover"
                    : "defeat";

        AlphaAnalytics.Instance.SendLevelEnd(
            levelId,
            result,
            snapshot.FinalMedal.ToString().ToLower()
        );

        if (commitSnapshot.RunCompletedAfterCommit)
        {
            AlphaAnalytics.Instance.SendRunEnd(levelId, true, true);
        }
        else if (snapshot.EndState == EndResultState.GameOver)
        {
            AlphaAnalytics.Instance.SendRunEnd(levelId, false, false);
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

        if (lastEndState != EndResultState.Retry)
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

        if (lastEndState != EndResultState.Victory)
            return;

        navigationLocked = true;

        CleanupBallsOnce();

        string levelId = hasToken ? lastToken.LevelId : string.Empty;

        if (mainUIController != null)
        {
            mainUIController.HideEndResultViewAnimated(this, () =>
            {
                PlayMainExitTransition(levelId);
            });
        }
        else
        {
            PlayMainExitTransition(levelId);
        }
    }

    private void PlayMainExitTransition(string levelId)
    {
        if (mainExitTransition != null)
            mainExitTransition.Play(levelId, GoToNextAfterVictory);
        else
            GoToNextAfterVictory();
    }

    private void GoToNextAfterVictory()
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
    }

    private string ResolveSequenceId(EndResultState state, int remainingContractLives)
    {
        if (state == EndResultState.Victory)
            return "contract_victory";

        if (state == EndResultState.GameOver)
            return forcedGameOver ? "hull_destroyed" : "contract_gameover";

        if (remainingContractLives >= 2)
            return "contract_defeat_2";

        if (remainingContractLives == 1)
            return "contract_defeat_1";

        return "contract_gameover";
    }

    private EndResultOverlayController.EndResultType ConvertToEndResultType(EndResultState state)
    {
        if (state == EndResultState.Victory)
            return EndResultOverlayController.EndResultType.Victory;

        if (state == EndResultState.GameOver)
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

        return SaveManager.Instance != null
            ? SaveManager.Instance.GetCurrentRunScore()
            : 0;
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

        return SaveManager.Instance != null
            ? SaveManager.Instance.GetMoney()
            : 0;
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
    /// Branche spéciale GameOver Hull.
    /// Affiche directement EndResult sans Results Ceremony.
    /// </summary>
    public void TriggerGameOverFinalRoutine(int finalScore)
    {
        forcedGameOver = true;

        hasToken = false;
        lastToken = default;

        commitPrepared = false;
        commitSnapshot = default;

        string finalLevelId = string.Empty;
        string worldId = string.Empty;
        int nodeIndex = -1;
        string runId = string.Empty;

        if (runSessionState != null)
        {
            runSessionState.EnsurePlanLoaded();

            RunNode node = runSessionState.CurrentPlayableNode;
            if (node != null && !string.IsNullOrEmpty(node.levelId))
                finalLevelId = node.levelId;
        }

        if (SaveManager.Instance != null)
        {
            RunStateData run = SaveManager.Instance.GetRunState();
            if (run != null)
            {
                runId = run.runId;
                worldId = run.worldId;
                nodeIndex = run.currentNodeIndex;
            }
        }

        EndLevelToken token = new EndLevelToken
        {
            RunId = runId,
            WorldId = worldId,
            LevelId = finalLevelId,
            NodeIndex = nodeIndex,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        EndLevelSnapshot snapshot = new EndLevelSnapshot
        {
            Token = token,
            LevelId = finalLevelId,
            Stats = null,
            MainObjective = default,
            Secondary = null,
            EndState = EndResultState.GameOver,
            FinalScore = Mathf.Max(0, finalScore),
            FinalMedal = EndMedal.None,
            RewardsCommitted = false,
            EvaluatedTimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        lastSnapshot = snapshot;
        lastToken = token;
        hasToken = true;

        navigationLocked = false;

        PrepareAndCommitOnce(snapshot);
        SendAnalytics(snapshot);

        ShowEndResultOverlay();
    }
}