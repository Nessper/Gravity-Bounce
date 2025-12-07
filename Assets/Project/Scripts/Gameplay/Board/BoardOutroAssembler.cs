using System.Collections;
using UnityEngine;

/// <summary>
/// Gere l'animation de "rangement" du board a la fin du niveau.
/// Ordre:
/// 1) Disparition du player (scale -> 0 + fade out)
/// 2) Repli des murs + BoardVisual (pulse + shrink vertical depuis le bas)
/// 3) Slide des bins vers leurs X d'outro (gauche et droit differents)
/// 4) Fade out des bins
/// 5) Petit delai, puis on laisse la main au GameFlowController pour la suite
/// </summary>
public class BoardOutroAssembler : MonoBehaviour
{
    [Header("Board Root")]
    [Tooltip("Transform racine du plateau. Si null, utilise ce transform.")]
    [SerializeField] private Transform boardRoot;

    [Header("Walls Root")]
    [Tooltip("Parent commun des murs (ex: EnergyWalls).")]
    [SerializeField] private Transform wallsRoot;

    [Header("Board Visual")]
    [Tooltip("Quad visuel du plateau (fond, glass, etc.).")]
    [SerializeField] private Transform boardVisual;

    [Header("Walls visuals (quads avec EnergyWallFX)")]
    [Tooltip("Quad visuel du mur gauche (porte EnergyWallFX).")]
    [SerializeField] private Transform leftWallVisual;

    [Tooltip("Quad visuel du mur droit (porte EnergyWallFX).")]
    [SerializeField] private Transform rightWallVisual;

    [Header("Bins")]
    [Tooltip("Racine du bin gauche (Transform en world space).")]
    [SerializeField] private Transform leftBinRoot;

    [Tooltip("Racine du bin droit (Transform en world space).")]
    [SerializeField] private Transform rightBinRoot;

    [Header("Bins Outro X")]
    [Tooltip("Position X de sortie du bin gauche pendant le rangement.")]
    [SerializeField] private float leftBinOutroX;

    [Tooltip("Position X de sortie du bin droit pendant le rangement.")]
    [SerializeField] private float rightBinOutroX;

    [Header("Player")]
    [Tooltip("Racine visuelle du player (paddle).")]
    [SerializeField] private Transform playerRoot;

    [Tooltip("Duree de disparition du player (scale + fade).")]
    [SerializeField] private float playerDisappearDuration = 0.25f;

    [Header("Timings outro")]
    [Tooltip("Duree du repli des murs + BoardVisual.")]
    [SerializeField] private float wallsShrinkDuration = 0.25f;

    [Tooltip("Duree du slide de sortie des bins vers leur X d'outro.")]
    [SerializeField] private float binsOutroSlideDuration = 0.35f;

    [Tooltip("Duree du fade out des bins.")]
    [SerializeField] private float binsFadeOutDuration = 0.25f;

    [Tooltip("Delai final apres le fade des bins, avant de considerer l'outro terminee.")]
    [SerializeField] private float finalDelay = 0.1f;

    // ============================
    // Donnees internes caches
    // ============================

    // Bins: renderers + alphas de base (comme pour l'intro)
    private SpriteRenderer[] leftBinRenderers;
    private SpriteRenderer[] rightBinRenderers;
    private float[] leftBinBaseAlphas;
    private float[] rightBinBaseAlphas;

    // Player: renderers + scale final
    private SpriteRenderer[] playerRenderers;
    private Vector3 playerFinalScale;
    private bool playerDataInitialized = false;

    // Walls + board: renderers, scales, positions, base bottom world Y
    private Renderer leftWallRenderer;
    private Renderer rightWallRenderer;
    private Renderer boardVisualRenderer;

    private Vector3 leftWallFinalScale;
    private Vector3 rightWallFinalScale;
    private Vector3 boardVisualFinalScale;

    private Vector3 leftWallFinalLocalPos;
    private Vector3 rightWallFinalLocalPos;
    private Vector3 boardVisualFinalLocalPos;

    private float leftWallBaseBottomWorldY;
    private float rightWallBaseBottomWorldY;
    private float boardVisualBaseBottomWorldY;

    private EnergyWallFX leftWallFx;
    private EnergyWallFX rightWallFx;

    private bool visualDataInitialized = false;
    private bool binDataInitialized = false;

    private void Awake()
    {
        if (boardRoot == null)
            boardRoot = transform;

        CacheBinRenderers();
        CacheVisualComponents();
        CachePlayerRenderers();
    }

