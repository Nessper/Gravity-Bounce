using UnityEngine;

public enum BallType
{
    White,
    Blue,
    Red,
    Black
}

public class BallState : MonoBehaviour
{
    [SerializeField] private Vector3 scale = Vector3.one;
    public Vector3 Scale => scale;

    [Header("Definition")]
    [SerializeField] private BallDefinition definition;

    public BallDefinition Definition => definition;

    [Header("Type & score (set au spawn)")]
    public BallType type = BallType.White;
    public int points = 0;

    [Header("Etat de jeu")]
    public bool inBin = false;
    public bool collected = false;
    public Side currentSide = Side.None;

    [Header("Tutoriel")]
    public bool isTutorialBall = false;

    [Header("Référence visuelle")]
    [SerializeField] private Renderer visualRenderer;

    [Header("Physique")]
    [SerializeField] private Rigidbody rb;

    [Header("Black Trail Speed Filter")]
    [SerializeField] private float blackTrailStartSpeed = 4f;
    [SerializeField] private float blackTrailStopSpeed = 2f;
    [SerializeField] private float blackTrailStopDelay = 0.18f;

    [Header("Matériaux par type")]
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material blueMaterial;
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material blackMaterial;

    [Header("Trails par type")]
    [SerializeField] private TrailRenderer trailWhite;
    [SerializeField] private TrailRenderer trailBlue;
    [SerializeField] private TrailRenderer trailRed;
    [SerializeField] private TrailRenderer trailBlack;

    [Header("FX noirs")]
    [SerializeField] private ParticleSystem blackCrackleFX;

    [Header("Layers visuels")]
    [SerializeField] private string defaultLayerName = "Gameplay";
    [SerializeField] private string cleanGameplayLayerName = "CleanGameplay";

    public string TypeName => type.ToString();

    private bool initialized;
    private bool registeredAsBlackThreat;

    private int defaultLayer = -1;
    private int blackBallLayer = -1;

    private TrailRenderer activeTrail;

    private bool blackTrailAllowed;
    private float blackTrailStopTimer;

    private bool hasModuleVisualPreview;
    private BallType moduleVisualPreviewType;

    private void Awake()
    {
        defaultLayer = LayerMask.NameToLayer(defaultLayerName);
        blackBallLayer = LayerMask.NameToLayer(cleanGameplayLayerName);

        if (visualRenderer == null)
        {
            Transform visual = transform.Find("Visual");

            if (visual != null)
                visualRenderer = visual.GetComponent<Renderer>();
        }

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (!initialized)
            ApplyVisuals(type);

        transform.localScale = scale;

        ApplyLayerForType(type);
        UpdateTrails();
        UpdateBlackFX();
        UpdateBlackThreatRegistration();
    }

    private void Update()
    {
        UpdateTrailEmission();
    }

    public void Initialize(BallType newType, int newPoints)
    {
        UnregisterBlackThreatIfNeeded();

        type = newType;
        points = newPoints;
        initialized = true;

        transform.localScale = scale;

        blackTrailAllowed = false;
        blackTrailStopTimer = 0f;

        hasModuleVisualPreview = false;
        moduleVisualPreviewType = type;

        ApplyVisuals(type);
        ApplyLayerForType(type);
        UpdateTrails();
        UpdateBlackFX();
        UpdateBlackThreatRegistration();
    }

    public void SetModuleVisualPreview(BallType? previewType)
    {
        if (previewType.HasValue)
        {
            hasModuleVisualPreview = true;
            moduleVisualPreviewType = previewType.Value;

            ApplyVisuals(moduleVisualPreviewType);
            ApplyPreviewTrail(moduleVisualPreviewType);
            return;
        }

        hasModuleVisualPreview = false;
        moduleVisualPreviewType = type;

        ApplyVisuals(type);
        UpdateTrails();
    }

    private void ApplyPreviewTrail(BallType previewType)
    {
        SetTrail(trailWhite, previewType == BallType.White);
        SetTrail(trailBlue, previewType == BallType.Blue);
        SetTrail(trailRed, previewType == BallType.Red);
        SetTrail(trailBlack, previewType == BallType.Black);

        switch (previewType)
        {
            case BallType.White:
                activeTrail = trailWhite;
                break;

            case BallType.Blue:
                activeTrail = trailBlue;
                break;

            case BallType.Red:
                activeTrail = trailRed;
                break;

            case BallType.Black:
                activeTrail = trailBlack;
                break;
        }
    }

    private void ApplyVisuals(BallType t)
    {
        if (visualRenderer == null)
            return;

        Material targetMaterial = null;
        string requestedId = t.ToString().ToLower();

        if (definition != null && definition.Id == requestedId)
        {
            targetMaterial = definition.Material;
        }
        else
        {
            switch (t)
            {
                case BallType.White:
                    targetMaterial = whiteMaterial;
                    break;
                case BallType.Blue:
                    targetMaterial = blueMaterial;
                    break;
                case BallType.Red:
                    targetMaterial = redMaterial;
                    break;
                case BallType.Black:
                    targetMaterial = blackMaterial;
                    break;
            }
        }

        if (targetMaterial != null)
            visualRenderer.material = targetMaterial;
    }

