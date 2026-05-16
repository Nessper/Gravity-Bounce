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
/// - fade du dimmer global
/// - HUD on
/// - countdown
/// - callback final
///
/// Hold to skip :
/// - utilise un overlay partage
/// - ce controller ne fait jamais de ForceHideImmediate()
/// - il fait seulement Show(this, ...) et Hide(this)
///
/// IMPORTANT :
/// - le noir plein ecran n appartient plus a ce controller
/// - il passe par MainOverlaysController et son dimmer global
/// - l intro reprend le dimmer laisse par le briefing
/// </summary>
public class LevelIntroSequenceController : MonoBehaviour
{
    [Header("Core refs")]
    [SerializeField] private LevelControlsController controlsController;
    [SerializeField] private CountdownUI countdownUI;

    [Header("Main Overlays")]
    [SerializeField] private MainUIController mainUIController;

    [Tooltip("Identifiant de niveau injecte par LevelManager.")]
    [SerializeField] private string levelId = "W1-L1";


    [Header("Visual Intro")]
    [SerializeField] private GameObject boardRoot;

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

    private LocalizationManager Loc => LocalizationManager.Instance;

    /// <summary>
    /// Permet au LevelManager d injecter le levelId courant.
    /// </summary>
    public void ConfigureLevelId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
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

        StopIntro();

        onCompleteCallback = onComplete;
        skipRequested = false;

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

        mainUIController?.StopAndHideDialog();

        if (countdownUI != null)
            countdownUI.Hide();

        mainUIController?.HideHoldToSkip(this);
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
        mainUIController?.HideHoldToSkip(this);
    }

    /// <summary>
    /// Prepare l etat visuel initial de l intro.
    /// IMPORTANT :
    /// - on ne reset plus brutalement l alpha du dimmer
    /// - on reprend simplement le dimmer laisse par le briefing
    /// </summary>
    private void SetupInitialVisualState()
    {
        if (mainUIController != null)
        {
            // Le briefing a deja remonté le dimmer a 1.
            // L intro prend le relais ici :
            // - background coupe
            // - dimmer pose a 0.9
            mainUIController.SetBackgroundImmediate(0f, false, false);
            mainUIController.SetDimmerImmediate(0.9f, true, true);
        }

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

        if (mainUIController != null)
            mainUIController.ShowHoldToSkip(this, OnSkipButtonPressed);

        bool shipDone = !shipIntroEnabled;
        bool dialogsDone = false;
        bool boardDone = boardIntroAssembler == null;

        if (shipIntroEnabled)
            StartCoroutine(PlayShipEntranceSequence(() => shipDone = true));

        DialogLine[] introLines = null;
        yield return StartCoroutine(WaitForLocalizationReady());

        if (!skipRequested)
            introLines = TryResolveIntroLines();

        if (!skipRequested && mainUIController != null && introLines != null && introLines.Length > 0)
        {
            mainUIController.PlayDialogSequence(
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

        mainUIController?.HideHoldToSkip(this);

        if (!skipRequested)
            yield return StartCoroutine(FadeGlobalDimmerOnly());

        while (!boardDone && !skipRequested)
            yield return null;

        if (skipRequested)
            yield break;

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        ActivateAllBoardRootChildren();

        if (controlsController != null)
            controlsController.ShowMobileControlsUI(true);

        if (unduckBeforeCountdown && AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolumeMultiplier(1f, introMusicUnduckFadeSec);

        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        CursorController.Lock();

        if (countdownUI != null)
        {
            bool countdownDone = false;
            countdownUI.PlayStartCountdown(() => countdownDone = true);

            while (!countdownDone)
                yield return null;
        }

        mainUIController?.HideHoldToSkip(this);

        onCompleteCallback?.Invoke();
    }

    /// <summary>
    /// Attend que le LocalizationManager soit pret.
    /// </summary>
    private IEnumerator WaitForLocalizationReady()
    {
        if (Loc == null)
        {
            Debug.LogError("[LevelIntroSequenceController] LocalizationManager.Instance est null.");
            yield break;
        }

        while (!Loc.IsReady && !skipRequested)
            yield return null;
    }

    /// <summary>
    /// Resout les lignes d intro pour le level courant.
    /// Retourne null si aucune sequence valable n est disponible.
    /// </summary>
    private DialogLine[] TryResolveIntroLines()
    {
        if (Loc == null)
        {
            Debug.LogError("[LevelIntroSequenceController] LocalizationManager.Instance est null.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(levelId))
        {
            Debug.LogError("[LevelIntroSequenceController] levelId vide.");
            return null;
        }

        DialogSequence sequence = Loc.GetIntroSequence(levelId);
        if (sequence == null)
            return null;

        DialogLine[] lines = Loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            return null;

        return lines;
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

        if (mainUIController != null)
        {
            mainUIController.SetDimmerImmediate(
                0f,
                interactable: false,
                blocksRaycasts: false
            );
        }

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (controlsController != null)
            controlsController.ShowMobileControlsUI(true);

        mainUIController?.HideHoldToSkip(this);
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

        CursorController.Lock();

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
    /// Fait disparaitre le dimmer global de l intro.
    /// </summary>
    private IEnumerator FadeGlobalDimmerOnly()
    {
        if (mainUIController == null)
            yield break;

        bool fadeDone = false;

        mainUIController.FadeDimmerTo(
            this,
            0f,
            overlayFadeDuration,
            interactableAtEnd: false,
            blocksRaycastsAtEnd: false,
            onComplete: () => fadeDone = true
        );

        while (!fadeDone)
        {
            if (skipRequested)
                yield break;

            yield return null;
        }
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