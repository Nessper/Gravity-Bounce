using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur de tutoriel ultra cible pour W1-L1.
///
/// Principes :
/// - Le tuto se joue AVANT le vrai StartLevel.
/// - Les billes de tuto sont hors gameplay reel.
/// - Si le joueur rate, on rejoue simplement la meme etape.
/// - Le score / les combos / le hull peuvent vivre normalement pendant le tuto,
///   puis tout est reset proprement a la fin.
/// - Le tuto ne se joue qu une seule fois par sauvegarde.
///
/// Architecture dialogue :
/// - Le tuto passe par DialogSequenceRunner, comme le reste du jeu.
/// - Les messages tuto sont joues en mode interactif.
/// - Les textes et speakers viennent des sequences JSON.
/// - Au retry, on ne rejoue que des phrases courtes dediees.
///
/// IMPORTANT :
/// - Le tuto ne pilote plus directement le dimmer global.
/// - Toute demande visuelle de dimmer passe par MainUIController.
/// </summary>
public class LevelTutorialController : MonoBehaviour
{
    private enum TutorialStepMode
    {
        None,
        White,
        Black,
        Flush,
        BlackHull
    }

    [Header("References gameplay")]
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private VoidTrigger voidTrigger;
    [SerializeField] private BinTrigger leftBinTrigger;
    [SerializeField] private BinTrigger rightBinTrigger;
    [SerializeField] private BinCollector binCollector;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private HullSystem hullSystem;
    [SerializeField] private ComboEngine comboEngine;

    [Header("References UI")]
    [SerializeField] private MainUIController mainUIController;
    [SerializeField] private HullUI hullUI;

    [Header("Configuration generale")]
    [SerializeField] private string tutorialLevelId = "W1-L1";
    [SerializeField] private float pauseAfterDialogSec = 0.2f;
    [SerializeField] private float successPauseSec = 0.8f;
    [SerializeField] private float pauseBetweenStepsSec = 0.4f;
    [SerializeField] private float pauseBeforeTutorialOutroSec = 0.35f;
    [SerializeField] private float pauseAfterTutorialOutroSec = 0.45f;
    [SerializeField] private float pauseBeforeMissionStartSec = 0.35f;
    [SerializeField] private float blackHullFeedbackDelaySec = 0.15f;

    [Header("Tutorial Sequences")]
    [SerializeField] private string tutorialIntroSequenceId = "W1_L1_tutorial_intro";
    [SerializeField] private string tutorialOutroSequenceId = "W1_L1_tutorial_outro";

    [Header("Etape 1 - White")]
    [SerializeField] private string whiteStepSequenceId = "W1_L1_tutorial_white";
    [SerializeField] private string whiteRetrySequenceId = "W1_L1_tutorial_white_retry";
    [SerializeField] private string whiteSuccessSequenceId = "W1_L1_tutorial_white_success";

    [Header("Etape 2 - Black")]
    [SerializeField] private string blackStepSequenceId = "W1_L1_tutorial_black";
    [SerializeField] private string blackRetrySequenceId = "W1_L1_tutorial_black_retry";
    [SerializeField] private string blackSuccessSequenceId = "W1_L1_tutorial_black_success";

    [Header("Etape 3 - Flush")]
    [SerializeField] private string flushStepSequenceId = "W1_L1_tutorial_flush_intro";
    [SerializeField] private string flushRetrySequenceId = "W1_L1_tutorial_flush_retry";
    [SerializeField] private string flushSuccessSequenceId = "W1_L1_tutorial_flush_success";
    [SerializeField] private Side flushTargetSide = Side.Left;
    [SerializeField] private Transform[] flushPrefillSlots;
    [SerializeField] private float flushPrefillTimeoutSec = 2f;

    [Header("Etape 4 - Flush avec bille noire")]
    [SerializeField] private string blackHullStepSequenceId = "W1_L1_tutorial_black_hull_intro";
    [SerializeField] private string blackHullRetrySequenceId = "W1_L1_tutorial_black_hull_retry";
    [SerializeField] private string blackHullSuccessSequenceId = "W1_L1_tutorial_black_hull_success";
    [SerializeField] private Side blackHullTargetSide = Side.Right;
    [SerializeField] private Transform[] blackHullPrefillSlots;
    [SerializeField] private float blackHullPrefillTimeoutSec = 2f;

