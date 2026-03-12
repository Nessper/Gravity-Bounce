using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gere la transition "Next" apres la fin de niveau :
/// - fermeture du panneau final
/// - dialogue d outro de niveau (optionnel)
/// - bouton Skip pendant le dialogue
/// - depart du vaisseau vers le haut
/// - callback final (ex: navigation vers RunHub / Credits / etc.)
///
/// Philosophie du skip :
/// - on ne saute PAS toute la transition
/// - on coupe seulement la phase dialogue
/// - on continue ensuite normalement vers l animation du vaisseau
/// </summary>
public class NextTransitionController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private GameObject endLevelRoot;
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Outro HUD")]
    [Tooltip("HUD du haut dedie a l outro (contient notamment le bouton Skip).")]
    [SerializeField] private GameObject outroHUDRoot;

    [Header("Skip")]
    [Tooltip("Bouton Skip de l outro.")]
    [SerializeField] private Button skipButton;

    [Tooltip("CanvasGroup du bouton Skip pour gerer alpha / interact / raycasts.")]
    [SerializeField] private CanvasGroup skipButtonCanvasGroup;

    [Tooltip("Delai avant l apparition du bouton Skip pendant le dialogue.")]
    [SerializeField] private float skipAppearDelay = 1.5f;

    [Tooltip("Duree du fade-in du bouton Skip.")]
    [SerializeField] private float skipFadeDuration = 0.25f;

    [Header("Outro Dialog")]
    [Tooltip("Suffixe du dialogue d outro. Exemple : W1_L1_outro.")]
    [SerializeField] private string outroSuffix = "_outro";

    [Header("Timing (unscaled)")]
    [SerializeField] private float pauseAfterHide = 0.25f;
    [SerializeField] private float pauseAfterDialog = 0.20f;
    [SerializeField] private float pauseAfterShip = 0.15f;

    [Header("Ship Outro (optional)")]
    [Tooltip("Transform du vaisseau (world space). Si null, l animation ship est ignoree.")]
    [SerializeField] private Transform shipRoot;

    [Tooltip("Camera orthographique de gameplay. Si null, Camera.main.")]
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("SpriteRenderer du vaisseau. Si null, GetComponentInChildren<SpriteRenderer>().")]
    [SerializeField] private SpriteRenderer shipSpriteRenderer;

    [Tooltip("Duree du depart du vaisseau (unscaled).")]
    [SerializeField] private float shipDepartDuration = 0.55f;

    [Tooltip("Marge world au-dessus du haut de la camera pour etre certain d etre hors champ.")]
    [SerializeField] private float shipOffscreenMarginWorld = 0.6f;

    [Tooltip("Overshoot (world) pour donner du punch (0 = aucun).")]
    [SerializeField] private float shipOvershootWorld = 0.8f;

    [Header("Flash (optional)")]
    [SerializeField] private CanvasGroup flashCanvasGroup;
    [SerializeField] private float flashPeakAlpha = 0.85f;
    [SerializeField] private float flashDuration = 0.12f;

    private bool isRunning;
    private bool skipRequested;

    private Coroutine playRoutine;
    private Coroutine skipRevealRoutine;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        // Securite : on part avec le bouton Skip cache.
        HideSkipButtonImmediate();

        if (outroHUDRoot != null)
            outroHUDRoot.SetActive(false);
    }

    /// <summary>
    /// Lance la transition d outro.
    /// </summary>
    public void PlayOutroAndFinish(Action onComplete)
    {
        if (isRunning)
            return;

        StopControllerRoutinesOnly();

        skipRequested = false;
        isRunning = false;

        HideSkipButtonImmediate();

        if (outroHUDRoot != null)
            outroHUDRoot.SetActive(false);

        if (gameObject.activeInHierarchy)
            playRoutine = StartCoroutine(Routine(onComplete));
    }

    /// <summary>
    /// Callback du bouton Skip.
    /// Coupe uniquement la phase dialogue, puis laisse la routine principale
    /// continuer normalement vers le depart du vaisseau.
    /// </summary>
    public void OnSkipButtonPressed()
    {
        if (!isRunning || skipRequested)
            return;

        skipRequested = true;

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();

        HideSkipButtonImmediate();
    }

    /// <summary>
    /// Routine principale de la transition Next.
    /// </summary>
    private IEnumerator Routine(Action onComplete)
    {
        isRunning = true;

        // 1) Ferme le root de fin de niveau.
        if (endLevelRoot != null && endLevelRoot.activeSelf)
            endLevelRoot.SetActive(false);

        if (pauseAfterHide > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterHide);

        // 2) Active le HUD outro (skip, etc.).
        if (outroHUDRoot != null)
            outroHUDRoot.SetActive(true);

        // 3) Lance l apparition differee du bouton Skip.
        if (gameObject.activeInHierarchy)
            skipRevealRoutine = StartCoroutine(RevealSkipButtonAfterDelay());

        // 4) Joue le dialogue outro si present.
        yield return StartCoroutine(PlayOutroIfAny());

        // Le dialogue est fini ou skippe -> le bouton Skip n a plus lieu d etre.
        HideSkipButtonImmediate();

        if (outroHUDRoot != null)
            outroHUDRoot.SetActive(false);

        // Si le joueur a skippe, on part directement sur l anim du vaisseau
        // sans attendre la pause de respiration apres dialogue.
        if (!skipRequested && pauseAfterDialog > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterDialog);

        // 5) Flash + depart du vaisseau.
        StartCoroutine(PlayFlashIfAny());
        yield return StartCoroutine(PlayShipDepartUpAndOffscreenIfAny());

        if (pauseAfterShip > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterShip);

        isRunning = false;
        playRoutine = null;

        onComplete?.Invoke();
    }

    /// <summary>
    /// Joue le dialogue outro du niveau si un ID existe dans le JSON.
    /// Le skip coupe proprement le dialogue via dialogSequenceRunner.StopAndHide().
    /// </summary>
    private IEnumerator PlayOutroIfAny()
    {
        if (dialogSequenceRunner == null)
            yield break;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            yield break;

        while (!dialogManager.IsReady)
        {
            if (skipRequested)
                yield break;

            yield return null;
        }

        // EndLevelUI expose CurrentLevelId (ex: "W1-L1")
        string levelId = (endLevelUI != null) ? endLevelUI.CurrentLevelId : null;
        if (string.IsNullOrEmpty(levelId))
            yield break;

        // Dialog JSON utilise des IDs underscore (ex: "W1_L1_outro")
        string normalizedLevelId = levelId.Replace("-", "_");
        string seqId = normalizedLevelId + outroSuffix;

        DialogSequence seq = dialogManager.GetSequenceById(seqId);
        if (seq == null)
            yield break;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;
        dialogSequenceRunner.Play(lines, () => done = true);

        while (!done)
        {
            if (skipRequested)
            {
                dialogSequenceRunner.StopAndHide();
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Fait sortir le vaisseau vers le haut, completement hors champ.
    /// </summary>
    private IEnumerator PlayShipDepartUpAndOffscreenIfAny()
    {
        if (shipRoot == null)
            yield break;

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (cam == null || !cam.orthographic)
            yield break;

        SpriteRenderer sr = shipSpriteRenderer != null ? shipSpriteRenderer : shipRoot.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
            yield break;

        float dur = Mathf.Max(0.01f, shipDepartDuration);

        Vector3 start = shipRoot.position;

        // Haut de l ecran camera en world.
        float camTopY = cam.transform.position.y + cam.orthographicSize;

        // Pour que le vaisseau soit completement sorti, son bas doit etre au-dessus du haut camera + marge.
        float halfHeight = sr.bounds.extents.y;
        float targetBottomY = camTopY + Mathf.Max(0f, shipOffscreenMarginWorld);
        float endY = targetBottomY + halfHeight;

        Vector3 end = new Vector3(start.x, endY, start.z);

        // Overshoot optionnel pour donner du punch.
        float overshoot = Mathf.Max(0f, shipOvershootWorld);
        Vector3 overshootPos = new Vector3(start.x, endY + overshoot, start.z);

        // Anim en 2 phases :
        // 1) impulsion vers le haut
        // 2) retour doux vers la position finale hors ecran
        float phase1Ratio = (overshoot > 0f) ? 0.70f : 1.0f;
        float dur1 = dur * Mathf.Clamp01(phase1Ratio);
        float dur2 = dur - dur1;

        // Phase 1
        float t = 0f;
        while (t < dur1)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur1);

            // EaseInQuad
            float eased = k * k;

            shipRoot.position = Vector3.LerpUnclamped(start, overshoot > 0f ? overshootPos : end, eased);
            yield return null;
        }

        shipRoot.position = overshoot > 0f ? overshootPos : end;

        // Phase 2
        if (overshoot > 0f && dur2 > 0.001f)
        {
            float t2 = 0f;
            while (t2 < dur2)
            {
                t2 += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t2 / dur2);

                // EaseOutCubic
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                shipRoot.position = Vector3.LerpUnclamped(overshootPos, end, eased);
                yield return null;
            }
        }

        shipRoot.position = end;
    }

    /// <summary>
    /// Petit flash optionnel accompagne le depart du vaisseau.
    /// </summary>
    private IEnumerator PlayFlashIfAny()
    {
        if (flashCanvasGroup == null)
            yield break;

        float dur = Mathf.Max(0.01f, flashDuration);
        float half = dur * 0.5f;

        flashCanvasGroup.gameObject.SetActive(true);
        flashCanvasGroup.alpha = 0f;
        flashCanvasGroup.blocksRaycasts = false;

        // Fade in
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            flashCanvasGroup.alpha = Mathf.Lerp(0f, flashPeakAlpha, k);
            yield return null;
        }

        // Fade out
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            flashCanvasGroup.alpha = Mathf.Lerp(flashPeakAlpha, 0f, k);
            yield return null;
        }

        flashCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Reveal progressif du bouton Skip.
    /// Logique identique a celle de l intro.
    /// </summary>
    private IEnumerator RevealSkipButtonAfterDelay()
    {
        if (skipAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(skipAppearDelay);

        if (skipRequested || skipButtonCanvasGroup == null)
            yield break;

        float dur = Mathf.Max(0.01f, skipFadeDuration);
        float t = 0f;

        skipButtonCanvasGroup.alpha = 0f;
        skipButtonCanvasGroup.interactable = false;
        skipButtonCanvasGroup.blocksRaycasts = false;

        while (t < dur)
        {
            if (skipRequested)
                yield break;

            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            skipButtonCanvasGroup.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }

        skipButtonCanvasGroup.alpha = 1f;
        skipButtonCanvasGroup.interactable = true;
        skipButtonCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Cache immediatement le bouton Skip.
    /// </summary>
    private void HideSkipButtonImmediate()
    {
        if (skipButtonCanvasGroup != null)
        {
            skipButtonCanvasGroup.alpha = 0f;
            skipButtonCanvasGroup.interactable = false;
            skipButtonCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Stop uniquement les coroutines de ce controller.
    /// On ne veut pas utiliser StopAllCoroutines() au moment du skip,
    /// car la routine principale doit continuer vers l animation du vaisseau.
    /// </summary>
    private void StopControllerRoutinesOnly()
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

        HideSkipButtonImmediate();

        if (dialogSequenceRunner != null)
            dialogSequenceRunner.StopAndHide();
    }
}