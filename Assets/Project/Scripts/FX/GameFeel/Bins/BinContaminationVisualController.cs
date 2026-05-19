using UnityEngine;

/// <summary>
/// Pilote la contamination visuelle des bins selon le nombre
/// de billes noires présentes dans chaque bin.
///
/// A placer sur GameFeelRoot.
///
/// Objectif visuel :
/// - 0 noire : bin normal.
/// - 1 noire : contamination visible + buzz electrique.
/// - 2 noires : glitch plus frequent et plus sale.
/// - 3+ noires : instabilite forte.
/// </summary>
public class BinContaminationVisualController : MonoBehaviour
{
    [System.Serializable]
    private class BinVisualSet
    {
        public string name;
        public BinTrigger trigger;
        public SpriteRenderer glass;
        public SpriteRenderer glow;

        [HideInInspector] public float contamination;

        [HideInInspector] public Vector3 glassBaseLocalPosition;
        [HideInInspector] public Vector3 glassBaseLocalScale;
        [HideInInspector] public Vector3 glowBaseLocalPosition;
        [HideInInspector] public Vector3 glowBaseLocalScale;

        [HideInInspector] public bool hasGlassBase;
        [HideInInspector] public bool hasGlowBase;

        [HideInInspector] public float nextBurstTimer;
        [HideInInspector] public float burstTimer;
        [HideInInspector] public float burstDuration;

        [HideInInspector] public float frameTimer;
        [HideInInspector] public bool frameGlowVisible;
        [HideInInspector] public Color frameGlowColor;
        [HideInInspector] public Vector2 frameGlassOffset;
        [HideInInspector] public Vector2 frameGlowOffset;
        [HideInInspector] public Vector3 frameGlowScale;
    }

    [Header("Bins")]
    [SerializeField] private BinVisualSet leftBin;
    [SerializeField] private BinVisualSet rightBin;

    [Header("General")]
    [SerializeField] private float lerpSpeed = 14f;

    [Header("Glass Colors")]
    [SerializeField] private Color cleanGlassColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color glassOneBlack = new Color(0.65f, 0f, 1f, 0.6f);
    [SerializeField] private Color glassTwoBlack = new Color(1f, 0f, 0.55f, 0.6f);
    [SerializeField] private Color glassThreeBlack = new Color(1f, 0f, 0.15f, 0.6f);
    [SerializeField] private Color glassBurstColor = new Color(1f, 1f, 1f, 0.6f);

