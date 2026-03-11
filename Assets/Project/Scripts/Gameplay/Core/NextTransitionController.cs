using System;
using System.Collections;
using UnityEngine;

public class NextTransitionController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private GameObject endLevelRoot;
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Header("Outro Dialog")]
    [SerializeField] private string outroSuffix = "_outro";

    [Header("Timing (unscaled)")]
    [SerializeField] private float pauseAfterHide = 0.25f;
    [SerializeField] private float pauseAfterDialog = 0.20f;
    [SerializeField] private float pauseAfterShip = 0.15f;

    [Header("Ship Outro (optional)")]
    [Tooltip("Transform du vaisseau (world space). Si null, l'anim ship est ignorée.")]
    [SerializeField] private Transform shipRoot;

    [Tooltip("Camera orthographique de gameplay. Si null, Camera.main.")]
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("SpriteRenderer du vaisseau (pour calculer la hauteur et sortir complètement). Si null, GetComponentInChildren<SpriteRenderer>().")]
    [SerializeField] private SpriteRenderer shipSpriteRenderer;

    [Tooltip("Durée du boost (unscaled).")]
    [SerializeField] private float shipDepartDuration = 0.55f;

    [Tooltip("Marge world au-dessus du haut de la caméra pour être certain d'être hors champ.")]
    [SerializeField] private float shipOffscreenMarginWorld = 0.6f;

    [Tooltip("Overshoot (world) pour donner du punch (0 = aucun).")]
    [SerializeField] private float shipOvershootWorld = 0.8f;

    [Header("Flash (optional)")]
    [SerializeField] private CanvasGroup flashCanvasGroup;

    [SerializeField] private float flashPeakAlpha = 0.85f;
    [SerializeField] private float flashDuration = 0.12f;


    private bool isRunning;

    public bool IsRunning => isRunning;

    public void PlayOutroAndFinish(Action onComplete)
    {
        if (isRunning)
            return;

        StopAllCoroutines();
        StartCoroutine(Routine(onComplete));
    }

    private IEnumerator Routine(Action onComplete)
    {
        isRunning = true;

        // 1) Hide overlay end
        if (endLevelRoot != null && endLevelRoot.activeSelf)
            endLevelRoot.SetActive(false);

        if (pauseAfterHide > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterHide);

        // 2) Outro dialog (optionnel)
        yield return StartCoroutine(PlayOutroIfAny());

        if (pauseAfterDialog > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterDialog);

        // 3) Ship depart (après dialogues) - sort complètement de l'écran vers le haut
        // 3) Ship depart (après dialogues)
        StartCoroutine(PlayFlashIfAny());
        yield return StartCoroutine(PlayShipDepartUpAndOffscreenIfAny());

        if (pauseAfterShip > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterShip);

        isRunning = false;
        onComplete?.Invoke();
    }

    private IEnumerator PlayOutroIfAny()
    {
        if (dialogSequenceRunner == null)
            yield break;

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            yield break;

        while (!dialogManager.IsReady)
            yield return null;

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
            yield return null;
    }

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

        // Top Y of camera view in world
        float camTopY = cam.transform.position.y + cam.orthographicSize;

        // Ensure ship fully out: move so ship bottom is above camTopY + margin
        float halfHeight = sr.bounds.extents.y;
        float targetBottomY = camTopY + Mathf.Max(0f, shipOffscreenMarginWorld);
        float endY = targetBottomY + halfHeight;

        Vector3 end = new Vector3(start.x, endY, start.z);

        // Overshoot for punch (upward)
        float overshoot = Mathf.Max(0f, shipOvershootWorld);
        Vector3 overshootPos = new Vector3(start.x, endY + overshoot, start.z);

        // Two-phase animation: kick to overshoot (ease-in), then settle back to end (ease-out)
        float phase1Ratio = (overshoot > 0f) ? 0.70f : 1.0f;
        float dur1 = dur * Mathf.Clamp01(phase1Ratio);
        float dur2 = dur - dur1;

        // Phase 1
        float t = 0f;
        while (t < dur1)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur1);

            // EaseInQuad (départ sec)
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

                // EaseOutCubic (settle)
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                shipRoot.position = Vector3.LerpUnclamped(overshootPos, end, eased);
                yield return null;
            }
        }

        shipRoot.position = end;
    }

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

}
