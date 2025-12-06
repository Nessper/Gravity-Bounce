using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la séquence d'intro de niveau :
/// - lock des controles
/// - état visuel initial (overlay + plateau masqué + HUD masqué + ship en bas)
/// - animation du vaisseau (en parallèle des dialogues)
/// - dialogues d'intro
/// - gros flash plein écran, pendant lequel le plateau est activé
/// - quand le flash disparaît, le plateau + HUD sont visibles
/// - petit délai
/// - compte à rebours "3-2-1"
/// - unlock des controles, puis callback onComplete (LevelManager.StartLevel)
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

    [Tooltip("Indices de monde / niveau utilisés pour récupérer la séquence d'intro dans le DialogManager.")]
    [SerializeField] private int worldIndex = 1;
    [SerializeField] private int levelIndex = 1;


    [Header("Visual Intro")]
    [Tooltip("Overlay noir (CanvasGroup) désactivé par défaut.")]
    [SerializeField] private CanvasGroup introOverlayCanvasGroup;

    [Tooltip("Racine du plateau (BoardRoot), désactivé au début.")]
    [SerializeField] private GameObject boardRoot;

    [Tooltip("Alpha de départ de l'overlay (ex: 0.9).")]
    [SerializeField] private float overlayInitialAlpha = 0.9f;

    [Header("Ship Intro (world space)")]
    [Tooltip("Transform du vaisseau de fond (SpriteRenderer en world space).")]
    [SerializeField] private Transform shipRoot;

    [Tooltip("Caméra utilisée pour le gameplay (orthographique). Si null, Camera.main.")]
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("Durée (secondes) de l'animation d'arrivée du vaisseau.")]
    [SerializeField] private float shipEnterDuration = 20f;

    [Tooltip("Marge mondiale sous le bas de la caméra pour la position 'extrême bas'.")]
    [SerializeField] private float shipOffscreenMarginWorld = 0.5f;

    [Tooltip("Fraction du chemin entre la position finale et l'extrême bas où démarre réellement le vaisseau (0 = déjà à sa place, 1 = tout en bas).")]
    [Range(0f, 1f)]
    [SerializeField] private float shipStartFromBottomFactor = 0.33f;

    private Vector3 shipStartWorldPosition;
    private Vector3 shipEndWorldPosition;
    private bool shipIntroEnabled = false;

    [Header("Board Reveal / Flash")]
    [Tooltip("Délai avant le flash une fois ship + dialogues terminés.")]
    [SerializeField] private float boardRevealDelay = 0.3f;

    [Tooltip("Durée de montée du flash (sec) de 0 à 1.")]
    [SerializeField] private float flashInDuration = 0.06f;

    [Tooltip("Durée de descente du flash (sec) de 1 à 0.")]
    [SerializeField] private float flashOutDuration = 0.25f;

    [Tooltip("CanvasGroup utilisé pour le flash plein écran (Image cyan).")]
    [SerializeField] private CanvasGroup boardFlashCanvasGroup;

    [Header("Gameplay HUD")]
    [Tooltip("HUD du haut (score run, barre de progression, pause, etc.).")]
    [SerializeField] private GameObject topHUDRoot;

    [Tooltip("HUD du bas (score local, timer, vies, etc.).")]
    [SerializeField] private GameObject bottomHUDRoot;

    [Header("Timing")]
    [Tooltip("Délai entre la fin du flash et le début du compte à rebours.")]
    [SerializeField] private float delayBeforeCountdown = 0.3f;

    [Header("Skip")]
    [Tooltip("Bouton Skip pour passer l'intro.")]
    [SerializeField] private Button skipButton;

    [Tooltip("Délai avant d'afficher le bouton Skip.")]
    [SerializeField] private float skipAppearDelay = 5f;

    [Tooltip("CanvasGroup du bouton Skip (pour alpha + interact).")]
    [SerializeField] private CanvasGroup skipButtonCanvasGroup;

    private bool skipRequested = false;
    private Action onCompleteCallback;



    // ============================
    // ENTRY POINT
    // ============================
    public void Play(Action onComplete)
    {
        onCompleteCallback = onComplete;
        skipRequested = false;

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipButtonPressed);
            skipButton.onClick.AddListener(OnSkipButtonPressed);
        }

        // Etat visuel du bouton Skip au démarrage : caché et non interactif
        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }

        SetupInitialVisualState();

        // Lancement du reveal du bouton Skip après un délai
        StartCoroutine(RevealSkipButtonAfterDelay());

        StartCoroutine(PlayRoutine());
    }



    // ============================
    // INITIAL SETUP
    // ============================
    /// <summary>
    /// Prépare l'état visuel initial :
    /// - overlay visible
    /// - plateau désactivé
    /// - HUD masqué
    /// - flash alpha = 0
    /// - ship placé en bas de l'écran selon la caméra ortho
    /// </summary>
    private void SetupInitialVisualState()
    {
        // Overlay
        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.gameObject.SetActive(true);
            introOverlayCanvasGroup.alpha = overlayInitialAlpha;
            introOverlayCanvasGroup.blocksRaycasts = true;   // bloque le gameplay derrière
            introOverlayCanvasGroup.interactable = true;     // permet au bouton Skip et aux UI enfants de rester interactifs
        }

        // HUD du bouton skip activé
        if (introHUDRoot != null)
            introHUDRoot.SetActive(true);

        // Plateau masqué
        if (boardRoot != null)
            boardRoot.SetActive(false);

        // HUD masqué
        if (topHUDRoot != null)
            topHUDRoot.SetActive(false);
        if (bottomHUDRoot != null)
            bottomHUDRoot.SetActive(false);

        // Flash plateau initialement invisible
        if (boardFlashCanvasGroup != null)
            boardFlashCanvasGroup.alpha = 0f;

        // Placement du vaisseau (caméra ortho + SpriteRenderer)
        shipIntroEnabled = false;

        if (shipRoot != null)
        {
            Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (cam != null && cam.orthographic)
            {
                SpriteRenderer sr = shipRoot.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    shipIntroEnabled = true;

                    // Position finale telle que dans la scène
                    shipEndWorldPosition = shipRoot.position;

                    // Bas de la caméra en monde
                    float camBottomY = cam.transform.position.y - cam.orthographicSize;

                    // Demi-hauteur du sprite
                    float halfHeight = sr.bounds.extents.y;

                    // Position "extrême bas" : haut du vaisseau collé au bas de l'écran - marge
                    float targetTopY = camBottomY - shipOffscreenMarginWorld;
                    float extremeStartY = targetTopY - halfHeight;

                    Vector3 extremeStartPos = new Vector3(
                        shipEndWorldPosition.x,
                        extremeStartY,
                        shipEndWorldPosition.z
                    );

                    // Position réelle de départ : interpolation entre end et extreme bas
                    shipStartWorldPosition = Vector3.Lerp(
                        shipEndWorldPosition,
                        extremeStartPos,
                        Mathf.Clamp01(shipStartFromBottomFactor)
                    );

                    shipRoot.position = shipStartWorldPosition;
                }
                else
                {
                    shipEndWorldPosition = shipRoot.position;
                    shipStartWorldPosition = shipEndWorldPosition;
                }
            }
            else
            {
                shipEndWorldPosition = shipRoot.position;
                shipStartWorldPosition = shipEndWorldPosition;
            }
        }
    }



    // ============================
    // MAIN ROUTINE (CAS NORMAL)
    // ============================
    private IEnumerator PlayRoutine()
    {
        // 1) Lock des controles
        if (controlsController != null)
            controlsController.DisableGameplayControls();

        // 2) Ship + dialogues en parallèle
        bool shipDone = !shipIntroEnabled;
        bool dialogsDone = false;

        if (shipIntroEnabled)
            StartCoroutine(PlayShipEntranceSequence(() => shipDone = true));

        // Récupération de la séquence d'intro via le DialogManager
        DialogLine[] introLines = null;
        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager != null)
        {
            DialogSequence sequence = dialogManager.GetIntroSequence(worldIndex, levelIndex);
            if (sequence != null)
                introLines = dialogManager.GetRandomVariantLines(sequence);
        }

        // Lancement des dialogues si possible
        if (dialogSequenceRunner != null && introLines != null && introLines.Length > 0)
        {
            dialogSequenceRunner.Play(introLines, () => dialogsDone = true);
        }
        else
        {
            // Pas de runner ou pas de lignes : on considère les dialogues comme terminés
            dialogsDone = true;
        }

        // 3) Attendre la fin des deux
        while (!shipDone || !dialogsDone)
        {
            yield return null;
        }

        // 4) Gros flash + reveal plateau + HUD
        yield return StartCoroutine(PlayBoardFlashReveal());

        // 4.5) Petit délai avant le countdown
        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        // 5) Countdown
        if (countdownUI != null)
            yield return StartCoroutine(countdownUI.PlayCountdown(null));

        // 6) Unlock et cleanup
        if (controlsController != null)
            controlsController.EnableGameplayControls();

        if (skipButton != null)
            skipButton.onClick.RemoveListener(OnSkipButtonPressed);

        onCompleteCallback?.Invoke();
    }



    // ============================
    // SKIP
    // ============================
    /// <summary>
    /// Appelé par le bouton Skip (via listener dans Play).
    /// Stoppe l'intro et force l'état juste avant le countdown.
    /// </summary>
    private void OnSkipButtonPressed()
    {
        if (skipRequested)
            return;

        skipRequested = true;
        Debug.Log("[LevelIntroSequenceController] Skip pressed");

        // Couper toutes les coroutines d'intro en cours
        StopAllCoroutines();

        // Couper aussi la séquence de dialogue si elle est en cours
        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        // Etat visuel comme si l'intro venait de se terminer
        ForceIntroSkippedState();

        // Enchaîner sur délai + countdown + unlock
        StartCoroutine(SkipToCountdownRoutine());
    }

    /// <summary>
    /// Force l'état "juste avant le countdown" :
    /// ship en place, plateau actif, overlay/flash off, HUD visible, dialog UI cachée, skip caché.
    /// </summary>
    private void ForceIntroSkippedState()
    {
        // Ship à sa position finale
        if (shipIntroEnabled && shipRoot != null)
            shipRoot.position = shipEndWorldPosition;

        // Plateau visible
        if (boardRoot != null)
            boardRoot.SetActive(true);

        // Overlay + flash à 0
        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.alpha = 0f;
            introOverlayCanvasGroup.blocksRaycasts = false;
        }

        if (boardFlashCanvasGroup != null)
            boardFlashCanvasGroup.alpha = 0f;

        // HUD visibles
        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);
        if (bottomHUDRoot != null)
            bottomHUDRoot.SetActive(true);
        // Masquer HUD d'intro
        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);


        // Stopper et cacher la séquence de dialogue
        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();


        // Cacher le bouton Skip
        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Routine appelée après un skip :
    /// - éventuel petit délai
    /// - countdown
    /// - unlock + onComplete
    /// </summary>
    private IEnumerator SkipToCountdownRoutine()
    {
        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        if (countdownUI != null)
            yield return StartCoroutine(countdownUI.PlayCountdown(null));

        if (controlsController != null)
            controlsController.EnableGameplayControls();

        if (skipButton != null)
            skipButton.onClick.RemoveListener(OnSkipButtonPressed);

        onCompleteCallback?.Invoke();
    }



    // ============================
    // REVEAL DU BOUTON SKIP
    // ============================
    private IEnumerator RevealSkipButtonAfterDelay()
    {
        if (skipAppearDelay > 0f)
            yield return new WaitForSeconds(skipAppearDelay);

        if (skipButtonCanvasGroup == null)
            yield break;

        // Fade-in du bouton
        float dur = 0.3f;
        float t = 0f;

        skipButtonCanvasGroup.alpha = 0f;
        skipButtonCanvasGroup.interactable = false;
        skipButtonCanvasGroup.blocksRaycasts = false;

        while (t < dur)
        {
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
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease-out cubic : rapide au début, ralentit sur la fin
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            shipRoot.position = Vector3.Lerp(shipStartWorldPosition, shipEndWorldPosition, easedT);

            yield return null;
        }

        shipRoot.position = shipEndWorldPosition;
        onComplete?.Invoke();
    }



    // ============================
    // BOARD FLASH + REVEAL
    // ============================
    private IEnumerator PlayBoardFlashReveal()
    {
        if (boardRevealDelay > 0f)
            yield return new WaitForSeconds(boardRevealDelay);

        float totalFlashDuration = Mathf.Max(0.01f, flashInDuration + flashOutDuration);
        float elapsed = 0f;

        float startOverlayAlpha = introOverlayCanvasGroup != null ? introOverlayCanvasGroup.alpha : 0f;
        float endOverlayAlpha = 0f;

        if (boardFlashCanvasGroup != null)
            boardFlashCanvasGroup.alpha = 0f;

        if (boardRoot != null)
            boardRoot.SetActive(true);

        while (elapsed < totalFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalFlashDuration);

            // Flash plateau (0 -> 1 puis 1 -> 0)
            if (boardFlashCanvasGroup != null)
            {
                float flashAlpha;

                if (elapsed <= flashInDuration)
                {
                    float k = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, flashInDuration));
                    flashAlpha = Mathf.Lerp(0f, 1f, k);
                }
                else
                {
                    float outTime = elapsed - flashInDuration;
                    float k = Mathf.Clamp01(outTime / Mathf.Max(0.0001f, flashOutDuration));
                    flashAlpha = Mathf.Lerp(1f, 0f, k);
                }

                boardFlashCanvasGroup.alpha = flashAlpha;
            }

            // Overlay noir qui fade vers 0 pendant le flash
            if (introOverlayCanvasGroup != null)
            {
                float overlayAlpha = Mathf.Lerp(startOverlayAlpha, endOverlayAlpha, t);
                introOverlayCanvasGroup.alpha = overlayAlpha;
            }

            yield return null;
        }

        if (boardFlashCanvasGroup != null)
            boardFlashCanvasGroup.alpha = 0f;

        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.alpha = 0f;
            introOverlayCanvasGroup.blocksRaycasts = false;
        }

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);
        if (bottomHUDRoot != null)
            bottomHUDRoot.SetActive(true);
        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

    }
}
