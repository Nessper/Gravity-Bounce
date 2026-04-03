using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gere la sequence d intro de niveau :
/// - lock des controles
/// - etat visuel initial
/// - animation du vaisseau
/// - dialogues d intro
/// - assemblage du plateau
/// - fade de l overlay
/// - HUD on
/// - countdown
/// - callback final
///
/// Hold to skip :
/// - utilise un overlay partage
/// - ce controller ne fait jamais de ForceHideImmediate()
/// - il fait seulement Show(this, ...) et Hide(this)
/// </summary>
public class LevelIntroSequenceController : MonoBehaviour
{
    [Header("Core refs")]
    [SerializeField] private LevelControlsController controlsController;
    [SerializeField] private CountdownUI countdownUI;

    [Header("Intro HUD")]
    [SerializeField] private GameObject introHUDRoot;

    [Header("Dialogs")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Tooltip("Identifiant de niveau injecte par LevelManager.")]
    [SerializeField] private string levelId = "W1-L1";

    [Header("Hold To Skip")]
    [SerializeField] private HoldToSkipOverlayUI holdToSkipOverlay;

    [Header("Visual Intro")]
    [SerializeField] private CanvasGroup introOverlayCanvasGroup;
    [SerializeField] private GameObject boardRoot;
    [SerializeField] private float overlayInitialAlpha = 0.9f;

    [Header("Ship Intro")]
    [SerializeField] private Transform shipRoot;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private float shipEnterDuration = 2f;
    [SerializeField] private float shipOffscreenMarginWorld = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float shipStartFromBottomFactor = 0.33f;

    private Vector3 shipStartWorldPosition;
    private Vector3 shipEndWorldPosition;
    private bool shipIntroEnabled;

    [Header("Board Intro")]
    [SerializeField] private BoardIntroAssembler boardIntroAssembler;
    [SerializeField] private float delayBeforeBoardAssembly = 0.3f;

    [Header("Gameplay HUD")]
    [SerializeField] private GameObject topHUDRoot;

    [Header("Timing")]
    [SerializeField] private float delayBeforeCountdown = 0.3f;

    [Header("Overlay Fade")]
    [SerializeField] private float overlayFadeDuration = 0.3f;

    [Header("Music")]
    [SerializeField] private bool playGameplayMusicDuringIntro = true;

    [Range(0f, 1f)]
    [SerializeField] private float introMusicVolumeMult = 0.25f;

    [SerializeField] private float introMusicDuckFadeSec = 0.3f;
    [SerializeField] private float introMusicUnduckFadeSec = 0.5f;
    [SerializeField] private bool unduckBeforeCountdown = true;
    [SerializeField] private float gameplayMusicFadeOutSec = 0.8f;
    [SerializeField] private float gameplayMusicFadeInSec = 0.8f;

    private bool skipRequested;
    private Action onCompleteCallback;
    private Coroutine playRoutine;
    private bool debugSkip;

