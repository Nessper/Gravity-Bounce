using UnityEngine;

/// <summary>
/// Réaction physique et lumineuse du vaisseau lors d'un flush.
/// 
/// Idée :
/// - flush gauche  => le vaisseau part légèrement à droite
/// - flush droite  => le vaisseau part légèrement à gauche
/// - l'image scale down brièvement, comme si le vaisseau absorbait le choc
/// - le vaisseau reçoit un court flash énergétique
///
/// À placer sur GameFeelRoot.
/// </summary>
public class ShipFlushMotionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BinCollector binCollector;
    [SerializeField] private Transform motionRoot;

    [Header("Flash Renderers")]
    [Tooltip("Renderer principal du vaisseau. Sert à copier automatiquement le sprite courant.")]
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Tooltip("Renderer overlay utilisé uniquement pour le flash.")]
    [SerializeField] private SpriteRenderer flashRenderer;

    [Header("Flash Colors")]
    [SerializeField] private Color normalFlushColor = new Color(0f, 0.9f, 1f, 0.35f);
    [SerializeField] private Color blackFlushColor = new Color(0.8f, 0f, 1f, 0.45f);

    [Header("Side Kick")]
    [SerializeField] private float baseSideOffset = 0.04f;
    [SerializeField] private float maxSideOffset = 0.12f;

    [Header("Compression / Scale Down")]
    [SerializeField] private float baseScaleDown = 0.015f;
    [SerializeField] private float maxScaleDown = 0.04f;

    [Header("Flash")]
    [SerializeField] private float baseFlashAlpha = 0.25f;
    [SerializeField] private float maxFlashAlpha = 0.55f;
    [SerializeField] private float flashReturnSpeed = 14f;

    [Header("Strength")]
    [SerializeField] private int ballsForMaxStrength = 8;
    [SerializeField] private float blackMultiplier = 1.5f;

    [Header("Return")]
    [SerializeField] private float positionReturnSpeed = 14f;
    [SerializeField] private float scaleReturnSpeed = 18f;

    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;

    private float currentOffsetX;
    private float currentScaleDown;

    private float currentFlashAlpha;
    private Color currentFlashBaseColor;

    private void Awake()
    {
        if (motionRoot == null)
        {
            Debug.LogWarning("[ShipFlushMotionController] motionRoot non assigné.");
            enabled = false;
            return;
        }

        baseLocalPosition = motionRoot.localPosition;
        baseLocalScale = motionRoot.localScale;

        currentFlashBaseColor = normalFlushColor;

        RefreshFlashSprite();
        ResetFlashRenderer();
    }

    private void OnEnable()
    {
        if (binCollector != null)
            binCollector.OnBinFlushed += HandleBinFlushed;
    }

    private void OnDisable()
    {
        if (binCollector != null)
            binCollector.OnBinFlushed -= HandleBinFlushed;
    }

    private void Update()
    {
        UpdateMotion();
        UpdateFlash();
    }

    /// <summary>
    /// À appeler plus tard si le sprite du vaisseau change dynamiquement après Awake.
    /// </summary>
    public void RefreshFlashSprite()
    {
        if (flashRenderer == null || sourceRenderer == null)
            return;

        flashRenderer.sprite = sourceRenderer.sprite;
        flashRenderer.flipX = sourceRenderer.flipX;
        flashRenderer.flipY = sourceRenderer.flipY;

        flashRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        flashRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;

        flashRenderer.transform.localPosition = Vector3.zero;
        flashRenderer.transform.localRotation = Quaternion.identity;
        flashRenderer.transform.localScale = Vector3.one;
    }

    private void ResetFlashRenderer()
    {
        if (flashRenderer == null)
            return;

        Color c = normalFlushColor;
        c.a = 0f;
        flashRenderer.color = c;
    }

    private void UpdateMotion()
    {
        currentOffsetX = Mathf.Lerp(
            currentOffsetX,
            0f,
            positionReturnSpeed * Time.deltaTime
        );

        currentScaleDown = Mathf.Lerp(
            currentScaleDown,
            0f,
            scaleReturnSpeed * Time.deltaTime
        );

        Vector3 pos = baseLocalPosition;
        pos.x += currentOffsetX;
        motionRoot.localPosition = pos;

        float scaleFactor = 1f - currentScaleDown;
        motionRoot.localScale = baseLocalScale * scaleFactor;
    }

    private void UpdateFlash()
    {
        if (flashRenderer.sprite == null)
            RefreshFlashSprite();

        if (flashRenderer == null)
            return;

        currentFlashAlpha = Mathf.Lerp(
            currentFlashAlpha,
            0f,
            flashReturnSpeed * Time.deltaTime
        );

        Color c = currentFlashBaseColor;
        c.a = currentFlashAlpha;
        flashRenderer.color = c;
    }

    private void HandleBinFlushed(Side side, BinSnapshot snapshot, int blackCount)
    {
        if (snapshot == null)
            return;

        RefreshFlashSprite();

        int ballCount = Mathf.Max(1, snapshot.nombreDeBilles);
        float strength01 = Mathf.Clamp01(ballCount / (float)Mathf.Max(1, ballsForMaxStrength));

        if (blackCount > 0)
            strength01 = Mathf.Clamp01(strength01 * blackMultiplier);

        ApplyMotion(side, strength01);
        ApplyFlash(strength01, blackCount > 0);
    }

    private void ApplyMotion(Side side, float strength01)
    {
        float direction = side == Side.Left ? 1f : -1f;

        float sideKick = baseSideOffset * strength01;
        float scaleKick = baseScaleDown * strength01;

        currentOffsetX += direction * sideKick;
        currentOffsetX = Mathf.Clamp(currentOffsetX, -maxSideOffset, maxSideOffset);

        currentScaleDown += scaleKick;
        currentScaleDown = Mathf.Clamp(currentScaleDown, 0f, maxScaleDown);
    }

    private void ApplyFlash(float strength01, bool hasBlack)
    {
        if (flashRenderer == null)
            return;

        currentFlashBaseColor = hasBlack ? blackFlushColor : normalFlushColor;

        float flashAlpha = Mathf.Lerp(baseFlashAlpha, maxFlashAlpha, strength01);
        currentFlashAlpha = Mathf.Max(currentFlashAlpha, flashAlpha);
    }
}