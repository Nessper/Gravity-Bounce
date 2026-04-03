using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controleur de tutoriel ultra cible pour W1-L1.
///
/// Principes :
/// - Le tuto se joue AVANT le vrai StartLevel.
/// - Les billes de tuto sont hors gameplay reel.
/// - Si le joueur rate, on rejoue simplement la meme etape.
/// - Aucune pollution du score, des objectifs, du hull ou des stats.
/// - Le tuto ne se joue qu une seule fois par sauvegarde.
///
/// Architecture dialogue :
/// - Le tuto passe par DialogSequenceRunner, comme le reste du jeu.
/// - Les messages tuto sont joues en mode interactif.
/// </summary>
public class LevelTutorialController : MonoBehaviour
{
    [Header("References gameplay")]
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private VoidTrigger voidTrigger;
    [SerializeField] private BinTrigger leftBinTrigger;
    [SerializeField] private BinTrigger rightBinTrigger;

    [Header("References UI")]
    [SerializeField] private DialogSequenceRunner dialogRunner;
    [SerializeField] private CanvasGroup darkOverlay;

    [Header("Configuration generale")]
    [SerializeField] private string tutorialLevelId = "W1-L1";
    [SerializeField] private float pauseAfterDialogSec = 0.2f;
    [SerializeField] private float successPauseSec = 0.8f;
    [SerializeField] private float pauseBetweenStepsSec = 0.4f;
    [SerializeField] private float pauseBeforeMissionStartSec = 0.7f;

    [Header("Textes")]
    [TextArea]
    [SerializeField] private string tutorialIntroText = "[TUTORIAL]\nLet's review the basics.";

    [TextArea]
    [SerializeField] private string whiteStepText = "Move the paddle to bounce the ball into a bin.";

    [TextArea]
    [SerializeField] private string whiteSuccessText = "Well done.";

    [TextArea]
    [SerializeField] private string blackStepText = "Black balls are dangerous.\nPress SHIFT to close the bins and reject them.";

    [TextArea]
    [SerializeField] private string blackSuccessText = "Nice work.\nYou are ready to start the mission.";

    [Header("Etape 1 - Bille blanche")]
    [SerializeField] private Vector3 whiteSpawnPosition = new Vector3(0f, 5.8f, -0.2f);
    [SerializeField] private Vector3 whiteVelocity = new Vector3(1.4f, -6.5f, 0f);

    [Header("Etape 2 - Bille noire")]
    [SerializeField] private Vector3 blackSpawnPosition = new Vector3(-0.8f, 5.8f, -0.2f);
    [SerializeField] private Vector3 blackVelocity = new Vector3(0f, -6f, 0f);

    private Action onTutorialComplete;
    private Coroutine currentRoutine;

    // Reference runtime vers la bille de tuto active
    private GameObject activeTutorialBall;
    private BallState activeTutorialBallState;

    // Etat runtime d une etape
    private bool waitingStepResult;
    private bool stepSucceeded;
    private bool stepFailed;

    /// <summary>
    /// Retourne true si le tuto doit etre joue pour ce niveau.
    /// Regles :
    /// - seulement sur tutorialLevelId
    /// - seulement si pas encore termine dans la sauvegarde
    /// </summary>
    public bool ShouldRunForLevel(string levelId)
    {
        if (!string.Equals(levelId, tutorialLevelId, StringComparison.Ordinal))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return true;

        return !SaveManager.Instance.Current.tutorialCompleted;
    }

    /// <summary>
    /// Lance le tuto puis invoque onComplete a la fin.
    /// </summary>
    public void PlayTutorial(Action onComplete)
    {
        StopTutorialImmediate();

        onTutorialComplete = onComplete;
        currentRoutine = StartCoroutine(TutorialRoutine());
    }

    /// <summary>
    /// Stoppe tout le tuto immediatement et nettoie l etat runtime.
    /// </summary>
    public void StopTutorialImmediate()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        UnhookEvents();
        CleanupActiveTutorialBall();

