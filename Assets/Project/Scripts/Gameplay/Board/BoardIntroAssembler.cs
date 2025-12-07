using System.Collections;
using UnityEngine;

/// <summary>
/// Gère l'état et l'animation d'intro du plateau.
/// Au démarrage :
/// - tous les enfants de BoardRoot sont désactivés sauf les obstacles
/// - les X finaux des bins sont mémorisés à partir de leur position dans la scène
/// - les bins sont déplacés sur leurs X de départ (LeftBinStartX / RightBinStartX), Y inchangé, alpha 0
/// - wallsRoot et BoardVisual sont désactivés, leurs états finaux sont mémorisés
/// - le player est désactivé, son scale final est mémorisé
/// PlayAssembly() :
/// - réactive les bins
/// - fade-in des bins à leur position de départ
/// - slide des bins vers leur X final
/// - légère pause
/// - activation de wallsRoot et du BoardVisual, pulse FX, déploiement vertical murs + board depuis le bas
/// - apparition du player (scale + fade)
/// ForceAssembledState() :
/// - remet bins + murs + board + player en état final, alpha 1 (pour le Skip)
/// </summary>
public class BoardIntroAssembler : MonoBehaviour
{
    [Header("Board Root")]
    [Tooltip("Transform racine du plateau. Si null, utilise ce transform.")]
    [SerializeField] private Transform boardRoot;

    [Tooltip("Racine des obstacles (ne sera pas désactivée au démarrage).")]
    [SerializeField] private Transform obstaclesRoot;

    [Header("Walls Root")]
    [Tooltip("Parent commun des murs (gauche + droite). Exemple : 'EnergyWalls'.")]
    [SerializeField] private Transform wallsRoot;

    [Header("Board Visual")]
    [Tooltip("Quad visuel du plateau (fond, glass, etc.).")]
    [SerializeField] private Transform boardVisual;

    [Header("Bins")]
    [Tooltip("Racine du bin gauche (Transform en world space).")]
    [SerializeField] private Transform leftBinRoot;

    [Tooltip("Racine du bin droit (Transform en world space).")]
    [SerializeField] private Transform rightBinRoot;

    [Header("Bins Start X")]
    [Tooltip("Position X de départ du bin gauche pendant l'intro.")]
    [SerializeField] private float leftBinStartX;

    [Tooltip("Position X de départ du bin droit pendant l'intro.")]
    [SerializeField] private float rightBinStartX;

    [Header("Bins timings")]
    [Tooltip("Durée du fade-in des bins à leur position de départ.")]
    [SerializeField] private float binsFadeDuration = 0.3f;

    [Tooltip("Durée du slide des bins de leur X de départ vers leur X final.")]
    [SerializeField] private float binsSlideDuration = 0.35f;

    [Header("Walls visuals (quads avec EnergyWallFX)")]
    [Tooltip("Quad visuel du mur gauche (porte EnergyWallFX).")]
    [SerializeField] private Transform leftWallVisual;

    [Tooltip("Quad visuel du mur droit (porte EnergyWallFX).")]
    [SerializeField] private Transform rightWallVisual;

    [Header("Walls timings")]
    [Tooltip("Délai après la fin du slide des bins avant d'activer et déployer murs + board.")]
    [SerializeField] private float wallsDelayAfterBins = 0.1f;

    [Tooltip("Durée du déploiement des murs et du BoardVisual.")]
    [SerializeField] private float wallsGrowDuration = 0.25f;

    [Header("Player")]
    [Tooltip("Racine visuelle du player (paddle).")]
    [SerializeField] private Transform playerRoot;

    [Tooltip("Durée de l'apparition du player (scale + fade).")]
    [SerializeField] private float playerAppearDuration = 0.25f;

    // Bins : données finales
    private float leftBinFinalX;
    private float rightBinFinalX;
    private bool binFinalPositionsInitialized = false;

    private SpriteRenderer[] leftBinRenderers;
    private SpriteRenderer[] rightBinRenderers;

    // Alphas de base des sprites des bins (pour respecter les réglages d'origine, ex: Glass à 0.6)
    private float[] leftBinBaseAlphas;
    private float[] rightBinBaseAlphas;