    /// <summary>
    /// Permet au LevelManager d injecter le levelId courant.
    /// </summary>
    public void ConfigureLevelId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        levelId = id;
    }

    /// <summary>
    /// Lance l intro complete.
    /// </summary>
    public void Play(Action onComplete)
    {
        if (debugSkip)
        {
            onComplete?.Invoke();
            return;
        }

        onCompleteCallback = onComplete;
        skipRequested = false;

        StopIntro();
        SetupInitialVisualState();

        if (playGameplayMusicDuringIntro)
        {
            LevelMusicDirector musicDirector = FindFirstObjectByType<LevelMusicDirector>();

            if (musicDirector != null)
                musicDirector.PlayGameplayMusic();

            AudioManager.Instance?.SetMusicVolumeMultiplier(introMusicVolumeMult, introMusicDuckFadeSec);
        }

        if (gameObject.activeInHierarchy)
            playRoutine = StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// Stoppe proprement l intro en cours et relache l overlay si on le possede.
    /// </summary>
    public void StopIntro()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (countdownUI != null)
            countdownUI.Hide();

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Active ou non le bypass debug.
    /// </summary>
    public void SetDebugSkip(bool value)
    {
        debugSkip = value;
    }

    /// <summary>
    /// Relache l overlay si ce controller est desactive en plein milieu.
    /// </summary>
    private void OnDisable()
    {
        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Prepare l etat visuel initial de l intro.
    /// </summary>
    private void SetupInitialVisualState()
    {
        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.gameObject.SetActive(true);
            introOverlayCanvasGroup.alpha = overlayInitialAlpha;
            introOverlayCanvasGroup.blocksRaycasts = true;
            introOverlayCanvasGroup.interactable = true;
        }

        if (introHUDRoot != null)
            introHUDRoot.SetActive(true);

        if (boardRoot != null)
            boardRoot.SetActive(true);

        if (topHUDRoot != null)
            topHUDRoot.SetActive(false);

        if (boardIntroAssembler != null)
            boardIntroAssembler.PrepareInitialState();

        shipIntroEnabled = false;

        if (shipRoot == null)
            return;

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            shipEndWorldPosition = shipRoot.position;
            shipStartWorldPosition = shipEndWorldPosition;
            return;
        }

        SpriteRenderer sr = shipRoot.GetComponentInChildren<SpriteRenderer>();
        shipEndWorldPosition = shipRoot.position;

        if (sr == null)
        {
            shipStartWorldPosition = shipEndWorldPosition;
            return;
        }

        shipIntroEnabled = true;

        float camBottomY = cam.transform.position.y - cam.orthographicSize;
        float halfHeight = sr.bounds.extents.y;

        float targetTopY = camBottomY - shipOffscreenMarginWorld;
        float extremeStartY = targetTopY - halfHeight;

        Vector3 extremeStartPos = new Vector3(
            shipEndWorldPosition.x,
            extremeStartY,
            shipEndWorldPosition.z
        );

        shipStartWorldPosition = Vector3.Lerp(
            shipEndWorldPosition,
            extremeStartPos,
            Mathf.Clamp01(shipStartFromBottomFactor)
        );

        shipRoot.position = shipStartWorldPosition;
    }

    /// <summary>
    /// Routine principale de l intro.
    /// </summary>
    private IEnumerator PlayRoutine()
    {
        if (controlsController != null)
            controlsController.DisableGameplayControls();

        bool shipDone = !shipIntroEnabled;
        bool dialogsDone = false;
        bool boardDone = (boardIntroAssembler == null);

        if (shipIntroEnabled)
            StartCoroutine(PlayShipEntranceSequence(() => shipDone = true));

        DialogLine[] introLines = null;
        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager != null)
        {
            while (!dialogManager.IsReady)
                yield return null;

            string seqId = BuildIntroSequenceId();
            if (!string.IsNullOrEmpty(seqId))
            {
                DialogSequence sequence = dialogManager.GetSequenceById(seqId);
                if (sequence != null)
                    introLines = dialogManager.GetRandomVariantLines(sequence);
            }
        }

        if (dialogSequenceRunner != null && introLines != null && introLines.Length > 0)
        {
            if (holdToSkipOverlay != null)
                holdToSkipOverlay.Show(this, OnSkipButtonPressed);

            dialogSequenceRunner.Play(
                introLines,
                DialogSequenceRunner.PlaybackMode.Interactive,
                () => dialogsDone = true
            );
        }
        else
        {
            dialogsDone = true;
        }

        while (!shipDone && !skipRequested)
            yield return null;

        if (!skipRequested && delayBeforeBoardAssembly > 0f)
            yield return new WaitForSeconds(delayBeforeBoardAssembly);

        if (!skipRequested && boardIntroAssembler != null)
            StartCoroutine(BoardAssemblyRoutine(() => boardDone = true));

        while (!dialogsDone && !skipRequested)
            yield return null;

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);

        if (!skipRequested)
            yield return StartCoroutine(FadeIntroOverlayOnly());

        while (!boardDone && !skipRequested)
            yield return null;

        if (skipRequested)
            yield break;

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        ActivateAllBoardRootChildren();

        if (controlsController != null)
            controlsController.ShowMobileControlsUI(true);

        if (unduckBeforeCountdown && AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolumeMultiplier(1f, introMusicUnduckFadeSec);

        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        if (countdownUI != null)
        {
            bool countdownDone = false;
            countdownUI.PlayStartCountdown(() => countdownDone = true);
            while (!countdownDone)
                yield return null;
        }

        onCompleteCallback?.Invoke();
    }

    /// <summary>
    /// Lance l assemblage du plateau.
    /// </summary>
    private IEnumerator BoardAssemblyRoutine(Action onComplete)
    {
        yield return boardIntroAssembler.PlayAssembly();
        onComplete?.Invoke();
    }

    /// <summary>
    /// Construit l identifiant de sequence d intro.
    /// Exemple : W1-L2 -> W1_L2_intro
    /// </summary>
    private string BuildIntroSequenceId()
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        string normalized = levelId.Replace("-", "_");
        return normalized + "_intro";
    }

    /// <summary>
    /// Callback declenche quand le hold est complete.
    /// </summary>
    public void OnSkipButtonPressed()
    {
        if (skipRequested)
            return;

        skipRequested = true;

        StopIntro();
        ForceIntroSkippedState();

        if (gameObject.activeInHierarchy)
            StartCoroutine(SkipToCountdownRoutine());
    }

    /// <summary>
    /// Force l intro dans son etat final saute.
    /// </summary>
    private void ForceIntroSkippedState()
    {
        if (shipIntroEnabled && shipRoot != null)
            shipRoot.position = shipEndWorldPosition;

        if (boardRoot != null)
            boardRoot.SetActive(true);

        if (boardIntroAssembler != null)
            boardIntroAssembler.ForceAssembledState();

        ActivateAllBoardRootChildren();

        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.alpha = 0f;
            introOverlayCanvasGroup.blocksRaycasts = false;
            introOverlayCanvasGroup.interactable = false;
        }

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        if (controlsController != null)
            controlsController.ShowMobileControlsUI(true);

        if (holdToSkipOverlay != null)
            holdToSkipOverlay.Hide(this);
    }

    /// <summary>
    /// Termine le skip par le countdown normal.
    /// </summary>
    private IEnumerator SkipToCountdownRoutine()
    {
        if (unduckBeforeCountdown && AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolumeMultiplier(1f, introMusicUnduckFadeSec);

        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        if (countdownUI != null)
        {
            bool countdownDone = false;
            countdownUI.PlayStartCountdown(() => countdownDone = true);
            while (!countdownDone)
                yield return null;
        }

        onCompleteCallback?.Invoke();
    }

    /// <summary>
    /// Anime l entree du vaisseau.
    /// </summary>
    private IEnumerator PlayShipEntranceSequence(Action onComplete)
    {
        if (!shipIntroEnabled || shipRoot == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float duration = Mathf.Max(0.01f, shipEnterDuration);
        float elapsed = 0f;

        shipRoot.position = shipStartWorldPosition;

        while (elapsed < duration)
        {
            if (skipRequested)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            shipRoot.position = Vector3.Lerp(shipStartWorldPosition, shipEndWorldPosition, easedT);
            yield return null;
        }

        shipRoot.position = shipEndWorldPosition;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Fait disparaitre l overlay noir de l intro.
    /// </summary>
    private IEnumerator FadeIntroOverlayOnly()
    {
        if (introOverlayCanvasGroup == null)
            yield break;

        float duration = Mathf.Max(0.01f, overlayFadeDuration);
        float elapsed = 0f;

        float startAlpha = introOverlayCanvasGroup.alpha;

        while (elapsed < duration)
        {
            if (skipRequested)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float a = Mathf.Lerp(startAlpha, 0f, t);
            introOverlayCanvasGroup.alpha = a;

            yield return null;
        }

        introOverlayCanvasGroup.alpha = 0f;
        introOverlayCanvasGroup.blocksRaycasts = false;
        introOverlayCanvasGroup.interactable = false;
    }

    /// <summary>
    /// Reactive explicitement tous les enfants du boardRoot.
    /// </summary>
    private void ActivateAllBoardRootChildren()
    {
        if (boardRoot == null)
            return;

        Transform root = boardRoot.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null)
                child.gameObject.SetActive(true);
        }
    }
}