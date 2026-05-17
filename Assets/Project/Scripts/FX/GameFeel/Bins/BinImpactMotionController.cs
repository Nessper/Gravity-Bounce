using UnityEngine;

/// <summary>
/// Gère les réactions visuelles des bins aux impacts.
/// 
/// Idée :
/// - CloseWall : impact fort, le bin absorbe le choc en descendant
/// - InnerWall : impact léger, petit tremblement / petite absorption
///
/// À placer sur GameFeelRoot.
/// </summary>
public class BinImpactMotionController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform leftBinVisual;
    [SerializeField] private Transform rightBinVisual;

    [Header("Close Wall Absorption")]
    [SerializeField] private float closeWallYOffset = 0.05f;
    [SerializeField] private float closeWallMaxYOffset = 0.12f;

    [Header("Inner Wall Absorption")]
    [SerializeField] private float innerWallYOffset = 0.015f;
    [SerializeField] private float innerWallMaxYOffset = 0.04f;

    [Header("Return")]
    [SerializeField] private float returnSpeed = 4f;

    [Header("Rattle")]
    [SerializeField] private float closeWallRattleAmplitude = 0.004f;
    [SerializeField] private float innerWallRattleAmplitude = 0.002f;
    [SerializeField] private float rattleFrequency = 70f;
    [SerializeField] private float rattleDuration = 0.06f;

    [Header("Filtering")]
    [Range(0f, 1f)]
    [SerializeField] private float minVisibleStrength = 0.15f;

    private Vector3 leftBasePos;
    private Vector3 rightBasePos;

    private float leftOffsetY;
    private float rightOffsetY;

    private float leftRattleTimer;
    private float rightRattleTimer;

    private float leftRattleStrength;
    private float rightRattleStrength;

    private float leftRattleAmplitude;
    private float rightRattleAmplitude;

    private void Awake()
    {
        if (leftBinVisual != null)
            leftBasePos = leftBinVisual.localPosition;

        if (rightBinVisual != null)
            rightBasePos = rightBinVisual.localPosition;
    }

    private void OnEnable()
    {
        BinImpactDetector.OnBinImpact += HandleBinImpact;
    }

    private void OnDisable()
    {
        BinImpactDetector.OnBinImpact -= HandleBinImpact;
    }

    private void Update()
    {
        UpdateBin(
            leftBinVisual,
            leftBasePos,
            ref leftOffsetY,
            ref leftRattleTimer,
            ref leftRattleStrength,
            ref leftRattleAmplitude
        );

        UpdateBin(
            rightBinVisual,
            rightBasePos,
            ref rightOffsetY,
            ref rightRattleTimer,
            ref rightRattleStrength,
            ref rightRattleAmplitude
        );
    }

    private void UpdateBin(
        Transform target,
        Vector3 basePos,
        ref float offsetY,
        ref float rattleTimer,
        ref float rattleStrength,
        ref float rattleAmplitude)
    {
        if (target == null)
            return;

        offsetY = Mathf.Lerp(
            offsetY,
            0f,
            returnSpeed * Time.deltaTime
        );

        float rattleX = 0f;

        if (rattleTimer > 0f)
        {
            rattleTimer -= Time.deltaTime;

            float normalized = Mathf.Clamp01(rattleTimer / Mathf.Max(0.001f, rattleDuration));
            float fade = normalized;

            rattleX = Mathf.Sin(Time.time * rattleFrequency)
                      * rattleAmplitude
                      * rattleStrength
                      * fade;
        }

        Vector3 pos = basePos;
        pos.y += offsetY;
        pos.x += rattleX;

        target.localPosition = pos;
    }

    private void HandleBinImpact(Side side, BinImpactDetector.ImpactKind impactKind, float strength01)
    {
        if (strength01 < minVisibleStrength)
            return;

        float yOffset;
        float maxOffset;
        float rattleAmplitude;

        if (impactKind == BinImpactDetector.ImpactKind.CloseWall)
        {
            yOffset = closeWallYOffset;
            maxOffset = closeWallMaxYOffset;
            rattleAmplitude = closeWallRattleAmplitude;
        }
        else
        {
            yOffset = innerWallYOffset;
            maxOffset = innerWallMaxYOffset;
            rattleAmplitude = innerWallRattleAmplitude;
        }

        float yKick = -yOffset * strength01;

        if (side == Side.Left)
        {
            leftOffsetY += yKick;
            leftOffsetY = Mathf.Clamp(leftOffsetY, -maxOffset, 0f);

            leftRattleTimer = rattleDuration;
            leftRattleStrength = strength01;
            leftRattleAmplitude = rattleAmplitude;
        }
        else if (side == Side.Right)
        {
            rightOffsetY += yKick;
            rightOffsetY = Mathf.Clamp(rightOffsetY, -maxOffset, 0f);

            rightRattleTimer = rattleDuration;
            rightRattleStrength = strength01;
            rightRattleAmplitude = rattleAmplitude;
        }
    }
}