    // Walls : données finales
    private Vector3 leftWallFinalScale;
    private Vector3 rightWallFinalScale;
    private Vector3 leftWallFinalLocalPos;
    private Vector3 rightWallFinalLocalPos;
    private float leftWallBaseBottomWorldY;
    private float rightWallBaseBottomWorldY;

    private Renderer leftWallRenderer;
    private Renderer rightWallRenderer;

    private EnergyWallFX leftWallFx;
    private EnergyWallFX rightWallFx;

    // BoardVisual : données finales
    private Vector3 boardVisualFinalScale;
    private Vector3 boardVisualFinalLocalPos;
    private float boardVisualBaseBottomWorldY;
    private Renderer boardVisualRenderer;

    // Player : données finales
    private bool playerDataInitialized = false;
    private Vector3 playerFinalScale;
    private SpriteRenderer[] playerRenderers;

    private bool visualDataInitialized = false;

    private void Awake()
    {
        if (boardRoot == null)
            boardRoot = transform;

        CacheBinRenderers();
        CacheVisualComponents();
        CachePlayerRenderers();
    }

    /// <summary>
    /// Prépare l'état initial du plateau.
    /// Appelé depuis LevelIntroSequenceController au lancement de l'intro.
    /// </summary>
    public void PrepareInitialState()
    {
        if (boardRoot == null)
            boardRoot = transform;

        DisableAllChildrenExceptObstacles();

        // Walls root désactivé au début (murs invisibles)
        if (wallsRoot != null)
            wallsRoot.gameObject.SetActive(false);

        // BoardVisual désactivé par DisableAllChildrenExceptObstacles, on ne le réactive pas ici

        InitializeFinalBinPositionsIfNeeded();
        InitializeVisualDataIfNeeded();
        InitializePlayerDataIfNeeded();

        // Si les X de départ ne sont pas configurés, on prend par défaut les X finaux
        if (leftBinRoot != null && Mathf.Approximately(leftBinStartX, 0f))
            leftBinStartX = leftBinFinalX;

        if (rightBinRoot != null && Mathf.Approximately(rightBinStartX, 0f))
            rightBinStartX = rightBinFinalX;

        // Placer les bins sur leurs X de départ, Y inchangé
        if (leftBinRoot != null)
        {
            Vector3 pos = leftBinRoot.position;
            pos.x = leftBinStartX;
            leftBinRoot.position = pos;
        }

        if (rightBinRoot != null)
        {
            Vector3 pos = rightBinRoot.position;
            pos.x = rightBinStartX;
            rightBinRoot.position = pos;
        }

        // Bins alpha 0 au démarrage (facteur 0 sur l'alpha de base)
        SetBinsAlpha(0f);

        // Player désactivé au début (on l'affichera après les murs)
        if (playerRoot != null)
            playerRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Lance l'animation complète d'assemblage du plateau.
    /// Appelée par LevelIntroSequenceController via StartCoroutine, en parallèle des dialogues.
    /// </summary>
    public IEnumerator PlayAssembly()
    {
        if (leftBinRoot != null)
            leftBinRoot.gameObject.SetActive(true);

        if (rightBinRoot != null)
            rightBinRoot.gameObject.SetActive(true);

        if (obstaclesRoot != null)
            obstaclesRoot.gameObject.SetActive(true);

        InitializeFinalBinPositionsIfNeeded();
        InitializeVisualDataIfNeeded();
        InitializePlayerDataIfNeeded();

        // Phase 1 : fade-in des bins
        float fadeDuration = Mathf.Max(0.01f, binsFadeDuration);
        float elapsed = 0f;

        SetBinsAlpha(0f);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            SetBinsAlpha(t);
            yield return null;
        }

        SetBinsAlpha(1f);

        // Phase 2 : slide des bins vers la position finale
        float slideDuration = Mathf.Max(0.01f, binsSlideDuration);
        elapsed = 0f;

        float leftStartXCurrent = leftBinRoot != null ? leftBinRoot.position.x : leftBinStartX;
        float rightStartXCurrent = rightBinRoot != null ? rightBinRoot.position.x : rightBinStartX;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            if (leftBinRoot != null)
            {
                Vector3 pos = leftBinRoot.position;
                pos.x = Mathf.Lerp(leftStartXCurrent, leftBinFinalX, easedT);
                leftBinRoot.position = pos;
            }

            if (rightBinRoot != null)
            {
                Vector3 pos = rightBinRoot.position;
                pos.x = Mathf.Lerp(rightStartXCurrent, rightBinFinalX, easedT);
                rightBinRoot.position = pos;
            }

            yield return null;
        }