    private void ApplyLayerForType(BallType t)
    {
        if (defaultLayer < 0 || blackBallLayer < 0)
            return;

        int visualLayer =
            t == BallType.Black
                ? blackBallLayer
                : defaultLayer;

        gameObject.layer = defaultLayer;

        if (visualRenderer != null)
            visualRenderer.gameObject.layer = visualLayer;

        SetTrailLayer(trailWhite, defaultLayer);
        SetTrailLayer(trailBlue, defaultLayer);
        SetTrailLayer(trailRed, defaultLayer);
        SetTrailLayer(trailBlack, visualLayer);
    }

    private void SetTrailLayer(TrailRenderer trail, int layer)
    {
        if (trail == null)
            return;

        trail.gameObject.layer = layer;
    }

    private void UpdateTrails()
    {
        SetTrail(trailWhite, false);
        SetTrail(trailBlue, false);
        SetTrail(trailRed, false);
        SetTrail(trailBlack, false);

        activeTrail = null;

        switch (type)
        {
            case BallType.White:
                activeTrail = trailWhite;
                break;

            case BallType.Blue:
                activeTrail = trailBlue;
                break;

            case BallType.Red:
                activeTrail = trailRed;
                break;

            case BallType.Black:
                activeTrail = trailBlack;
                break;
        }

        if (activeTrail != null)
        {
            activeTrail.Clear();
            activeTrail.emitting = type != BallType.Black;
        }
    }

    private void UpdateTrailEmission()
    {
        if (activeTrail == null)
            return;

        if (hasModuleVisualPreview)
        {
            activeTrail.emitting = true;
            return;
        }

        if (type != BallType.Black)
        {
            activeTrail.emitting = true;
            return;
        }

        if (rb == null)
        {
            activeTrail.emitting = false;
            return;
        }

        float speed = rb.linearVelocity.magnitude;

        if (speed >= blackTrailStartSpeed)
        {
            blackTrailAllowed = true;
            blackTrailStopTimer = 0f;
        }
        else if (speed <= blackTrailStopSpeed)
        {
            blackTrailStopTimer += Time.deltaTime;

            if (blackTrailStopTimer >= blackTrailStopDelay)
                blackTrailAllowed = false;
        }
        else
        {
            blackTrailStopTimer = 0f;
        }

        activeTrail.emitting = blackTrailAllowed;
    }

    private void SetTrail(TrailRenderer tr, bool emitting)
    {
        if (tr == null)
            return;

        tr.emitting = emitting;

        if (!emitting)
            tr.Clear();
    }

    private void UpdateBlackThreatRegistration()
    {
        bool shouldBeRegistered =
            gameObject.activeInHierarchy &&
            type == BallType.Black;

        if (shouldBeRegistered && !registeredAsBlackThreat)
        {
            if (BlackThreatTracker.Instance != null)
            {
                BlackThreatTracker.Instance.RegisterBlackBall();
                registeredAsBlackThreat = true;
            }

            return;
        }

        if (!shouldBeRegistered && registeredAsBlackThreat)
            UnregisterBlackThreatIfNeeded();
    }

    private void UnregisterBlackThreatIfNeeded()
    {
        if (!registeredAsBlackThreat)
            return;

        if (BlackThreatTracker.Instance != null)
            BlackThreatTracker.Instance.UnregisterBlackBall();

        registeredAsBlackThreat = false;
    }

    private void UpdateBlackFX()
    {
        bool shouldPlay = type == BallType.Black;

        UpdateParticleFx(blackCrackleFX, shouldPlay);
    }

    private void UpdateParticleFx(ParticleSystem ps, bool shouldPlay)
    {
        if (ps == null)
            return;

        if (shouldPlay)
        {
            if (!ps.isPlaying)
                ps.Play(true);
        }
        else
        {
            ps.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    public void SetDefinition(BallDefinition newDefinition)
    {
        definition = newDefinition;

        if (definition != null)
            points = definition.BasePoints;

        if (hasModuleVisualPreview)
            ApplyVisuals(moduleVisualPreviewType);
        else
            ApplyVisuals(type);

        ApplyLayerForType(type);
        UpdateTrails();
        UpdateBlackFX();
        UpdateBlackThreatRegistration();
    }

    public bool TryGetDefinitionForType(
    BallType targetType,
    out BallDefinition result)
    {
        result = null;

        if (definition == null)
            return false;

        string targetId = targetType.ToString().ToLower();

        if (definition.Id == targetId)
        {
            result = definition;
            return true;
        }

        return false;
    }

    public string BallId =>
    definition != null ? definition.Id : type.ToString();

    public Color ScoreColor =>
        definition != null ? definition.ScoreColor : Color.white;

    public bool IsDanger =>
        definition != null
            ? definition.IsDanger
            : type == BallType.Black;

    private void OnDisable()
    {
        hasModuleVisualPreview = false;
        UnregisterBlackThreatIfNeeded();
    }

    private void OnDestroy()
    {
        hasModuleVisualPreview = false;
        UnregisterBlackThreatIfNeeded();
    }
}