    // ============================
    // API publique
    // ============================

    /// <summary>
    /// Lance l'animation complete de rangement du board.
    /// A appeler depuis le GameFlowController juste apres le dernier flush,
    /// avec un petit delai externe si besoin.
    /// Exemple:
    /// yield return StartCoroutine(boardOutro.PlayOutro());
    /// // ensuite, afficher le panel de fin / changer de scene, etc.
    /// </summary>
    public IEnumerator PlayOutro()
    {
        InitializeVisualDataIfNeeded();
        InitializePlayerDataIfNeeded();
        InitializeBinDataIfNeeded();

        // 1) Disparition du player
        if (playerRoot != null)
        {
            yield return StartCoroutine(PlayPlayerDisappear());
        }

        // 2) Repli des murs + board (sabre laser inverse)
        yield return StartCoroutine(PlayWallsAndBoardShrink());

        // 3) Slide des bins vers leurs X d'outro
        yield return StartCoroutine(PlayBinsSlideOutro());

        // 4) Fade out des bins
        yield return StartCoroutine(PlayBinsFadeOut());

        // 5) Petit delai final
        if (finalDelay > 0f)
            yield return new WaitForSeconds(finalDelay);
    }

    // ============================
    // Etapes de l'outro
    // ============================

    private IEnumerator PlayPlayerDisappear()
    {
        if (!playerDataInitialized || playerRoot == null)
            yield break;

        float duration = Mathf.Max(0.01f, playerDisappearDuration);
        float elapsed = 0f;

        Vector3 startScale = playerRoot.localScale;
        if (startScale == Vector3.zero)
        {
            // Si jamais on est deja a 0 (cas bizarre), on force a l'echelle finale connue
            startScale = playerFinalScale;
        }

        // Alpha start = 1 (on suppose player visible)
        SetPlayerAlpha(1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            playerRoot.localScale = Vector3.Lerp(startScale, Vector3.zero, easedT);
            SetPlayerAlpha(1f - t);

            yield return null;
        }

        playerRoot.localScale = Vector3.zero;
        SetPlayerAlpha(0f);
    }

    private IEnumerator PlayWallsAndBoardShrink()
    {
        float duration = Mathf.Max(0.01f, wallsShrinkDuration);
        float elapsed = 0f;

        // Pulse FX sur les murs, comme a l'assemble
        if (leftWallFx != null)
            leftWallFx.TriggerPulse();

        if (rightWallFx != null)
            rightWallFx.TriggerPulse();

        // Etat initial: factor 1 (etat final assemble)
        SetVerticalScaleFromBottom(leftWallVisual, leftWallRenderer, leftWallFinalScale, leftWallBaseBottomWorldY, 1f);
        SetVerticalScaleFromBottom(rightWallVisual, rightWallRenderer, rightWallFinalScale, rightWallBaseBottomWorldY, 1f);
        SetVerticalScaleFromBottom(boardVisual, boardVisualRenderer, boardVisualFinalScale, boardVisualBaseBottomWorldY, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            // On veut aller de factor = 1 vers factor = 0
            float factor = 1f - easedT;

            SetVerticalScaleFromBottom(leftWallVisual, leftWallRenderer, leftWallFinalScale, leftWallBaseBottomWorldY, factor);
            SetVerticalScaleFromBottom(rightWallVisual, rightWallRenderer, rightWallFinalScale, rightWallBaseBottomWorldY, factor);
            SetVerticalScaleFromBottom(boardVisual, boardVisualRenderer, boardVisualFinalScale, boardVisualBaseBottomWorldY, factor);

            yield return null;
        }

        // Etat final: factor 0
        SetVerticalScaleFromBottom(leftWallVisual, leftWallRenderer, leftWallFinalScale, leftWallBaseBottomWorldY, 0f);
        SetVerticalScaleFromBottom(rightWallVisual, rightWallRenderer, rightWallFinalScale, rightWallBaseBottomWorldY, 0f);
        SetVerticalScaleFromBottom(boardVisual, boardVisualRenderer, boardVisualFinalScale, boardVisualBaseBottomWorldY, 0f);

        // Optionnel: desactiver les murs + board une fois invisibles
        if (wallsRoot != null)
            wallsRoot.gameObject.SetActive(false);

        if (boardVisual != null)
            boardVisual.gameObject.SetActive(false);
    }