        // Position finale exacte des bins
        if (leftBinRoot != null)
        {
            Vector3 pos = leftBinRoot.position;
            pos.x = leftBinFinalX;
            leftBinRoot.position = pos;
        }

        if (rightBinRoot != null)
        {
            Vector3 pos = rightBinRoot.position;
            pos.x = rightBinFinalX;
            rightBinRoot.position = pos;
        }

        // Phase 3 : légère pause
        if (wallsDelayAfterBins > 0f)
            yield return new WaitForSeconds(wallsDelayAfterBins);

        // Phase 4 : activation murs + board, puis montée façon sabre laser
        float growDuration = Mathf.Max(0.01f, wallsGrowDuration);
        elapsed = 0f;

        if (wallsRoot != null)
            wallsRoot.gameObject.SetActive(true);

        if (boardVisual != null)
            boardVisual.gameObject.SetActive(true);

        // Etat initial de la montée : factor 0
        SetVerticalScaleFromBottom(leftWallVisual, leftWallRenderer, leftWallFinalScale, leftWallBaseBottomWorldY, 0f);
        SetVerticalScaleFromBottom(rightWallVisual, rightWallRenderer, rightWallFinalScale, rightWallBaseBottomWorldY, 0f);
        SetVerticalScaleFromBottom(boardVisual, boardVisualRenderer, boardVisualFinalScale, boardVisualBaseBottomWorldY, 0f);

        // Pulse FX sur chaque mur
        if (leftWallFx != null)
            leftWallFx.TriggerPulse();

        if (rightWallFx != null)
            rightWallFx.TriggerPulse();

        // Animation sabre laser : factor 0 -> 1 pour murs + board
        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            SetVerticalScaleFromBottom(leftWallVisual, leftWallRenderer, leftWallFinalScale, leftWallBaseBottomWorldY, easedT);
            SetVerticalScaleFromBottom(rightWallVisual, rightWallRenderer, rightWallFinalScale, rightWallBaseBottomWorldY, easedT);
            SetVerticalScaleFromBottom(boardVisual, boardVisualRenderer, boardVisualFinalScale, boardVisualBaseBottomWorldY, easedT);

