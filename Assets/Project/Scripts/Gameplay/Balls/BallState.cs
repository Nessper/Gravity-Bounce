using UnityEngine;

public class BallState : MonoBehaviour
{
    [SerializeField] private Vector3 scale = Vector3.one;
    public Vector3 Scale => scale;

    [Header("Definition")]
    [SerializeField] private BallDefinition definition;
    public BallDefinition Definition => definition;

    [Header("Runtime Score")]
    public int points = 0;

    [Header("Runtime State")]
    public bool inBin = false;
    public bool collected = false;
    public Side currentSide = Side.None;

    [Header("Tutorial")]
    public bool isTutorialBall = false;

    [Header("Visual")]
    [SerializeField] private Renderer visualRenderer;

    [Header("Physics")]
    [SerializeField] private Rigidbody rb;

    [Header("Trails by BallId")]
    [SerializeField] private TrailRenderer trailWhite;
    [SerializeField] private TrailRenderer trailBlue;
    [SerializeField] private TrailRenderer trailRed;
    [SerializeField] private TrailRenderer trailBlack;

    [Header("Danger Trail Speed Filter")]
    [SerializeField] private float dangerTrailStartSpeed = 4f;
    [SerializeField] private float dangerTrailStopSpeed = 2f;
    [SerializeField] private float dangerTrailStopDelay = 0.18f;

    [Header("Danger FX")]
    [SerializeField] private ParticleSystem dangerCrackleFX;

    [Header("Visual Layers")]
    [SerializeField] private string defaultLayerName = "Gameplay";
    [SerializeField] private string dangerLayerName = "CleanGameplay";

    public string BallId =>
        definition != null && !string.IsNullOrWhiteSpace(definition.Id)
            ? definition.Id
            : "unknown";

    public string TypeName => BallId;

    public Color ScoreColor =>
        definition != null ? definition.ScoreColor : Color.white;

    public bool IsDanger =>
        definition != null && definition.IsDanger;

    public bool CountsForProgress =>
        definition != null && definition.CountsForProgress;

    private bool initialized;
    private bool registeredAsDangerThreat;

    private int defaultLayer = -1;
    private int dangerLayer = -1;

    private TrailRenderer activeTrail;

    private bool dangerTrailAllowed;
    private float dangerTrailStopTimer;

    private BallDefinition visualPreviewDefinition;

    private bool HasVisualPreview =>
        visualPreviewDefinition != null;

    private BallDefinition CurrentVisualDefinition =>
        HasVisualPreview ? visualPreviewDefinition : definition;

    private void Awake()
    {
        defaultLayer = LayerMask.NameToLayer(defaultLayerName);
        dangerLayer = LayerMask.NameToLayer(dangerLayerName);

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
            RefreshAllVisualState();

        transform.localScale = scale;
    }

    private void Update()
    {
        UpdateTrailEmission();
    }

    public void Initialize(BallDefinition newDefinition)
    {
        UnregisterDangerThreatIfNeeded();

        definition = newDefinition;
        visualPreviewDefinition = null;
        initialized = true;

        points = definition != null ? definition.BasePoints : 0;

        transform.localScale = scale;

        dangerTrailAllowed = false;
        dangerTrailStopTimer = 0f;

        RefreshAllVisualState();
    }

    public void SetDefinition(BallDefinition newDefinition)
    {
        Initialize(newDefinition);
    }

    public void SetModuleVisualPreview(BallDefinition previewDefinition)
    {
        visualPreviewDefinition = previewDefinition;

        ApplyCurrentVisuals();
        ApplyCurrentLayer();
        UpdateTrails();
        UpdateDangerFX();
        UpdateDangerThreatRegistration();
    }

    public void ClearModuleVisualPreview()
    {
        SetModuleVisualPreview(null);
    }

    private void RefreshAllVisualState()
    {
        ApplyCurrentVisuals();
        ApplyCurrentLayer();
        UpdateTrails();
        UpdateDangerFX();
        UpdateDangerThreatRegistration();
    }