        waitingStepResult = false;
        stepSucceeded = false;
        stepFailed = false;

        SetOverlayVisible(false);

        if (dialogRunner != null)
            dialogRunner.StopAndHide();
    }

    /// <summary>
    /// Sequence complete du tuto.
    /// </summary>
    private IEnumerator TutorialRoutine()
    {
        // Securite : au cas ou les controles ne seraient pas deja actifs.
        playerController?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);

        // Intro generale du tuto
        yield return ShowStepMessage(tutorialIntroText);

        // Etape blanche
        yield return RunWhiteStep();

        if (pauseBetweenStepsSec > 0f)
            yield return new WaitForSeconds(pauseBetweenStepsSec);

        // Etape noire
        yield return RunBlackStep();

        // Message final
        yield return ShowStepMessage(blackSuccessText);

        if (pauseBeforeMissionStartSec > 0f)
            yield return new WaitForSeconds(pauseBeforeMissionStartSec);

        CleanupActiveTutorialBall();
        SetOverlayVisible(false);

        // Marque le tuto comme termine dans la sauvegarde
        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            SaveManager.Instance.Current.tutorialCompleted = true;
            SaveManager.Instance.Save();
        }

        currentRoutine = null;
        onTutorialComplete?.Invoke();
    }

    /// <summary>
    /// Etape 1 :
    /// - afficher le message
    /// - spawn la bille blanche
    /// - succes si la bille entre dans un bin
    /// - echec si elle tombe dans le void
    /// - en cas d echec, on rejoue simplement l etape
    /// </summary>
    private IEnumerator RunWhiteStep()
    {
        bool completed = false;

        while (!completed)
        {
            yield return ShowStepMessage(whiteStepText);

            SpawnTutorialBall(whiteSpawnPosition, whiteVelocity, BallType.White);

            ResetStepState();
            HookEvents();

            while (waitingStepResult)
                yield return null;

            if (stepSucceeded && successPauseSec > 0f)
                yield return new WaitForSeconds(successPauseSec);

            UnhookEvents();
            CleanupActiveTutorialBall();

            if (stepSucceeded)
            {
                yield return ShowStepMessage(whiteSuccessText);
                completed = true;
            }
        }
    }

    /// <summary>
    /// Etape 2 :
    /// - afficher le message
    /// - spawn la bille noire
    /// - succes si la bille finit dans le void
    /// - echec si elle entre dans un bin
    /// - en cas d echec, on rejoue simplement l etape
    /// </summary>
    private IEnumerator RunBlackStep()
    {
        bool completed = false;

        while (!completed)
        {
            yield return ShowStepMessage(blackStepText);

            SpawnTutorialBall(blackSpawnPosition, blackVelocity, BallType.Black);

            ResetStepState();
            HookEvents();

            while (waitingStepResult)
                yield return null;

            if (stepSucceeded && successPauseSec > 0f)
                yield return new WaitForSeconds(successPauseSec);

            UnhookEvents();
            CleanupActiveTutorialBall();

            if (stepSucceeded)
                completed = true;
        }
    }

    /// <summary>
    /// Affiche un message de tuto avec overlay sombre.
    /// Le message passe par DialogSequenceRunner en mode interactif.
    /// </summary>
    private IEnumerator ShowStepMessage(string text)
    {
        SetOverlayVisible(true);

        bool dialogDone = false;

        if (dialogRunner != null)
        {
            DialogLine[] lines = new DialogLine[]
            {
                new DialogLine
                {
                    speakerId = string.Empty,
                    text = text
                }
            };

            dialogRunner.Play(
                lines,
                DialogSequenceRunner.PlaybackMode.Interactive,
                () => dialogDone = true
            );

            while (!dialogDone)
                yield return null;
        }

        if (pauseAfterDialogSec > 0f)
            yield return new WaitForSeconds(pauseAfterDialogSec);

        SetOverlayVisible(false);
    }

    /// <summary>
    /// Spawn une bille de tuto isolee du pool gameplay.
    /// </summary>
    private void SpawnTutorialBall(Vector3 position, Vector3 velocity, BallType type)
    {
        CleanupActiveTutorialBall();

        if (ballSpawner == null)
        {
            Debug.LogError("[LevelTutorialController] ballSpawner manquant.");
            return;
        }

        activeTutorialBall = ballSpawner.SpawnTutorialBall(position, velocity, type);
        activeTutorialBallState = activeTutorialBall != null
            ? activeTutorialBall.GetComponent<BallState>()
            : null;
    }

    /// <summary>
    /// Detruit la bille de tuto active si elle existe encore.
    /// Cette bille ne retourne jamais dans le pool gameplay.
    /// </summary>
    private void CleanupActiveTutorialBall()
    {
        if (activeTutorialBall == null)
            return;

        if (ballSpawner != null)
            ballSpawner.DestroyTutorialBall(activeTutorialBall);
        else
            Destroy(activeTutorialBall);

        activeTutorialBall = null;
        activeTutorialBallState = null;
    }

    /// <summary>
    /// Reset de l etat logique de l etape courante.
    /// </summary>
    private void ResetStepState()
    {
        waitingStepResult = true;
        stepSucceeded = false;
        stepFailed = false;
    }

    /// <summary>
    /// Attache les events utilises pendant l etape courante.
    /// </summary>
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
    }

    /// <summary>
    /// Detache les events utilises pendant l etape courante.
    /// </summary>
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
    }

    /// <summary>
    /// Collision paddle / balle.
    /// Ici, on ne valide rien directement.
    /// Pour la blanche, la validation se fait a l entree dans un bin.
    /// </summary>
    private void HandlePlayerBallCollision(Collision collision)
    {
        if (!waitingStepResult || activeTutorialBallState == null)
            return;

        BallState otherBall = collision.collider.GetComponent<BallState>();
        if (otherBall == null || otherBall != activeTutorialBallState)
            return;

        // Pas de validation directe ici.
    }

    /// <summary>
    /// Recoit la notification de perte d une bille de tuto via le VoidTrigger.
    /// Regles :
    /// - blanche -> echec
    /// - noire -> succes
    ///
    /// Important :
    /// la bille a deja ete detruite par le VoidTrigger, on neutralise donc
    /// nos references locales pour eviter tout double destroy.
    /// </summary>
    private void HandleTutorialBallLost(BallState lostBall)
    {
        if (!waitingStepResult || activeTutorialBallState == null)
            return;

        if (lostBall != activeTutorialBallState)
            return;

        activeTutorialBall = null;
        activeTutorialBallState = null;

        if (lostBall.type == BallType.White)
        {
            stepFailed = true;
            waitingStepResult = false;
            return;
        }

        if (lostBall.type == BallType.Black)
        {
            stepSucceeded = true;
            waitingStepResult = false;
        }
    }

    /// <summary>
    /// Recoit la notification d entree d une bille dans un bin.
    /// Regles :
    /// - blanche dans un bin -> succes
    /// - noire dans un bin -> echec
    /// </summary>
    private void HandleBallEnteredBin(BallState enteredBall, Side side)
    {
        if (!waitingStepResult || activeTutorialBallState == null)
            return;

        if (enteredBall != activeTutorialBallState)
            return;

        if (enteredBall.type == BallType.White)
        {
            stepSucceeded = true;
            waitingStepResult = false;
            return;
        }

        if (enteredBall.type == BallType.Black)
        {
            stepFailed = true;
            waitingStepResult = false;
        }
    }

    /// <summary>
    /// Active ou desactive l overlay sombre.
    /// La couleur / opacite visuelle doivent etre reglees dans l Image du panel.
    /// </summary>
    private void SetOverlayVisible(bool visible)
    {
        if (darkOverlay == null)
            return;

        darkOverlay.alpha = visible ? 1f : 0f;
        darkOverlay.blocksRaycasts = visible;
        darkOverlay.interactable = false;
    }
}