            yield return null;
        }

        // Etat final exact des murs + board
        if (leftWallVisual != null)
        {
            leftWallVisual.localScale = leftWallFinalScale;
            leftWallVisual.localPosition = leftWallFinalLocalPos;
        }

        if (rightWallVisual != null)
        {
            rightWallVisual.localScale = rightWallFinalScale;
            rightWallVisual.localPosition = rightWallFinalLocalPos;
        }

        if (boardVisual != null)
        {
            boardVisual.localScale = boardVisualFinalScale;
            boardVisual.localPosition = boardVisualFinalLocalPos;
        }

        // Phase 5 : apparition du player (scale + fade)
        if (playerRoot != null)
            yield return StartCoroutine(PlayPlayerAppear());
    }

    /// <summary>
    /// Force l'état final du board (utilisé pour le Skip).
    /// </summary>
    public void ForceAssembledState()
    {
        if (boardRoot == null)
            boardRoot = transform;

        if (obstaclesRoot != null)
            obstaclesRoot.gameObject.SetActive(true);

        if (wallsRoot != null)
            wallsRoot.gameObject.SetActive(true);

        if (boardVisual != null)
            boardVisual.gameObject.SetActive(true);

        if (leftBinRoot != null)
            leftBinRoot.gameObject.SetActive(true);

        if (rightBinRoot != null)
            rightBinRoot.gameObject.SetActive(true);

        InitializeFinalBinPositionsIfNeeded();
        InitializeVisualDataIfNeeded();
        InitializePlayerDataIfNeeded();

        if (leftBinRoot != null)
        {
            Vector3 pos = leftBinRoot.position;
            pos.x = leftBinFinalX;
            leftBinRoot.position = pos;
        }

        if (rightBinRoot != null)
        {
            Vector3 pos = rightBinRoot.position;
            pos.x = rightBinFinalX;
            rightBinRoot.position = pos;
        }

        // Factor 1 = alpha de base pour tous les sprites des bins
        SetBinsAlpha(1f);

        if (leftWallVisual != null)
        {
            leftWallVisual.localScale = leftWallFinalScale;
            leftWallVisual.localPosition = leftWallFinalLocalPos;
        }

        if (rightWallVisual != null)
        {
            rightWallVisual.localScale = rightWallFinalScale;
            rightWallVisual.localPosition = rightWallFinalLocalPos;
        }

        if (boardVisual != null)
        {
            boardVisual.localScale = boardVisualFinalScale;
            boardVisual.localPosition = boardVisualFinalLocalPos;
        }

        if (playerRoot != null)
        {
            playerRoot.gameObject.SetActive(true);
            playerRoot.localScale = playerFinalScale;
            SetPlayerAlpha(1f);
        }
    }

    // ============================
    // PLAYER INTRO
    // ============================

    private IEnumerator PlayPlayerAppear()
    {
        if (playerRoot == null)
            yield break;

        float duration = Mathf.Max(0.01f, playerAppearDuration);
        float elapsed = 0f;

        playerRoot.gameObject.SetActive(true);

        // Start : scale 0, alpha 0
        playerRoot.localScale = Vector3.zero;
        SetPlayerAlpha(0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            playerRoot.localScale = Vector3.Lerp(Vector3.zero, playerFinalScale, easedT);
            SetPlayerAlpha(t);

            yield return null;
        }

        playerRoot.localScale = playerFinalScale;
        SetPlayerAlpha(1f);
    }

    // ============================
    // INTERNAL HELPERS
    // ============================

    private void DisableAllChildrenExceptObstacles()
    {
        if (boardRoot == null)
            return;

        for (int i = 0; i < boardRoot.childCount; i++)
        {
            Transform child = boardRoot.GetChild(i);
            if (child == null)
                continue;

            if (obstaclesRoot != null && (child == obstaclesRoot || obstaclesRoot.IsChildOf(child)))
            {
                child.gameObject.SetActive(true);
                continue;
            }

            child.gameObject.SetActive(false);
        }

        if (obstaclesRoot != null)
        {
            obstaclesRoot.gameObject.SetActive(true);

            for (int i = 0; i < obstaclesRoot.childCount; i++)
            {
                Transform c = obstaclesRoot.GetChild(i);
                if (c != null)
                    c.gameObject.SetActive(true);
            }
        }
    }

    private void InitializeFinalBinPositionsIfNeeded()
    {
        if (binFinalPositionsInitialized)
            return;

        if (leftBinRoot != null)
            leftBinFinalX = leftBinRoot.position.x;

        if (rightBinRoot != null)
            rightBinFinalX = rightBinRoot.position.x;

        binFinalPositionsInitialized = true;
    }

    private void InitializeVisualDataIfNeeded()
    {
        if (visualDataInitialized)
            return;

        // Renderers
        if (leftWallVisual != null && leftWallRenderer == null)
            leftWallRenderer = leftWallVisual.GetComponentInChildren<Renderer>(true);

        if (rightWallVisual != null && rightWallRenderer == null)
            rightWallRenderer = rightWallVisual.GetComponentInChildren<Renderer>(true);

        if (boardVisual != null && boardVisualRenderer == null)
            boardVisualRenderer = boardVisual.GetComponentInChildren<Renderer>(true);

        // Scales + positions finales (telles que dans la scène)
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

        // FX auto-récupérés sur les mêmes GO que les visuels
        if (leftWallVisual != null && leftWallFx == null)
            leftWallFx = leftWallVisual.GetComponent<EnergyWallFX>();

        if (rightWallVisual != null && rightWallFx == null)
            rightWallFx = rightWallVisual.GetComponent<EnergyWallFX>();

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

    /// <summary>
    /// Applique un facteur d'alpha (0 -> 1) à tous les sprites des bins,
    /// en le multipliant par leur alpha de base (respect du Glass à 0.6, etc.).
    /// </summary>
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
    /// Applique un facteur de scale vertical (0 -> 1) à un visuel,
    /// en gardant son pied fixé à baseBottomWorldY.
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