    private IEnumerator PlayBinsSlideOutro()
    {
        if (leftBinRoot == null && rightBinRoot == null)
            yield break;

        float duration = Mathf.Max(0.01f, binsOutroSlideDuration);
        float elapsed = 0f;

        float leftStartX = leftBinRoot != null ? leftBinRoot.position.x : 0f;
        float rightStartX = rightBinRoot != null ? rightBinRoot.position.x : 0f;

        // Si les X d'outro ne sont pas renseignes, on garde les X actuels
        float leftTargetX = (leftBinRoot != null && !Mathf.Approximately(leftBinOutroX, 0f))
            ? leftBinOutroX
            : leftStartX;

        float rightTargetX = (rightBinRoot != null && !Mathf.Approximately(rightBinOutroX, 0f))
            ? rightBinOutroX
            : rightStartX;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            if (leftBinRoot != null)
            {
                Vector3 pos = leftBinRoot.position;
                pos.x = Mathf.Lerp(leftStartX, leftTargetX, easedT);
                leftBinRoot.position = pos;
            }

            if (rightBinRoot != null)
            {
                Vector3 pos = rightBinRoot.position;
                pos.x = Mathf.Lerp(rightStartX, rightTargetX, easedT);
                rightBinRoot.position = pos;
            }

            yield return null;
        }

        if (leftBinRoot != null)
        {
            Vector3 pos = leftBinRoot.position;
            pos.x = leftTargetX;
            leftBinRoot.position = pos;
        }