    [Header("Glow Colors")]
    [SerializeField] private Color cleanGlowColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField] private Color glowOneBlack = new Color(0.75f, 0f, 1f, 1f);
    [SerializeField] private Color glowTwoBlack = new Color(1f, 0f, 0.45f, 1f);
    [SerializeField] private Color glowThreeBlack = new Color(1f, 0.05f, 0.12f, 1f);

    [Header("Burst Colors")]
    [SerializeField] private Color burstDarkViolet = new Color(0.06f, 0f, 0.16f, 1f);
    [SerializeField] private Color burstDarkRed = new Color(0.25f, 0f, 0.06f, 1f);
    [SerializeField] private Color burstWhite = new Color(1f, 1f, 1f, 1f);

    [Header("Continuous Buzz")]
    [SerializeField] private float buzzSpeed = 120f;
    [SerializeField] private float buzzColorAmount = 0.35f;
    [SerializeField] private float buzzAlphaAmount = 0.22f;

    [Header("Burst Timing")]
    [SerializeField] private Vector2 oneBlackBurstInterval = new Vector2(0.28f, 0.65f);
    [SerializeField] private Vector2 twoBlackBurstInterval = new Vector2(0.14f, 0.34f);
    [SerializeField] private Vector2 threeBlackBurstInterval = new Vector2(0.05f, 0.16f);
    [SerializeField] private Vector2 burstDuration = new Vector2(0.055f, 0.13f);
    [SerializeField] private float burstFrameRate = 70f;

    [Header("Burst Shake")]
    [SerializeField] private float oneBlackShake = 0.006f;
    [SerializeField] private float twoBlackShake = 0.012f;
    [SerializeField] private float threeBlackShake = 0.02f;

    [Header("Burst Scale")]
    [SerializeField] private float glowScaleJitterX = 0.12f;
    [SerializeField] private float glowScaleJitterY = 0.05f;

    private void Start()
    {
        CacheBase(leftBin);
        CacheBase(rightBin);

        ResetBurst(leftBin);
        ResetBurst(rightBin);

        ApplyBin(leftBin, true);
        ApplyBin(rightBin, true);
    }

    private void Update()
    {
        UpdateBin(leftBin);
        UpdateBin(rightBin);
    }

    private void CacheBase(BinVisualSet bin)
    {
        if (bin == null)
            return;

        if (bin.glass != null)
        {
            bin.glassBaseLocalPosition = bin.glass.transform.localPosition;
            bin.glassBaseLocalScale = bin.glass.transform.localScale;
            bin.hasGlassBase = true;
        }

        if (bin.glow != null)
        {
            bin.glowBaseLocalPosition = bin.glow.transform.localPosition;
            bin.glowBaseLocalScale = bin.glow.transform.localScale;
            bin.hasGlowBase = true;
        }
    }

    private void UpdateBin(BinVisualSet bin)
    {
        if (bin == null)
            return;

        int blackCount = GetBlackCount(bin);
        float target = GetTargetContamination(blackCount);

        bin.contamination = Mathf.Lerp(
            bin.contamination,
            target,
            Time.deltaTime * lerpSpeed
        );

        UpdateBurstState(bin, blackCount);
        ApplyBin(bin, false);
    }

    private int GetBlackCount(BinVisualSet bin)
    {
        if (bin == null || bin.trigger == null)
            return 0;

        return bin.trigger.BlackCount;
    }

    private float GetTargetContamination(int blackCount)
    {
        if (blackCount <= 0)
            return 0f;

        if (blackCount == 1)
            return 1f;

        if (blackCount == 2)
            return 1.35f;

        return 1.8f;
    }

    private void UpdateBurstState(BinVisualSet bin, int blackCount)
    {
        if (bin == null)
            return;

        if (blackCount <= 0)
        {
            ResetBurst(bin);
            return;
        }

        if (bin.burstTimer > 0f)
        {
            bin.burstTimer -= Time.deltaTime;
            bin.frameTimer -= Time.deltaTime;

            if (bin.frameTimer <= 0f)
                GenerateBurstFrame(bin, blackCount);

            return;
        }

        bin.nextBurstTimer -= Time.deltaTime;

        if (bin.nextBurstTimer <= 0f)
            StartBurst(bin, blackCount);
    }

    private void StartBurst(BinVisualSet bin, int blackCount)
    {
        bin.burstDuration = Random.Range(
            burstDuration.x,
            burstDuration.y
        );

        bin.burstTimer = bin.burstDuration;
        bin.frameTimer = 0f;

        GenerateBurstFrame(bin, blackCount);
        ScheduleNextBurst(bin, blackCount);
    }

    private void GenerateBurstFrame(BinVisualSet bin, int blackCount)
    {
        float shake = GetShakeAmount(blackCount);

        bin.frameTimer = 1f / Mathf.Max(1f, burstFrameRate);

        bin.frameGlassOffset = new Vector2(
            Random.Range(-shake, shake),
            Random.Range(-shake, shake)
        );

        bin.frameGlowOffset = new Vector2(
            Random.Range(-shake, shake),
            Random.Range(-shake, shake)
        );

        bin.frameGlowScale = new Vector3(
            1f + Random.Range(-glowScaleJitterX, glowScaleJitterX),
            1f + Random.Range(-glowScaleJitterY, glowScaleJitterY),
            1f
        );

        float visibilityChance = blackCount == 1 ? 0.9f : 0.75f;
        bin.frameGlowVisible = Random.value <= visibilityChance;

        float roll = Random.value;

        if (blackCount >= 3 && roll > 0.72f)
            bin.frameGlowColor = burstWhite;
        else if (blackCount >= 2 && roll > 0.45f)
            bin.frameGlowColor = burstDarkRed;
        else
            bin.frameGlowColor = burstDarkViolet;
    }

    private void ResetBurst(BinVisualSet bin)
    {
        if (bin == null)
            return;

        bin.burstTimer = 0f;
        bin.burstDuration = 0f;
        bin.frameTimer = 0f;
        bin.frameGlowVisible = true;
        bin.frameGlowColor = cleanGlowColor;
        bin.frameGlassOffset = Vector2.zero;
        bin.frameGlowOffset = Vector2.zero;
        bin.frameGlowScale = Vector3.one;

        ScheduleNextBurst(bin, GetBlackCount(bin));

        RestoreTransforms(bin);
    }

    private void ScheduleNextBurst(BinVisualSet bin, int blackCount)
    {
        if (bin == null)
            return;

        Vector2 interval = oneBlackBurstInterval;

        if (blackCount == 2)
            interval = twoBlackBurstInterval;
        else if (blackCount >= 3)
            interval = threeBlackBurstInterval;

        bin.nextBurstTimer = Random.Range(interval.x, interval.y);
    }

    private float GetShakeAmount(int blackCount)
    {
        if (blackCount <= 0)
            return 0f;

        if (blackCount == 1)
            return oneBlackShake;

        if (blackCount == 2)
            return twoBlackShake;

        return threeBlackShake;
    }

    private bool IsBursting(BinVisualSet bin)
    {
        return bin != null && bin.burstTimer > 0f;
    }

    private void ApplyBin(BinVisualSet bin, bool instant)
    {
        if (bin == null)
            return;

        int blackCount = GetBlackCount(bin);

        float t = instant
            ? GetTargetContamination(blackCount)
            : bin.contamination;

        t = Mathf.Clamp01(t);

        bool bursting = IsBursting(bin);

        ApplyGlass(bin, blackCount, t, bursting);
        ApplyGlow(bin, blackCount, t, bursting);
        ApplyTransforms(bin, bursting);
    }

    private void ApplyGlass(
        BinVisualSet bin,
        int blackCount,
        float t,
        bool bursting)
    {
        if (bin.glass == null)
            return;

        Color target = cleanGlassColor;

        if (blackCount == 1)
            target = glassOneBlack;
        else if (blackCount == 2)
            target = glassTwoBlack;
        else if (blackCount >= 3)
            target = glassThreeBlack;

        if (bursting)
            target = glassBurstColor;

        bin.glass.color = Color.Lerp(
            bin.glass.color,
            target,
            Time.deltaTime * lerpSpeed
        );
    }

    private void ApplyGlow(
        BinVisualSet bin,
        int blackCount,
        float t,
        bool bursting)
    {
        if (bin.glow == null)
            return;

        Color target = cleanGlowColor;

        if (blackCount == 1)
            target = glowOneBlack;
        else if (blackCount == 2)
            target = glowTwoBlack;
        else if (blackCount >= 3)
            target = glowThreeBlack;

        if (blackCount > 0)
            target = ApplyContinuousBuzz(bin, target, blackCount);

        if (bursting)
        {
            target = bin.frameGlowColor;
            bin.glow.enabled = bin.frameGlowVisible;
        }
        else
        {
            bin.glow.enabled = true;
        }

        bin.glow.color = Color.Lerp(
            bin.glow.color,
            target,
            Time.deltaTime * lerpSpeed
        );
    }

    private Color ApplyContinuousBuzz(
        BinVisualSet bin,
        Color source,
        int blackCount)
    {
        float seed = string.IsNullOrEmpty(bin.name)
            ? 0f
            : Mathf.Abs(bin.name.GetHashCode() * 0.001f);

        float buzz = Mathf.PerlinNoise(
            Time.time * buzzSpeed,
            seed + 13.37f
        );

        float buzzSigned = (buzz - 0.5f) * 2f;

        float countMultiplier = 1f;

        if (blackCount == 2)
            countMultiplier = 1.35f;
        else if (blackCount >= 3)
            countMultiplier = 1.8f;

        Color buzzTarget = burstDarkViolet;

        if (blackCount == 2)
            buzzTarget = burstDarkRed;
        else if (blackCount >= 3)
            buzzTarget = burstWhite;

        Color result = Color.Lerp(
            source,
            buzzTarget,
            Mathf.Abs(buzzSigned) * buzzColorAmount * countMultiplier
        );

        result.a *= 1f + buzzSigned * buzzAlphaAmount * countMultiplier;
        result.a = Mathf.Clamp01(result.a);

        return result;
    }

    private void ApplyTransforms(BinVisualSet bin, bool bursting)
    {
        if (bin == null)
            return;

        if (bin.hasGlassBase)
        {
            Vector3 glassPos = bin.glassBaseLocalPosition;

            if (bursting)
            {
                glassPos.x += bin.frameGlassOffset.x;
                glassPos.y += bin.frameGlassOffset.y;
            }

            bin.glass.transform.localPosition = glassPos;
            bin.glass.transform.localScale = bin.glassBaseLocalScale;
        }

        if (bin.hasGlowBase)
        {
            Vector3 glowPos = bin.glowBaseLocalPosition;
            Vector3 glowScale = bin.glowBaseLocalScale;

            if (bursting)
            {
                glowPos.x += bin.frameGlowOffset.x;
                glowPos.y += bin.frameGlowOffset.y;

                glowScale.x *= bin.frameGlowScale.x;
                glowScale.y *= bin.frameGlowScale.y;
            }

            bin.glow.transform.localPosition = glowPos;
            bin.glow.transform.localScale = glowScale;
        }
    }

    private void RestoreTransforms(BinVisualSet bin)
    {
        if (bin == null)
            return;

        if (bin.hasGlassBase)
        {
            bin.glass.transform.localPosition = bin.glassBaseLocalPosition;
            bin.glass.transform.localScale = bin.glassBaseLocalScale;
        }

        if (bin.hasGlowBase)
        {
            bin.glow.transform.localPosition = bin.glowBaseLocalPosition;
            bin.glow.transform.localScale = bin.glowBaseLocalScale;
            bin.glow.enabled = true;
        }
    }
}