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

    private bool initialized;

    [Header("Référence visuelle")]
    [SerializeField] private Renderer visualRenderer;

    [Header("Matériaux par type")]
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material blueMaterial;
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material blackMaterial;

    [Header("Trails par type (réglage dans l'Inspector)")]
    [SerializeField] private TrailRenderer trailWhite;
    [SerializeField] private TrailRenderer trailBlue;
    [SerializeField] private TrailRenderer trailRed;
    [SerializeField] private TrailRenderer trailBlack;

    public string TypeName => type.ToString();

    private void Awake()
    {
        if (visualRenderer == null)
        {
            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                visualRenderer = visual.GetComponent<Renderer>();
            }
        }
    }

    public void Initialize(BallType newType, int newPoints)
    {
        type = newType;
        points = newPoints;
        initialized = true;

        transform.localScale = scale;

        ApplyVisuals(type);
        UpdateTrails();
    }

    private void Start()
    {
        if (!initialized)
        {
            ApplyVisuals(type);
        }

        transform.localScale = scale;
        UpdateTrails();
    }

    private void ApplyVisuals(BallType t)
    {
        if (visualRenderer == null)
            return;

        Material targetMaterial = null;

        switch (t)
        {
            case BallType.White: targetMaterial = whiteMaterial; break;
            case BallType.Blue: targetMaterial = blueMaterial; break;
            case BallType.Red: targetMaterial = redMaterial; break;
            case BallType.Black: targetMaterial = blackMaterial; break;
        }

        if (targetMaterial != null)
        {
            visualRenderer.material = targetMaterial;
        }
    }

    /// <summary>
    /// Active uniquement le TrailRenderer correspondant au type de bille.
    /// Tous les réglages (couleur, time, width...) se font dans l'inspector.
    /// </summary>
    private void UpdateTrails()
    {
        // On coupe tout
        SetTrail(trailWhite, false);
        SetTrail(trailBlue, false);
        SetTrail(trailRed, false);
        SetTrail(trailBlack, false);

        TrailRenderer activeTrail = null;

        switch (type)
        {
            case BallType.White: activeTrail = trailWhite; break;
            case BallType.Blue: activeTrail = trailBlue; break;
            case BallType.Red: activeTrail = trailRed; break;
            case BallType.Black: activeTrail = trailBlack; break;
        }

        if (activeTrail != null)
        {
            activeTrail.Clear();
            activeTrail.emitting = true;
        }
    }

    private void SetTrail(TrailRenderer tr, bool emitting)
    {
        if (tr == null) return;

        tr.emitting = emitting;
        if (!emitting)
        {
            tr.Clear();
        }
    }
}