    [Header("Etape 1 - Bille blanche")]
    [SerializeField] private Vector3 whiteSpawnPosition = new Vector3(0f, 5.8f, -0.2f);
    [SerializeField] private Vector3 whiteVelocity = new Vector3(1.4f, -6.5f, 0f);

    [Header("Etape 2 - Bille noire")]
    [SerializeField] private Vector3 blackSpawnPosition = new Vector3(-0.8f, 5.8f, -0.2f);
    [SerializeField] private Vector3 blackVelocity = new Vector3(0f, -6f, 0f);

    [Header("Etape 3 - Bille bleue de flush")]
    [SerializeField] private Vector3 flushSpawnPosition = new Vector3(0f, 5.8f, -0.2f);
    [SerializeField] private Vector3 flushVelocity = new Vector3(1.2f, -6.2f, 0f);

    [Header("Etape 4 - Bille blanche de demonstration")]
    [SerializeField] private Vector3 blackHullSpawnPosition = new Vector3(0f, 5.8f, -0.2f);
    [SerializeField] private Vector3 blackHullVelocity = new Vector3(-1.2f, -6.2f, 0f);

    private Action onTutorialComplete;
    private Coroutine currentRoutine;

    private GameObject activeTutorialBall;
    private BallState activeTutorialBallState;

    private readonly HashSet<GameObject> tutorialOwnedBalls = new HashSet<GameObject>();

    private bool waitingStepResult;
    private bool stepSucceeded;
    private bool stepFailed;

    private TutorialStepMode currentStepMode = TutorialStepMode.None;

    private int tutorialStartHull;
    private int tutorialStartMaxHull;

    private LocalizationManager Loc => LocalizationManager.Instance;

    public bool ShouldRunForLevel(string levelId)
    {
        if (!string.Equals(levelId, tutorialLevelId, StringComparison.Ordinal))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return true;

        return !SaveManager.Instance.Current.tutorialCompleted;
    }

    public void PlayTutorial(Action onComplete)
    {
        StopTutorialImmediate();

        onTutorialComplete = onComplete;
        currentRoutine = StartCoroutine(TutorialRoutine());
    }

    public void StopTutorialImmediate()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        UnhookEvents();

        CleanupAllTutorialBalls();
        ClearTutorialBinsState();

        waitingStepResult = false;
        stepSucceeded = false;
        stepFailed = false;
        currentStepMode = TutorialStepMode.None;

        mainUIController?.HideTutorialDimmerImmediate();

        if (binCollector != null)
            binCollector.SetAutoFlushEnabled(true);

        mainUIController?.StopAndHideDialog();

