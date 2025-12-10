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

    [Tooltip("Indices de monde / niveau utilises pour recuperer la sequence d intro dans le DialogManager.")]
    [SerializeField] private int worldIndex = 1;
    [SerializeField] private int levelIndex = 1;

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
    private bool shipIntroEnabled = false;

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

    private bool skipRequested = false;
    private Action onCompleteCallback;

    // ============================
    // ENTRY POINT
    // ============================

    /// <summary>
    /// Lance la sequence d intro complete.
    /// Le callback onComplete est appele une fois le compte a rebours termine.
    /// </summary>
    public void Play(Action onComplete)
    {
        onCompleteCallback = onComplete;
        skipRequested = false;

        // Etat visuel du bouton Skip au demarrage : cache et non interactif.
        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }

        SetupInitialVisualState();

        // Lancement du reveal du bouton Skip apres un delai.
        StartCoroutine(RevealSkipButtonAfterDelay());

        StartCoroutine(PlayRoutine());
    }

    // ============================
    // INITIAL SETUP
    // ============================

    /// <summary>
    /// Met en place l etat visuel initial de l intro
    /// (overlay, HUD, plateau, position du vaisseau, etc.).
    /// </summary>
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

        // Plateau actif (structure en place, contenu gere par l assembleur)
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

    /// <summary>
    /// Routine principale de l intro (vaisseau, dialogues, board, HUD, countdown).
    /// </summary>
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

        // 3) Attendre que le ship soit a sa position finale
        while (!shipDone)
            yield return null;

        // 3.5) Petit delai avant de lancer l assemblage du board
        if (delayBeforeBoardAssembly > 0f)
            yield return new WaitForSeconds(delayBeforeBoardAssembly);

        // 4) Lancer l assemblage du board en parallele, avec callback de fin
        if (boardIntroAssembler != null)
        {
            StartCoroutine(BoardAssemblyRoutine(() => boardDone = true));
        }

        // 5) Attendre la fin des dialogues
        while (!dialogsDone)
            yield return null;

        // 6) Dialogues termines : fade de l overlay (HUD encore off)
        yield return StartCoroutine(FadeIntroOverlayOnly());

        // 7) Attendre la fin de l assemblage du board
        while (!boardDone)
            yield return null;

        // 8) Board termine : HUD on + introHUD off + securite BoardRoot
        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        ActivateAllBoardRootChildren();

        // Afficher l UI mobile avant le compte a rebours
        if (controlsController != null)
        {
            controlsController.ShowMobileControlsUI(true);
        }

        // 9) Petit delai avant le countdown
        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        // 10) Countdown
        if (countdownUI != null)
            yield return StartCoroutine(countdownUI.PlayCountdown(null));

        // 11) Unlock et cleanup
        if (controlsController != null)
            controlsController.EnableGameplayControls();

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

    /// <summary>
    /// Appelle par le bouton Skip (via l inspector).
    /// Force la fin immediate de l intro et passe directement au countdown.
    /// </summary>
    public void OnSkipButtonPressed()
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

    /// <summary>
    /// Force tout l etat visuel comme si l intro etait terminee.
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
        }

        if (topHUDRoot != null)
            topHUDRoot.SetActive(true);

        if (introHUDRoot != null)
            introHUDRoot.SetActive(false);

        // Afficher aussi l UI mobile quand on skip
        if (controlsController != null)
        {
            controlsController.ShowMobileControlsUI(true);
        }

        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Sequence de skip: petit delai, puis countdown et unlock des controles.
    /// </summary>
    private IEnumerator SkipToCountdownRoutine()
    {
        if (delayBeforeCountdown > 0f)
            yield return new WaitForSeconds(delayBeforeCountdown);

        if (countdownUI != null)
            yield return StartCoroutine(countdownUI.PlayCountdown(null));

        if (controlsController != null)
            controlsController.EnableGameplayControls();

        onCompleteCallback?.Invoke();
    }

    // ============================
    // REVEAL DU BOUTON SKIP
    // ============================

    /// <summary>
    /// Attend un delai puis fait apparaitre le bouton Skip avec un petit fade.
    /// </summary>
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

    /// <summary>
    /// Anime le vaisseau depuis sa position de depart jusqu a sa position finale.
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
    /// Fait disparaitre progressivement l overlay d intro,
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

    /// <summary>
    /// Securite: s assure que tous les enfants de BoardRoot sont actifs.
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