        if (rightBinRoot != null)
        {
            Vector3 pos = rightBinRoot.position;
            pos.x = rightTargetX;
            rightBinRoot.position = pos;
        }
    }

    private IEnumerator PlayBinsFadeOut()
    {
        InitializeBinDataIfNeeded();

        float duration = Mathf.Max(0.01f, binsFadeOutDuration);
        float elapsed = 0f;

        // On part d'un factor = 1 (alpha de base) vers 0
        SetBinsAlpha(1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float factor = 1f - t;

            SetBinsAlpha(factor);

            yield return null;
        }

        SetBinsAlpha(0f);

        // Optionnel: desactiver les bins quand ils sont invisibles
        if (leftBinRoot != null)
            leftBinRoot.gameObject.SetActive(false);

        if (rightBinRoot != null)
            rightBinRoot.gameObject.SetActive(false);
    }

    // ============================
    // Initialisation des donnees
    // ============================

    private void CacheBinRenderers()
    {
        if (leftBinRoot != null)
        {
            leftBinRenderers = leftBinRoot.GetComponentsInChildren<SpriteRenderer>(true);
            leftBinBaseAlphas = new float[leftBinRenderers.Length];
            for (int i = 0; i < leftBinRenderers.Length; i++)
            {
                SpriteRenderer r = leftBinRenderers[i];
                leftBinBaseAlphas[i] = r != null ? r.color.a : 1f;
            }
        }

        if (rightBinRoot != null)
        {
            rightBinRenderers = rightBinRoot.GetComponentsInChildren<SpriteRenderer>(true);
            rightBinBaseAlphas = new float[rightBinRenderers.Length];
            for (int i = 0; i < rightBinRenderers.Length; i++)
            {
                SpriteRenderer r = rightBinRenderers[i];
                rightBinBaseAlphas[i] = r != null ? r.color.a : 1f;
            }
        }
    }

    private void CacheVisualComponents()
    {
        if (leftWallVisual != null)
        {
            if (leftWallRenderer == null)
                leftWallRenderer = leftWallVisual.GetComponentInChildren<Renderer>(true);

            if (leftWallFx == null)
                leftWallFx = leftWallVisual.GetComponent<EnergyWallFX>();
        }

        if (rightWallVisual != null)
        {
            if (rightWallRenderer == null)
                rightWallRenderer = rightWallVisual.GetComponentInChildren<Renderer>(true);

            if (rightWallFx == null)
                rightWallFx = rightWallVisual.GetComponent<EnergyWallFX>();
        }

        if (boardVisual != null && boardVisualRenderer == null)
            boardVisualRenderer = boardVisual.GetComponentInChildren<Renderer>(true);
    }

    private void CachePlayerRenderers()
    {
        if (playerRoot != null && playerRenderers == null)
            playerRenderers = playerRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void InitializeVisualDataIfNeeded()
    {
        if (visualDataInitialized)
            return;

        // Walls
        if (leftWallVisual != null)
        {
            leftWallFinalScale = leftWallVisual.localScale;
            leftWallFinalLocalPos = leftWallVisual.localPosition;

            if (leftWallRenderer != null)
            {
                Bounds b = leftWallRenderer.bounds;
                float halfHeight = b.extents.y;
                leftWallBaseBottomWorldY = b.center.y - halfHeight;
            }
        }

        if (rightWallVisual != null)
        {
            rightWallFinalScale = rightWallVisual.localScale;
            rightWallFinalLocalPos = rightWallVisual.localPosition;

            if (rightWallRenderer != null)
            {
                Bounds b = rightWallRenderer.bounds;
                float halfHeight = b.extents.y;
                rightWallBaseBottomWorldY = b.center.y - halfHeight;
            }
        }

        // Board visual
        if (boardVisual != null)
        {
            boardVisualFinalScale = boardVisual.localScale;
            boardVisualFinalLocalPos = boardVisual.localPosition;

            if (boardVisualRenderer != null)
            {
                Bounds b = boardVisualRenderer.bounds;
                float halfHeight = b.extents.y;
                boardVisualBaseBottomWorldY = b.center.y - halfHeight;
            }
        }

        visualDataInitialized = true;
    }

    private void InitializePlayerDataIfNeeded()
    {
        if (playerDataInitialized)
            return;

        if (playerRoot != null)
        {
            playerFinalScale = playerRoot.localScale;

            if (playerRenderers == null)
                playerRenderers = playerRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }

        playerDataInitialized = true;
    }

    private void InitializeBinDataIfNeeded()
    {
        if (binDataInitialized)
            return;

        // Si on a change les alphas en runtime (par exemple via l'intro),
        // on considere que l'etat courant correspond au "1.0" de l'outro.
        if (leftBinRenderers != null && leftBinBaseAlphas != null)
        {
            for (int i = 0; i < leftBinRenderers.Length; i++)
            {
                SpriteRenderer r = leftBinRenderers[i];
                if (r == null) continue;

                leftBinBaseAlphas[i] = r.color.a;
            }
        }

        if (rightBinRenderers != null && rightBinBaseAlphas != null)
        {
            for (int i = 0; i < rightBinRenderers.Length; i++)
            {
                SpriteRenderer r = rightBinRenderers[i];
                if (r == null) continue;

                rightBinBaseAlphas[i] = r.color.a;
            }
        }

        binDataInitialized = true;
    }

    // ============================
    // Helpers d'alpha / scale
    // ============================

    private void SetBinsAlpha(float factor)
    {
        factor = Mathf.Clamp01(factor);

        if (leftBinRenderers != null)
        {
            for (int i = 0; i < leftBinRenderers.Length; i++)
            {
                SpriteRenderer r = leftBinRenderers[i];
                if (r == null) continue;

                float baseA = (leftBinBaseAlphas != null && i < leftBinBaseAlphas.Length)
                    ? leftBinBaseAlphas[i]
                    : r.color.a;

                Color c = r.color;
                c.a = baseA * factor;
                r.color = c;
            }
        }

        if (rightBinRenderers != null)
        {
            for (int i = 0; i < rightBinRenderers.Length; i++)
            {
                SpriteRenderer r = rightBinRenderers[i];
                if (r == null) continue;

                float baseA = (rightBinBaseAlphas != null && i < rightBinBaseAlphas.Length)
                    ? rightBinBaseAlphas[i]
                    : r.color.a;

                Color c = r.color;
                c.a = baseA * factor;
                r.color = c;
            }
        }
    }

    private void SetPlayerAlpha(float alpha)
    {
        if (playerRenderers == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            SpriteRenderer r = playerRenderers[i];
            if (r == null) continue;

            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }

    /// <summary>
    /// Applique un facteur de scale vertical (0 -> 1) a un visuel,
    /// en gardant son pied fixe a baseBottomWorldY (sabre laser).
    /// </summary>
    private void SetVerticalScaleFromBottom(
        Transform visual,
        Renderer visualRenderer,
        Vector3 finalScale,
        float baseBottomWorldY,
        float factor)
    {
        if (visual == null || visualRenderer == null)
            return;

        factor = Mathf.Clamp01(factor);

        // 1) Scale en Y
        Vector3 s = finalScale;
        s.y = Mathf.Lerp(0f, finalScale.y, factor);
        visual.localScale = s;

        // 2) Repositionner pour garder le bas fixe
        Bounds b = visualRenderer.bounds;
        float halfHeight = b.extents.y;

        Vector3 posWorld = visual.position;
        posWorld.y = baseBottomWorldY + halfHeight;
        visual.position = posWorld;
    }
}
