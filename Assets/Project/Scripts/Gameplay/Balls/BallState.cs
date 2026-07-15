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

    [SerializeField] private DroneBallExclusionState droneExclusionState;

    public DroneBallExclusionState DroneExclusionState =>
        droneExclusionState;
    public bool IsTemporarilyExcludedFromGameplay =>
        droneExclusionState != DroneBallExclusionState.None;

    [Header("Tutorial")]
    public bool isTutorialBall = false;

    [Header("Visual")]
    [SerializeField] private Renderer visualRenderer;

    [Header("Destruction FX")]
    [SerializeField] private BallDestructionEffects destructionEffects;

    [Header("Physics")]
    [SerializeField] private Rigidbody rb;

    private Collider ballCollider;
    private Object droneExclusionOwner;
    private bool colliderWasEnabledBeforeDroneReservation;
    private bool rigidbodyWasKinematicBeforeDroneReservation;

    public Vector3 LinearVelocity =>
        rb != null ? rb.linearVelocity : Vector3.zero;

    public void ClearTrailHistory()
    {
        trailWhite?.Clear();
        trailBlue?.Clear();
        trailRed?.Clear();
        trailBlack?.Clear();
    }

    public void SetDroneTeleportVisualVisible(bool isVisible)
    {
        droneTeleportVisualHidden = !isVisible;

        if (visualRenderer != null)
            visualRenderer.enabled = isVisible;

        if (isVisible)
        {
            RefreshAllVisualState();
            return;
        }

        SetTrail(trailWhite, false);
        SetTrail(trailBlue, false);
        SetTrail(trailRed, false);
        SetTrail(trailBlack, false);
        UpdateParticleFx(dangerCrackleFX, false);
    }

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

    public bool IsVisualDanger =>
        IsCurrentVisualDanger();

    public bool CountsForProgress =>
        definition != null && definition.CountsForProgress;

    private bool initialized;
    private bool registeredAsDangerThreat;

    private int defaultLayer = -1;
    private int dangerLayer = -1;

    private TrailRenderer activeTrail;
    private bool droneTeleportVisualHidden;

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

        if (destructionEffects == null)
            destructionEffects = GetComponentInChildren<BallDestructionEffects>(true);

        ballCollider = GetComponent<Collider>();
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
        droneTeleportVisualHidden = false;

        if (visualRenderer != null)
            visualRenderer.enabled = true;

        RefreshAllVisualState();
    }

    public void SetDefinition(BallDefinition newDefinition)
    {
        Initialize(newDefinition);
    }

    public bool TryReserveForDrone(Object owner)
    {
        if (owner == null ||
            collected ||
            inBin ||
            IsTemporarilyExcludedFromGameplay ||
            !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (ballCollider == null)
            ballCollider = GetComponent<Collider>();

        colliderWasEnabledBeforeDroneReservation =
            ballCollider != null && ballCollider.enabled;
        rigidbodyWasKinematicBeforeDroneReservation =
            rb != null && rb.isKinematic;

        droneExclusionOwner = owner;
        droneExclusionState = DroneBallExclusionState.Reserved;

        if (ballCollider != null)
            ballCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        return true;
    }

    public bool TryMarkCapturedByDrone(Object owner)
    {
        if (!IsOwnedByDrone(owner) ||
            droneExclusionState != DroneBallExclusionState.Reserved)
        {
            return false;
        }

        droneExclusionState = DroneBallExclusionState.Captured;
        return true;
    }

    public bool TryReleaseFromDrone(Object owner, Vector3 launchVelocity)
    {
        if (!IsOwnedByDrone(owner))
            return false;

        RestoreAfterDroneExclusion(launchVelocity);
        return true;
    }

    public bool TryCancelDroneExclusion(Object owner)
    {
        if (!IsOwnedByDrone(owner))
            return false;

        RestoreAfterDroneExclusion(Vector3.zero);
        return true;
    }

    public void ForceReleaseDroneExclusion()
    {
        if (!IsTemporarilyExcludedFromGameplay)
            return;

        RestoreAfterDroneExclusion(Vector3.zero);
    }

    public void ResetDroneExclusionState()
    {
        droneExclusionState = DroneBallExclusionState.None;
        droneExclusionOwner = null;
        colliderWasEnabledBeforeDroneReservation = false;
        rigidbodyWasKinematicBeforeDroneReservation = false;
    }

    public bool IsOwnedByDrone(Object owner)
    {
        return owner != null &&
               droneExclusionOwner == owner &&
               IsTemporarilyExcludedFromGameplay;
    }

    private void RestoreAfterDroneExclusion(Vector3 launchVelocity)
    {
        bool restoreCollider = colliderWasEnabledBeforeDroneReservation;
        bool restoreKinematic = rigidbodyWasKinematicBeforeDroneReservation;

        ResetDroneExclusionState();

        if (rb != null)
        {
            rb.isKinematic = restoreKinematic;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = restoreKinematic
                ? Vector3.zero
                : launchVelocity;
        }

        if (ballCollider != null)
            ballCollider.enabled = restoreCollider;
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

    public void PlayDestructionEffect(Vector3 worldPosition)
    {
        if (destructionEffects == null)
            return;

        BallDefinition visualDefinition = CurrentVisualDefinition;
        string visualBallId =
            visualDefinition != null &&
            !string.IsNullOrWhiteSpace(visualDefinition.Id)
                ? visualDefinition.Id
                : BallId;

        destructionEffects.Play(visualBallId, worldPosition);
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

        if (droneTeleportVisualHidden)
        {
            activeTrail.emitting = false;
            return;
        }

        if (HasVisualPreview)
        {
            activeTrail.emitting = true;
            return;
        }

        if (!IsCurrentVisualDanger())
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
            IsCurrentVisualDanger();

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
        UpdateParticleFx(dangerCrackleFX, IsCurrentVisualDanger());
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
