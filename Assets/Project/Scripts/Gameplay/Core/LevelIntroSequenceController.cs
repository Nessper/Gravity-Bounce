using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gere la sequence d intro de niveau :
/// - lock des controles
/// - etat visuel initial (overlay + plateau actif mais demonte + HUD masque + ship en bas)
/// - animation du vaisseau (en parallele des dialogues)
/// - dialogues d intro
/// - animation d assemblage du plateau via BoardIntroAssembler
/// - fade de l overlay a la fin des dialogues
/// - attente de la fin de l assemblage du board
/// - HUD on, petit delai
/// - compte a rebours "3-2-1"
/// - callback onComplete (LevelManager.StartLevel)
///
/// Musique (NEW) :
/// - Pendant l intro : MainGameplay joue mais en sourdine (ducking).
/// - Quand l intro est terminee (ou skip) : retour au volume normal avant le countdown.
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

    [Tooltip("Identifiant de niveau (ex: 'W1-L2') injecte par LevelManager. Utilise pour resoudre les dialogues.")]
    [SerializeField] private string levelId = "W1-L1";

    [Header("Visual Intro")]
    [Tooltip("Overlay noir (CanvasGroup) active pendant l intro.")]
    [SerializeField] private CanvasGroup introOverlayCanvasGroup;

    [Tooltip("Racine du plateau (BoardRoot), active des le debut de l intro (porte aussi BoardIntroAssembler).")]
    [SerializeField] private GameObject boardRoot;

    [Tooltip("Alpha de depart de l overlay (ex: 0.9).")]
    [SerializeField] private float overlayInitialAlpha = 0.9f;

    [Header("Ship Intro (world space)")]
    [Tooltip("Transform du vaisseau de fond (SpriteRenderer en world space).")]
    [SerializeField] private Transform shipRoot;

    [Tooltip("Camera utilisee pour le gameplay (orthographique). Si null, Camera.main.")]
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("Duree (secondes) de l animation d arrivee du vaisseau.")]
    [SerializeField] private float shipEnterDuration = 2f;

    [Tooltip("Marge mondiale sous le bas de la camera pour la position extreme bas.")]
    [SerializeField] private float shipOffscreenMarginWorld = 0.5f;

    [Tooltip("Fraction du chemin entre la position finale et l extreme bas ou demarre vraiment le vaisseau (0 = deja a sa place, 1 = tout en bas).")]
    [Range(0f, 1f)]
    [SerializeField] private float shipStartFromBottomFactor = 0.33f;

    private Vector3 shipStartWorldPosition;
    private Vector3 shipEndWorldPosition;
    private bool shipIntroEnabled;

    [Header("Board Intro")]
    [Tooltip("Script charge de preparer et d animer le montage du plateau (bins, murs, fond...).")]
    [SerializeField] private BoardIntroAssembler boardIntroAssembler;

    [Tooltip("Delai apres la fin de l animation du vaisseau avant de lancer l assemblage du plateau.")]
    [SerializeField] private float delayBeforeBoardAssembly = 0.3f;

    [Header("Gameplay HUD")]
    [Tooltip("HUD du haut (score run, barre de progression, pause, etc.).")]
    [SerializeField] private GameObject topHUDRoot;

    [Header("Timing")]
    [Tooltip("Delai entre la fin du HUD on et le debut du compte a rebours.")]
    [SerializeField] private float delayBeforeCountdown = 0.3f;

    [Header("Overlay Fade")]
    [Tooltip("Duree du fade-out de l overlay d intro (alpha -> 0) une fois les dialogues termines.")]
    [SerializeField] private float overlayFadeDuration = 0.3f;

    [Header("Skip")]
    [Tooltip("Bouton Skip pour passer l intro (cable dans l inspector vers OnSkipButtonPressed).")]
    [SerializeField] private Button skipButton;

    [Tooltip("Delai avant d afficher le bouton Skip.")]
    [SerializeField] private float skipAppearDelay = 5f;

    [Tooltip("CanvasGroup du bouton Skip (pour alpha + interact).")]
    [SerializeField] private CanvasGroup skipButtonCanvasGroup;

    [Header("Music")]
    [Tooltip("Si true, la musique MainGameplay demarre au debut de l intro.")]
    [SerializeField] private bool playGameplayMusicDuringIntro = true;

    [Tooltip("Multiplicateur de volume musique pendant l intro (sourdine).")]
    [Range(0f, 1f)]
    [SerializeField] private float introMusicVolumeMult = 0.25f;

    [Tooltip("Fade pour passer en sourdine au debut intro.")]
    [SerializeField] private float introMusicDuckFadeSec = 0.3f;

    [Tooltip("Fade pour remonter au volume normal avant le gameplay.")]
    [SerializeField] private float introMusicUnduckFadeSec = 0.5f;

    [Tooltip("Si true, on remonte le volume musique juste avant le countdown.")]
    [SerializeField] private bool unduckBeforeCountdown = true;

    [Tooltip("Fades utilises si la musique change (ex: MainBriefing -> MainGameplay).")]
    [SerializeField] private float gameplayMusicFadeOutSec = 0.8f;

    [SerializeField] private float gameplayMusicFadeInSec = 0.8f;

    private bool skipRequested;
    private Action onCompleteCallback;
    private Coroutine playRoutine;
    private Coroutine skipRevealRoutine;

    private bool debugSkip;

    // ============================
    // PUBLIC API
    // ============================

    public void ConfigureLevelId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        levelId = id;
    }

    public void Play(Action onComplete)
    {
        if (debugSkip)
        {
            onComplete?.Invoke();
            return;
        }

        onCompleteCallback = onComplete;
        skipRequested = false;

        StopIntro(); // securite (restart / retry / hot reload)

        SetupInitialVisualState();

        // ------------------------------------------------------------
        // MUSIQUE : demarre MainGameplay mais en sourdine tant que l intro n est pas finie.
        // ------------------------------------------------------------
        if (playGameplayMusicDuringIntro)
        {
            LevelMusicDirector musicDirector = FindFirstObjectByType<LevelMusicDirector>();

            if (musicDirector != null)
                musicDirector.PlayGameplayMusic();

            AudioManager.Instance?.SetMusicVolumeMultiplier(introMusicVolumeMult, introMusicDuckFadeSec);
        }

        // Etat visuel du bouton Skip au demarrage
        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }

        // Lancement du reveal du bouton Skip apres un delai
        if (gameObject.activeInHierarchy)
            skipRevealRoutine = StartCoroutine(RevealSkipButtonAfterDelay());

        if (gameObject.activeInHierarchy)
            playRoutine = StartCoroutine(PlayRoutine());
    }

    public void StopIntro()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (skipRevealRoutine != null)
        {
            StopCoroutine(skipRevealRoutine);
            skipRevealRoutine = null;
        }

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (countdownUI != null)
            countdownUI.Hide();
    }

    public void SetDebugSkip(bool value)
    {
        debugSkip = value;
    }

    // ============================
    // INITIAL SETUP
    // ============================

    private void SetupInitialVisualState()
    {
        // Overlay
        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.gameObject.SetActive(true);
            introOverlayCanvasGroup.alpha = overlayInitialAlpha;
            introOverlayCanvasGroup.blocksRaycasts = true;
            introOverlayCanvasGroup.interactable = true;
        }

        // HUD intro visible (skip, etc.)
        if (introHUDRoot != null)
            introHUDRoot.SetActive(true);

        // Plateau actif
        if (boardRoot != null)
            boardRoot.SetActive(true);

        // HUD gameplay masque
        if (topHUDRoot != null)
            topHUDRoot.SetActive(false);

        // Preparation du plateau demonte
        if (boardIntroAssembler != null)
            boardIntroAssembler.PrepareInitialState();

        // Placement du vaisseau
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

    // ============================
    // MAIN ROUTINE
    // ============================

    private IEnumerator PlayRoutine()
    {
        // 1) Lock des controles
        if (controlsController != null)
            controlsController.DisableGameplayControls();

        // 2) Ship + dialogues en parallele
        bool shipDone = !shipIntroEnabled;
        bool dialogsDone = false;
        bool boardDone = (boardIntroAssembler == null);

        if (shipIntroEnabled)
            StartCoroutine(PlayShipEntranceSequence(() => shipDone = true));

        // Recuperation des lignes d intro via sequenceId (source de verite = levelId)
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
            dialogSequenceRunner.Play(introLines, () => dialogsDone = true);
        else
            dialogsDone = true;

        // 3) Attendre que le ship soit a sa position finale
        while (!shipDone && !skipRequested)
            yield return null;

        // 3.5) Petit delai avant de lancer l assemblage du board
        if (!skipRequested && delayBeforeBoardAssembly > 0f)
            yield return new WaitForSeconds(delayBeforeBoardAssembly);

        // 4) Lancer l assemblage du board
        if (!skipRequested && boardIntroAssembler != null)
            StartCoroutine(BoardAssemblyRoutine(() => boardDone = true));

        // 5) Attendre la fin des dialogues
        while (!dialogsDone && !skipRequested)
            yield return null;

        // 6) Fade overlay apres dialogues
        if (!skipRequested)
            yield return StartCoroutine(FadeIntroOverlayOnly());

        // 7) Attendre la fin du board
        while (!boardDone && !skipRequested)
            yield return null;

        if (skipRequested)
            yield break;

        // 8) HUD on + introHUD off + securite BoardRoot
        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        ActivateAllBoardRootChildren();

        // UI mobile on avant countdown
        if (controlsController != null)
            controlsController.ShowMobileControlsUI(true);

        // ------------------------------------------------------------
        // MUSIQUE : retour volume normal avant countdown (energie).
        // ------------------------------------------------------------
        if (unduckBeforeCountdown && AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolumeMultiplier(1f, introMusicUnduckFadeSec);

        // 9) Delai avant countdown
        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        // 10) Countdown
        if (countdownUI != null)
        {
            bool countdownDone = false;
            countdownUI.PlayStartCountdown(() => countdownDone = true);
            while (!countdownDone)
                yield return null;
        }

        // 11) Fin
        onCompleteCallback?.Invoke();
    }

    private IEnumerator BoardAssemblyRoutine(Action onComplete)
    {
        yield return boardIntroAssembler.PlayAssembly();
        onComplete?.Invoke();
    }

    // ============================
    // DIALOG IDS
    // ============================

    private string BuildIntroSequenceId()
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        // Exemple : "W1-L2" -> "W1_L2_intro"
        string normalized = levelId.Replace("-", "_");
        return normalized + "_intro";
    }

    // ============================
    // SKIP
    // ============================

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
        }

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        if (controlsController != null)
            controlsController.ShowMobileControlsUI(true);

        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator SkipToCountdownRoutine()
    {
        // ------------------------------------------------------------
        // MUSIQUE : sur skip, retour volume normal avant countdown.
        // ------------------------------------------------------------
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

    // ============================
    // SKIP BUTTON REVEAL
    // ============================

    private IEnumerator RevealSkipButtonAfterDelay()
    {
        if (skipAppearDelay > 0f)
            yield return new WaitForSeconds(skipAppearDelay);

        if (skipRequested || skipButtonCanvasGroup == null)
            yield break;

        float dur = 0.3f;
        float t = 0f;

        skipButtonCanvasGroup.alpha = 0f;
        skipButtonCanvasGroup.interactable = false;
        skipButtonCanvasGroup.blocksRaycasts = false;

        while (t < dur)
        {
            if (skipRequested)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            skipButtonCanvasGroup.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }

        skipButtonCanvasGroup.alpha = 1f;
        skipButtonCanvasGroup.interactable = true;
        skipButtonCanvasGroup.blocksRaycasts = true;
    }

    // ============================
    // SHIP MOVEMENT
    // ============================

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

    // ============================
    // OVERLAY FADE
    // ============================

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
    }

    // ============================
    // BOARDROOT SAFETY
    // ============================

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