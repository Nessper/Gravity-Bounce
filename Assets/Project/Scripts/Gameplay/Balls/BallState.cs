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
    [SerializeField] private string blackBallLayerName = "BlackBall";

    public string TypeName => type.ToString();

    private bool initialized;
    private bool registeredAsBlackThreat;

    private int defaultLayer = -1;
    private int blackBallLayer = -1;

    private void Awake()
    {
        defaultLayer = LayerMask.NameToLayer(defaultLayerName);
        blackBallLayer = LayerMask.NameToLayer(blackBallLayerName);

        if (visualRenderer == null)
        {
            Transform visual = transform.Find("Visual");
            if (visual != null)
                visualRenderer = visual.GetComponent<Renderer>();
        }
    }

    private void Start()
    {
        if (!initialized)
            ApplyVisuals(type);

        transform.localScale = scale;

        ApplyLayerForType(type);
        UpdateTrails();
        UpdateBlackThreatRegistration();
    }

    public void Initialize(BallType newType, int newPoints)
    {
        UnregisterBlackThreatIfNeeded();

        type = newType;
        points = newPoints;
        initialized = true;

        transform.localScale = scale;

        ApplyVisuals(type);
        ApplyLayerForType(type);
        UpdateTrails();
        UpdateBlackFX();
        UpdateBlackThreatRegistration();
    }

    private void ApplyVisuals(BallType t)
    {
        if (visualRenderer == null)
            return;

        Material targetMaterial = null;

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

        if (targetMaterial != null)
            visualRenderer.material = targetMaterial;
    }

    private void ApplyLayerForType(BallType t)
    {
        if (defaultLayer < 0 || blackBallLayer < 0)
            return;

        int visualLayer = t == BallType.Black ? blackBallLayer : defaultLayer;

        // Le root reste en Gameplay pour ne pas casser collisions / physique.
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

        TrailRenderer activeTrail = null;

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
            activeTrail.emitting = true;
        }
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
        if (blackCrackleFX == null)
            return;

        if (type == BallType.Black)
        {
            if (!blackCrackleFX.isPlaying)
                blackCrackleFX.Play(true);
        }
        else
        {
            blackCrackleFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDisable()
    {
        UnregisterBlackThreatIfNeeded();
    }

    private void OnDestroy()
    {
        UnregisterBlackThreatIfNeeded();
    }
}