    private void ApplyCurrentVisuals()
    {
        BallDefinition visualDefinition = CurrentVisualDefinition;

        if (visualDefinition == null || visualDefinition.Material == null)
            return;

        if (visualRenderer == null)
            return;

        visualRenderer.material = visualDefinition.Material;
    }

    private void ApplyCurrentLayer()
    {
        if (defaultLayer < 0 || dangerLayer < 0)
            return;

        int visualLayer = IsCurrentVisualDanger()
            ? dangerLayer
            : defaultLayer;

        gameObject.layer = defaultLayer;

        if (visualRenderer != null)
            visualRenderer.gameObject.layer = visualLayer;

        SetTrailLayer(trailWhite, defaultLayer);
        SetTrailLayer(trailBlue, defaultLayer);
        SetTrailLayer(trailRed, defaultLayer);
        SetTrailLayer(trailBlack, visualLayer);
    }

    private bool IsCurrentVisualDanger()
    {
        BallDefinition visualDefinition = CurrentVisualDefinition;
        return visualDefinition != null && visualDefinition.IsDanger;
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

        activeTrail = GetTrailForCurrentVisualBall();

        if (activeTrail == null)
            return;

        activeTrail.Clear();
        activeTrail.emitting = !IsCurrentVisualDanger();
    }

    private TrailRenderer GetTrailForCurrentVisualBall()
    {
        BallDefinition visualDefinition = CurrentVisualDefinition;
        string id =
            visualDefinition != null && !string.IsNullOrWhiteSpace(visualDefinition.Id)
                ? visualDefinition.Id
                : BallId;

        switch (id)
        {
            case "white":
                return trailWhite;

            case "blue":
                return trailBlue;

            case "red":
                return trailRed;

            case "black":
                return trailBlack;

            default:
                return trailWhite;
        }
    }

    private void UpdateTrailEmission()
    {
        if (activeTrail == null)
            return;

        if (HasVisualPreview)
        {
            activeTrail.emitting = true;
            return;
        }

        if (!IsDanger)
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

        if (speed >= dangerTrailStartSpeed)
        {
            dangerTrailAllowed = true;
            dangerTrailStopTimer = 0f;
        }
        else if (speed <= dangerTrailStopSpeed)
        {
            dangerTrailStopTimer += Time.deltaTime;

            if (dangerTrailStopTimer >= dangerTrailStopDelay)
                dangerTrailAllowed = false;
        }
        else
        {
            dangerTrailStopTimer = 0f;
        }

        activeTrail.emitting = dangerTrailAllowed;
    }

    private void SetTrail(TrailRenderer trail, bool emitting)
    {
        if (trail == null)
            return;

        trail.emitting = emitting;

        if (!emitting)
            trail.Clear();
    }

    private void UpdateDangerThreatRegistration()
    {
        bool shouldBeRegistered =
            gameObject.activeInHierarchy &&
            IsDanger;

        if (shouldBeRegistered && !registeredAsDangerThreat)
        {
            if (BlackThreatTracker.Instance != null)
            {
                BlackThreatTracker.Instance.RegisterBlackBall();
                registeredAsDangerThreat = true;
            }

            return;
        }

        if (!shouldBeRegistered && registeredAsDangerThreat)
            UnregisterDangerThreatIfNeeded();
    }

    private void UnregisterDangerThreatIfNeeded()
    {
        if (!registeredAsDangerThreat)
            return;

        if (BlackThreatTracker.Instance != null)
            BlackThreatTracker.Instance.UnregisterBlackBall();

        registeredAsDangerThreat = false;
    }

    private void UpdateDangerFX()
    {
        UpdateParticleFx(dangerCrackleFX, IsDanger);
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

    private void OnDisable()
    {
        visualPreviewDefinition = null;
        UnregisterDangerThreatIfNeeded();
    }

    private void OnDestroy()
    {
        visualPreviewDefinition = null;
        UnregisterDangerThreatIfNeeded();
    }
}