using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la séquence d'intro de niveau :
/// - lock des contrôles
/// - état visuel initial (overlay + plateau actif mais démonté + HUD masqué + ship en bas)
/// - animation du vaisseau (en parallèle des dialogues)
/// - dialogues d'intro
/// - animation d'assemblage du plateau via BoardIntroAssembler
/// - fade de l'overlay à la fin des dialogues
/// - attente de la fin de l'assemblage du board
/// - HUD on, petit délai
/// - compte à rebours "3-2-1"
/// - unlock des contrôles, puis callback onComplete (LevelManager.StartLevel)
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

    [Tooltip("Racine du plateau (BoardRoot), active dès le début de l'intro (porte aussi BoardIntroAssembler).")]
    [SerializeField] private GameObject boardRoot;

    [Tooltip("Alpha de départ de l'overlay (ex: 0.9).")]
    [SerializeField] private float overlayInitialAlpha = 0.9f;

    [Header("Ship Intro (world space)")]
    [Tooltip("Transform du vaisseau de fond (SpriteRenderer en world space).")]
    [SerializeField] private Transform shipRoot;

    [Tooltip("Caméra utilisée pour le gameplay (orthographique). Si null, Camera.main.")]
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("Durée (secondes) de l'animation d'arrivée du vaisseau.")]
    [SerializeField] private float shipEnterDuration = 2f;

    [Tooltip("Marge mondiale sous le bas de la caméra pour la position 'extrême bas'.")]
    [SerializeField] private float shipOffscreenMarginWorld = 0.5f;

    [Tooltip("Fraction du chemin entre la position finale et l'extrême bas où démarre réellement le vaisseau (0 = déjà à sa place, 1 = tout en bas).")]
    [Range(0f, 1f)]
    [SerializeField] private float shipStartFromBottomFactor = 0.33f;

    private Vector3 shipStartWorldPosition;
    private Vector3 shipEndWorldPosition;
    private bool shipIntroEnabled = false;

    [Header("Board Intro")]
    [Tooltip("Script chargé de préparer et d'animer le montage du plateau (bins, murs, fond...).")]
    [SerializeField] private BoardIntroAssembler boardIntroAssembler;

    [Tooltip("Délai après la fin de l'animation du vaisseau avant de lancer l'assemblage du plateau.")]
    [SerializeField] private float delayBeforeBoardAssembly = 0.3f;

    [Header("Gameplay HUD")]
    [Tooltip("HUD du haut (score run, barre de progression, pause, etc.).")]
    [SerializeField] private GameObject topHUDRoot;

    [Header("Timing")]
    [Tooltip("Délai entre la fin du HUD on et le début du compte à rebours.")]
    [SerializeField] private float delayBeforeCountdown = 0.3f;

    [Header("Overlay Fade")]
    [Tooltip("Durée du fade-out de l'overlay d'intro (alpha -> 0) une fois les dialogues terminés.")]
    [SerializeField] private float overlayFadeDuration = 0.3f;

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

        // Plateau actif (structure en place, mais contenu géré par l'assembleur)
        if (boardRoot != null)
            boardRoot.SetActive(true);

        // HUD gameplay masqué
        if (topHUDRoot != null)
            topHUDRoot.SetActive(false);

        // Préparation du plateau démonté
        if (boardIntroAssembler != null)
            boardIntroAssembler.PrepareInitialState();

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

                    shipEndWorldPosition = shipRoot.position;

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
    // MAIN ROUTINE
    // ============================
    private IEnumerator PlayRoutine()
    {
        // 1) Lock des contrôles
        if (controlsController != null)
            controlsController.DisableGameplayControls();

        // 2) Ship + dialogues en parallèle
        bool shipDone = !shipIntroEnabled;
        bool dialogsDone = false;
        bool boardDone = (boardIntroAssembler == null);

        if (shipIntroEnabled)
            StartCoroutine(PlayShipEntranceSequence(() => shipDone = true));

        DialogLine[] introLines = null;
        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager != null)
        {
            DialogSequence sequence = dialogManager.GetIntroSequence(worldIndex, levelIndex);
            if (sequence != null)
                introLines = dialogManager.GetRandomVariantLines(sequence);
        }

        if (dialogSequenceRunner != null && introLines != null && introLines.Length > 0)
        {
            dialogSequenceRunner.Play(introLines, () => dialogsDone = true);
        }
        else
        {
            dialogsDone = true;
        }

        // 3) Attendre que le ship soit à sa position finale
        while (!shipDone)
            yield return null;

        // 3.5) Petit délai avant de lancer l'assemblage du board
        if (delayBeforeBoardAssembly > 0f)
            yield return new WaitForSeconds(delayBeforeBoardAssembly);

        // 4) Lancer l'assemblage du board EN PARALLÈLE, avec callback de fin
        if (boardIntroAssembler != null)
        {
            StartCoroutine(BoardAssemblyRoutine(() => boardDone = true));
        }

        // 5) Attendre la fin des dialogues
        while (!dialogsDone)
            yield return null;

        // 6) Dialogues terminés : fade de l'overlay (mais on ne touche pas encore au HUD)
        yield return StartCoroutine(FadeIntroOverlayOnly());

        // 7) Attendre la fin de l'assemblage du board
        while (!boardDone)
            yield return null;

        // 8) Board terminé : HUD on + introHUD off + sécurité BoardRoot
        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        ActivateAllBoardRootChildren();

        // 9) Petit délai avant le countdown
        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        // 10) Countdown
        if (countdownUI != null)
            yield return StartCoroutine(countdownUI.PlayCountdown(null));

        // 11) Unlock et cleanup
        if (controlsController != null)
            controlsController.EnableGameplayControls();

        if (skipButton != null)
            skipButton.onClick.RemoveListener(OnSkipButtonPressed);

        onCompleteCallback?.Invoke();
    }

    private IEnumerator BoardAssemblyRoutine(Action onComplete)
    {
        yield return boardIntroAssembler.PlayAssembly();
        onComplete?.Invoke();
    }

    // ============================
    // SKIP
    // ============================
    private void OnSkipButtonPressed()
    {
        if (skipRequested)
            return;

        skipRequested = true;
        Debug.Log("[LevelIntroSequenceController] Skip pressed");

        StopAllCoroutines();

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        ForceIntroSkippedState();

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

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }
    }

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

            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            shipRoot.position = Vector3.Lerp(shipStartWorldPosition, shipEndWorldPosition, easedT);

            yield return null;
        }

        shipRoot.position = shipEndWorldPosition;
        onComplete?.Invoke();
    }

    // ============================
    // OVERLAY FADE (SEULEMENT)
    // ============================
    /// <summary>
    /// Fait disparaître progressivement l'overlay d'intro,
    /// sans toucher au HUD.
    /// </summary>
    private IEnumerator FadeIntroOverlayOnly()
    {
        float duration = Mathf.Max(0.01f, overlayFadeDuration);
        float elapsed = 0f;

        float startAlpha = introOverlayCanvasGroup != null ? introOverlayCanvasGroup.alpha : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (introOverlayCanvasGroup != null)
            {
                float a = Mathf.Lerp(startAlpha, 0f, t);
                introOverlayCanvasGroup.alpha = a;
            }

            yield return null;
        }

        if (introOverlayCanvasGroup != null)
        {
            introOverlayCanvasGroup.alpha = 0f;
            introOverlayCanvasGroup.blocksRaycasts = false;
        }
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
