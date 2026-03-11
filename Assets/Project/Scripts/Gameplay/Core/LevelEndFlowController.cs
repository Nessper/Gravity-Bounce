using System;
using System.Collections;
using UnityEngine;

public class LevelEndFlowController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private RunSessionState runSessionState;

    [Header("Dialogs")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Root (parent de finalPanelUI)")]
    [SerializeField] private GameObject endLevelRoot;

    [Header("HUD Gameplay (a masquer endLevelRoot)")]
    [SerializeField] private GameObject hudGameplay;

    [Header("Final Panel UI")]
    [SerializeField] private FinalPanelUI finalPanelUI;

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

    [Header("Cleanup")]
    [SerializeField] private VoidScrappers.Gameplay.Balls.BallsCleanupService ballsCleanupService;

    private enum EndType
    {
        Victory,
        Defeat,
        GameOver
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

        // IMPORTANT: signature avec token
        endLevelUI.OnCeremonyFinished += HandleCeremonyFinished;
    }

    private void OnDisable()
    {
        if (endLevelUI != null)
            endLevelUI.OnCeremonyFinished -= HandleCeremonyFinished;
    }

    private void HandleCeremonyFinished(EndLevelOutcome outcome, EndLevelToken token)
    {
        lastOutcome = outcome;
        hasOutcome = true;

        lastToken = token;
        hasToken = true;

        navigationLocked = false;
        isRunning = false;

        PrepareAndCommitOnce(outcome, token);
    }

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
        StartCoroutine(FinalRoutine(lastOutcome, commitSnapshot));
    }

    private void PrepareAndCommitOnce(EndLevelOutcome outcome, EndLevelToken token)
    {
        if (commitPrepared)
            return;

        int remainingContractLives = (runSessionState != null) ? runSessionState.ContractLives : 0;

        // --------------------------------------------------------
        // 1) Résoudre le type de fin + appliquer les conséquences "défaite"
        //    (LoseContractLife(1)) le cas échéant.
        // --------------------------------------------------------
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

        // --------------------------------------------------------
        // 2) VALIDATION TOKEN vs SAVE (source de vérité)
        //    -> on garde la validation stricte sur Victory (anti double-commit / mauvaise run)
        // --------------------------------------------------------
        if (outcome.IsVictory)
        {
            if (!TryValidateTokenAgainstSave(token))
            {
                commitAccepted = false;
                Debug.LogError("[LevelEndFlowController] Token invalide vs save -> commit Victory refuse (anti double-commit / mauvaise run).");
            }
        }

        // --------------------------------------------------------
        // 3) CRASH-SAFE: on doit marquer le snapshot "committed" aussi en DEFEAT
        //
        // Problème corrigé:
        // - en défaite, ResolveAndApplyEndTypeOnce applique déjà ContractLives -1
        // - si le joueur quit après l'écran de fin, on rejoue la cérémonie au boot
        // - OnCeremonyFinished re-déclenche PrepareAndCommitOnce => re-payait -1
        //
        // Solution:
        // - en fin DEFEAT/GAMEOVER, on marque le snapshot comme committed immédiatement
        //   (car la conséquence a déjà été appliquée).
        //
        // Note:
        // - on ne force pas une validation stricte ici, car le garde-fou "nodeIndex == token.NodeIndex"
        //   est fait côté LevelManager au moment du resume. Ici on veut juste l'idempotence.
        // --------------------------------------------------------
        if (!outcome.IsVictory && commitAccepted && SaveManager.Instance != null)
        {
            SaveManager.Instance.MarkPendingEndSnapshotCommitted(token);
        }

        // --------------------------------------------------------
        // 4) Rewards + Progression commit (Victory only)
        // --------------------------------------------------------
        if (outcome.IsVictory && commitAccepted)
        {
            // 1) Campaign score
            campaignAfter = campaignBefore + Mathf.Max(0, outcome.FinalScore);
            WriteCampaignScore(campaignAfter);

            // 2) Money reward
            int moneyReward = 0;
            if (economyConfig != null)
                moneyReward = Mathf.Max(0, economyConfig.GetMoneyReward(outcome.BestMedal));

            if (moneyReward > 0 && runSessionState != null)
            {
                moneyAfter = moneyBefore + moneyReward;
                revealMoney = true;
                runSessionState.AddMoney(moneyReward);
            }

            // 3) Progression: advance + persist NOW (source de vérité)
            if (runSessionState == null)
            {
                Debug.LogError("[LevelEndFlowController] RunSessionState manquant: impossible de commit la victoire.");
                commitAccepted = false;
            }
            else
            {
                bool ok = runSessionState.CommitVictoryAndAdvanceNode();
                if (!ok)
                {
                    commitAccepted = false;
                    Debug.LogWarning("[LevelEndFlowController] CommitVictoryAndAdvanceNode a échoué (déjà avancé ?).");
                }

                runSessionState.EnsurePlanLoaded();
                runCompletedAfterCommit = runSessionState.IsRunCompleted;

                // ============================================================
                // CRASH-SAFE: en Victory, on marque le snapshot comme committed
                // après le commit métier (score/money/advance).
                // ============================================================
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

    private IEnumerator FinalRoutine(EndLevelOutcome outcome, FinalCommitSnapshot snap)
    {
        isRunning = true;

        if (finalPanelUI == null)
        {
            Debug.LogError("[LevelEndFlowController] FinalPanelUI manquant.");
            isRunning = false;
            yield break;
        }

        finalPanelUI.ResetAll();

        yield return StartCoroutine(PlayDialogById(snap.SequenceId));

        yield return StepDelay();
        yield return StartCoroutine(finalPanelUI.ShowStamp(Convert(snap.EndType)));

        yield return StepDelay();
        finalPanelUI.ShowLevelScorePanel(outcome.FinalScore);

        yield return StepDelay();
        finalPanelUI.ShowCampaignScorePanelInstant(snap.CampaignBefore);

        if (snap.CampaignAfter != snap.CampaignBefore)
            yield return StartCoroutine(finalPanelUI.AnimateCampaignScore(snap.CampaignBefore, snap.CampaignAfter));

        yield return StepDelay();
        yield return StartCoroutine(finalPanelUI.ShowMedal(outcome.BestMedal));

        if (outcome.IsVictory && snap.RevealMoney)
        {
            yield return StepDelay();
            finalPanelUI.ShowMoneyPanelInstant(snap.MoneyBefore);
            yield return StartCoroutine(finalPanelUI.AnimateMoney(snap.MoneyBefore, snap.MoneyAfter));
        }

        yield return StepDelay();
        ShowEndButtons(snap.EndType);

        isRunning = false;
    }

    private IEnumerator StepDelay()
    {
        if (revealStepDelay > 0f)
            yield return new WaitForSecondsRealtime(revealStepDelay);
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

    private void CleanupBallsOnce()
    {
        // On veut pouvoir appeler ça plusieurs fois sans risque (boutons, transition, etc.)
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

        // Nettoyage centralisé AVANT la transition
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

        return (remainingContractLives > 0) ? EndType.Defeat : EndType.GameOver;
    }

    private IEnumerator PlayDialogById(string sequenceId)
    {
        if (dialogSequenceRunner == null)
            yield break;

        if (string.IsNullOrEmpty(sequenceId))
            yield break;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            yield break;

        while (!dialogManager.IsReady)
            yield return null;

        DialogSequence seq = dialogManager.GetSequenceById(sequenceId);
        if (seq == null)
            yield break;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;
        dialogSequenceRunner.Play(lines, () => done = true);

        while (!done)
            yield return null;
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
            BestMedal = EndMedal.None
        };

        hasOutcome = true;

        hasToken = false;
        lastToken = default;

        commitPrepared = false;
        commitSnapshot = default;

        PrepareAndCommitOnce(lastOutcome, default);

        OnClickShowFinalPanel();
    }
}