        if (hullUI != null)
            hullUI.StopAttentionFlash();
    }

    private IEnumerator TutorialRoutine()
    {
        CaptureInitialRuntimeState();

        playerController?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);

        yield return ShowStepSequence(tutorialIntroSequenceId);

        yield return RunWhiteStep();

        if (pauseBetweenStepsSec > 0f)
            yield return new WaitForSeconds(pauseBetweenStepsSec);

        yield return RunBlackStep();

        if (pauseBetweenStepsSec > 0f)
            yield return new WaitForSeconds(pauseBetweenStepsSec);

        yield return RunFlushStep();

        if (pauseBetweenStepsSec > 0f)
            yield return new WaitForSeconds(pauseBetweenStepsSec);

        yield return RunBlackHullStep();

        if (pauseBeforeTutorialOutroSec > 0f)
            yield return new WaitForSeconds(pauseBeforeTutorialOutroSec);

        // Reset sous overlay, pour que la transition soit invisible.
        ResetGameplayStateAfterTutorial();

        yield return ShowStepSequence(tutorialOutroSequenceId, keepOverlayVisibleAfter: true);

        if (pauseAfterTutorialOutroSec > 0f)
            yield return new WaitForSeconds(pauseAfterTutorialOutroSec);

        yield return FadeOutTutorialDimmer();

        if (pauseBeforeMissionStartSec > 0f)
            yield return new WaitForSeconds(pauseBeforeMissionStartSec);

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            SaveManager.Instance.Current.tutorialCompleted = true;
            SaveManager.Instance.Save();
        }

        currentRoutine = null;
        onTutorialComplete?.Invoke();
    }

    private void CaptureInitialRuntimeState()
    {
        tutorialStartHull = 0;
        tutorialStartMaxHull = 1;

        if (hullSystem != null)
        {
            tutorialStartHull = hullSystem.GetCurrentHull();
            tutorialStartMaxHull = hullSystem.GetMaxHull();
        }
    }

    private void ResetGameplayStateAfterTutorial()
    {
        UnhookEvents();

        CleanupAllTutorialBalls();
        ClearTutorialBinsState();

        if (binCollector != null)
            binCollector.SetAutoFlushEnabled(true);

        if (hullUI != null)
            hullUI.StopAttentionFlash();

        mainUIController?.ShowTutorialDimmerImmediate();

        scoreManager?.ResetForLevelStart(0);
        comboEngine?.ResetRuntimeState();

        if (hullSystem != null)
            hullSystem.RestoreRuntimeState(tutorialStartHull, tutorialStartMaxHull);
    }

    private IEnumerator RunWhiteStep()
    {
        bool completed = false;
        bool firstAttempt = true;
        currentStepMode = TutorialStepMode.White;

        while (!completed)
        {
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            yield return ShowStepSequence(firstAttempt ? whiteStepSequenceId : whiteRetrySequenceId);

            SpawnActiveTutorialBall(whiteSpawnPosition, whiteVelocity, BallType.White);

            ResetStepState();
            HookEvents();

            while (waitingStepResult)
                yield return null;

            if (stepSucceeded && successPauseSec > 0f)
                yield return new WaitForSeconds(successPauseSec);

            UnhookEvents();
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            if (stepSucceeded)
            {
                yield return ShowStepSequence(whiteSuccessSequenceId, keepOverlayVisibleAfter: true);
                completed = true;
            }
            else
            {
                firstAttempt = false;
            }
        }

        currentStepMode = TutorialStepMode.None;
    }

    private IEnumerator RunBlackStep()
    {
        bool completed = false;
        bool firstAttempt = true;
        currentStepMode = TutorialStepMode.Black;

        while (!completed)
        {
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            yield return ShowStepSequence(firstAttempt ? blackStepSequenceId : blackRetrySequenceId);

            SpawnActiveTutorialBall(blackSpawnPosition, blackVelocity, BallType.Black);

            ResetStepState();
            HookEvents();

            while (waitingStepResult)
                yield return null;

            if (stepSucceeded && successPauseSec > 0f)
                yield return new WaitForSeconds(successPauseSec);

            UnhookEvents();
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            if (stepSucceeded)
            {
                yield return ShowStepSequence(blackSuccessSequenceId, keepOverlayVisibleAfter: true);
                completed = true;
            }
            else
            {
                firstAttempt = false;
            }
        }

        currentStepMode = TutorialStepMode.None;
    }

    private IEnumerator RunFlushStep()
    {
        bool completed = false;
        bool firstAttempt = true;
        currentStepMode = TutorialStepMode.Flush;

        while (!completed)
        {
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            if (binCollector != null)
                binCollector.SetAutoFlushEnabled(false);

            yield return StartCoroutine(PrepareFlushPrefillRoutine(BallType.White, 4));

            yield return ShowStepSequence(firstAttempt ? flushStepSequenceId : flushRetrySequenceId);

            SpawnActiveTutorialBall(flushSpawnPosition, flushVelocity, BallType.Blue);

            ResetStepState();
            HookEvents();

            while (waitingStepResult)
                yield return null;

            if (stepSucceeded && successPauseSec > 0f)
                yield return new WaitForSeconds(successPauseSec);

            UnhookEvents();
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            if (binCollector != null)
                binCollector.SetAutoFlushEnabled(true);

            if (stepSucceeded)
            {
                yield return ShowStepSequence(flushSuccessSequenceId, keepOverlayVisibleAfter: true);
                completed = true;
            }
            else
            {
                firstAttempt = false;
            }
        }

        currentStepMode = TutorialStepMode.None;
    }

    private IEnumerator RunBlackHullStep()
    {
        bool completed = false;
        bool firstAttempt = true;
        currentStepMode = TutorialStepMode.BlackHull;

        while (!completed)
        {
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            if (binCollector != null)
                binCollector.SetAutoFlushEnabled(false);

            yield return StartCoroutine(PrepareBlackHullPrefillRoutine());

            yield return ShowStepSequence(firstAttempt ? blackHullStepSequenceId : blackHullRetrySequenceId);

            SpawnActiveTutorialBall(blackHullSpawnPosition, blackHullVelocity, BallType.White);

            ResetStepState();
            HookEvents();

            while (waitingStepResult)
                yield return null;

            if (stepSucceeded && successPauseSec > 0f)
                yield return new WaitForSeconds(successPauseSec);

            UnhookEvents();
            CleanupAllTutorialBalls();
            ClearTutorialBinsState();

            if (binCollector != null)
                binCollector.SetAutoFlushEnabled(true);

            if (stepSucceeded)
            {
                if (hullUI != null)
                    hullUI.StartAttentionFlashLoop();

                if (blackHullFeedbackDelaySec > 0f)
                    yield return new WaitForSeconds(blackHullFeedbackDelaySec);

                yield return ShowStepSequence(blackHullSuccessSequenceId, keepOverlayVisibleAfter: true);

                if (hullUI != null)
                    hullUI.StopAttentionFlash();

                completed = true;
            }
            else
            {
                if (hullUI != null)
                    hullUI.StopAttentionFlash();

                firstAttempt = false;
            }
        }

        currentStepMode = TutorialStepMode.None;
    }

    private IEnumerator PrepareFlushPrefillRoutine(BallType prefillType, int targetCount)
    {
        if (flushPrefillSlots == null || flushPrefillSlots.Length < targetCount)
        {
            Debug.LogError("[LevelTutorialController] Il faut au moins " + targetCount + " slots de prefill.");
            yield break;
        }

        BinTrigger targetTrigger = GetTrigger(flushTargetSide);
        if (targetTrigger == null)
        {
            Debug.LogError("[LevelTutorialController] BinTrigger cible introuvable pour le prefill.");
            yield break;
        }

        for (int i = 0; i < targetCount; i++)
        {
            Transform slot = flushPrefillSlots[i];
            SpawnTutorialPrefillBall(slot, prefillType);
        }

        float elapsed = 0f;

        while (targetTrigger.Count < targetCount && elapsed < flushPrefillTimeoutSec)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (targetTrigger.Count < targetCount)
            Debug.LogWarning("[LevelTutorialController] Prefill incomplet avant timeout. Count=" + targetTrigger.Count);
    }

    private IEnumerator PrepareBlackHullPrefillRoutine()
    {
        const int targetCount = 4;

        if (blackHullPrefillSlots == null || blackHullPrefillSlots.Length < targetCount)
        {
            Debug.LogError("[LevelTutorialController] Il faut au moins 4 slots de prefill pour l etape BlackHull.");
            yield break;
        }

        BinTrigger targetTrigger = GetTrigger(blackHullTargetSide);
        if (targetTrigger == null)
        {
            Debug.LogError("[LevelTutorialController] BinTrigger cible introuvable pour l etape BlackHull.");
            yield break;
        }

        SpawnTutorialPrefillBall(blackHullPrefillSlots[0], BallType.White);
        SpawnTutorialPrefillBall(blackHullPrefillSlots[1], BallType.White);
        SpawnTutorialPrefillBall(blackHullPrefillSlots[2], BallType.White);
        SpawnTutorialPrefillBall(blackHullPrefillSlots[3], BallType.Black);

        float elapsed = 0f;

        while (targetTrigger.Count < targetCount && elapsed < blackHullPrefillTimeoutSec)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (targetTrigger.Count < targetCount)
            Debug.LogWarning("[LevelTutorialController] Prefill BlackHull incomplet avant timeout. Count=" + targetTrigger.Count);
    }

    private void SpawnTutorialPrefillBall(Transform slot, BallType type)
    {
        if (slot == null || ballSpawner == null)
            return;

        GameObject go = ballSpawner.SpawnTutorialBall(
            slot.position,
            Vector3.zero,
            type,
            releasePhysics: true,
            applyInitialVelocity: false
        );

        if (go != null)
            RegisterTutorialBall(go);
    }

    private IEnumerator ShowStepSequence(string sequenceId, bool keepOverlayVisibleAfter = false)
    {
        if (string.IsNullOrWhiteSpace(sequenceId))
            yield break;

        if (Loc == null)
        {
            Debug.LogError("[LevelTutorialController] LocalizationManager.Instance est null.");
            yield break;
        }

        while (!Loc.IsReady)
            yield return null;

        DialogSequence sequence = Loc.GetSequenceById(sequenceId);
        if (sequence == null)
        {
            Debug.LogError("[LevelTutorialController] Sequence tuto introuvable : " + sequenceId);
            yield break;
        }

        DialogLine[] lines = Loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
        {
            Debug.LogError("[LevelTutorialController] Sequence tuto vide : " + sequenceId);
            yield break;
        }

        yield return ShowTutorialDimmer();

        bool dialogDone = false;

        mainUIController?.PlayDialogSequence(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () => dialogDone = true
        );

        while (!dialogDone)
            yield return null;

        if (pauseAfterDialogSec > 0f)
            yield return new WaitForSeconds(pauseAfterDialogSec);

        if (!keepOverlayVisibleAfter)
            yield return HideTutorialDimmer();
    }

    private IEnumerator FadeOutTutorialDimmer()
    {
        yield return HideTutorialDimmer();
    }

    private IEnumerator ShowTutorialDimmer()
    {
        if (mainUIController == null)
            yield break;

        bool done = false;

        mainUIController.ShowTutorialDimmer(
            this,
            () => done = true
        );

        while (!done)
            yield return null;
    }

    private IEnumerator HideTutorialDimmer()
    {
        if (mainUIController == null)
            yield break;

        bool done = false;

        mainUIController.HideTutorialDimmer(
            this,
            () => done = true
        );

        while (!done)
            yield return null;
    }

    private void SpawnActiveTutorialBall(Vector3 position, Vector3 velocity, BallType type)
    {
        CleanupActiveTutorialBallOnly();

        if (ballSpawner == null)
        {
            Debug.LogError("[LevelTutorialController] ballSpawner manquant.");
            return;
        }

        activeTutorialBall = ballSpawner.SpawnTutorialBall(
            position,
            velocity,
            type,
            releasePhysics: true,
            applyInitialVelocity: true
        );

        activeTutorialBallState = activeTutorialBall != null
            ? activeTutorialBall.GetComponent<BallState>()
            : null;

        RegisterTutorialBall(activeTutorialBall);
    }

    private void RegisterTutorialBall(GameObject go)
    {
        if (go == null)
            return;

        tutorialOwnedBalls.Add(go);
    }

    private void CleanupActiveTutorialBallOnly()
    {
        if (activeTutorialBall == null)
            return;

        if (ballSpawner != null)
            ballSpawner.DestroyTutorialBall(activeTutorialBall);
        else
            Destroy(activeTutorialBall);

        tutorialOwnedBalls.Remove(activeTutorialBall);
        activeTutorialBall = null;
        activeTutorialBallState = null;
    }

    private void CleanupAllTutorialBalls()
    {
        foreach (GameObject go in tutorialOwnedBalls)
        {
            if (go == null)
                continue;

            if (ballSpawner != null)
                ballSpawner.DestroyTutorialBall(go);
            else
                Destroy(go);
        }

        tutorialOwnedBalls.Clear();
        activeTutorialBall = null;
        activeTutorialBallState = null;
    }

    private void ClearTutorialBinsState()
    {
        ClearTrigger(leftBinTrigger);
        ClearTrigger(rightBinTrigger);
    }

    private void ClearTrigger(BinTrigger trigger)
    {
        if (trigger == null)
            return;

        trigger.TakeSnapshotAndClear();
    }

    private void ResetStepState()
    {
        waitingStepResult = true;
        stepSucceeded = false;
        stepFailed = false;
    }

    private void HookEvents()
    {
        if (playerController != null)
            playerController.OnBallCollision += HandlePlayerBallCollision;

        if (voidTrigger != null)
            voidTrigger.OnTutorialBallLost += HandleTutorialBallLost;

        if (leftBinTrigger != null)
            leftBinTrigger.OnBallEnteredBin += HandleBallEnteredBin;

        if (rightBinTrigger != null)
            rightBinTrigger.OnBallEnteredBin += HandleBallEnteredBin;

        if (binCollector != null)
            binCollector.OnBinFlushed += HandleBinFlushed;
    }

    private void UnhookEvents()
    {
        if (playerController != null)
            playerController.OnBallCollision -= HandlePlayerBallCollision;

        if (voidTrigger != null)
            voidTrigger.OnTutorialBallLost -= HandleTutorialBallLost;

        if (leftBinTrigger != null)
            leftBinTrigger.OnBallEnteredBin -= HandleBallEnteredBin;

        if (rightBinTrigger != null)
            rightBinTrigger.OnBallEnteredBin -= HandleBallEnteredBin;

        if (binCollector != null)
            binCollector.OnBinFlushed -= HandleBinFlushed;
    }

    private void HandlePlayerBallCollision(Collision collision)
    {
        if (!waitingStepResult || activeTutorialBallState == null)
            return;

        BallState otherBall = collision.collider.GetComponent<BallState>();
        if (otherBall == null || otherBall != activeTutorialBallState)
            return;
    }

    private void HandleTutorialBallLost(BallState lostBall)
    {
        if (!waitingStepResult || activeTutorialBallState == null)
            return;

        if (lostBall != activeTutorialBallState)
            return;

        activeTutorialBall = null;
        activeTutorialBallState = null;

        if (currentStepMode == TutorialStepMode.White && lostBall.type == BallType.White)
        {
            stepFailed = true;
            waitingStepResult = false;
            return;
        }

        if (currentStepMode == TutorialStepMode.Black && lostBall.type == BallType.Black)
        {
            stepSucceeded = true;
            waitingStepResult = false;
            return;
        }

        if (currentStepMode == TutorialStepMode.Flush && lostBall.type == BallType.Blue)
        {
            stepFailed = true;
            waitingStepResult = false;
            return;
        }

        if (currentStepMode == TutorialStepMode.BlackHull && lostBall.type == BallType.White)
        {
            stepFailed = true;
            waitingStepResult = false;
        }
    }

    private void HandleBallEnteredBin(BallState enteredBall, Side side)
    {
        if (!waitingStepResult || activeTutorialBallState == null)
            return;

        if (enteredBall != activeTutorialBallState)
            return;

        if (currentStepMode == TutorialStepMode.White)
        {
            if (enteredBall.type == BallType.White)
            {
                stepSucceeded = true;
                waitingStepResult = false;
            }

            return;
        }

        if (currentStepMode == TutorialStepMode.Black)
        {
            if (enteredBall.type == BallType.Black)
            {
                stepFailed = true;
                waitingStepResult = false;
            }

            return;
        }

        if (currentStepMode == TutorialStepMode.Flush)
        {
            if (side != flushTargetSide)
            {
                stepFailed = true;
                waitingStepResult = false;
                return;
            }

            if (binCollector != null)
            {
                binCollector.CollectFromBin(
                    side,
                    force: false,
                    skipDelay: true,
                    isFinalFlush: false,
                    isTutorialFlush: true
                );
            }

            return;
        }

        if (currentStepMode == TutorialStepMode.BlackHull)
        {
            if (side != blackHullTargetSide)
            {
                stepFailed = true;
                waitingStepResult = false;
                return;
            }

            if (binCollector != null)
            {
                binCollector.CollectFromBin(
                    side,
                    force: false,
                    skipDelay: true,
                    isFinalFlush: false,
                    isTutorialFlush: true
                );
            }
        }
    }

    private void HandleBinFlushed(Side side, BinSnapshot snapshot, int blackCount)
    {
        if (!waitingStepResult)
            return;

        if (currentStepMode == TutorialStepMode.Flush)
        {
            if (side != flushTargetSide)
                return;

            if (blackCount > 0)
                return;

            activeTutorialBall = null;
            activeTutorialBallState = null;

            stepSucceeded = true;
            waitingStepResult = false;
            return;
        }

        if (currentStepMode == TutorialStepMode.BlackHull)
        {
            if (side != blackHullTargetSide)
                return;

            if (blackCount <= 0)
                return;

            activeTutorialBall = null;
            activeTutorialBallState = null;

            stepSucceeded = true;
            waitingStepResult = false;
        }
    }

    private BinTrigger GetTrigger(Side side)
    {
        if (side == Side.Left)
            return leftBinTrigger;

        if (side == Side.Right)
            return rightBinTrigger;

        return null;
    }
}