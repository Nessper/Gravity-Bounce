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

    [Header("État de jeu")]
    public bool inBin = false;
    public bool collected = false;
    public Side currentSide = Side.None;

    private bool initialized;

    // --- AJOUT UTILE : nom de type lisible pour ScoreManager / ComboEngine ---
    public string TypeName => type.ToString();

    /// <summary>
    /// Initialise la bille avec les données venant du JSON (via le spawner).
    /// </summary>
    public void Initialize(BallType newType, int newPoints)
    {
        type = newType;
        points = newPoints;
        initialized = true;
        ApplyVisuals(type);
    }

    private void Start()
    {
        if (!initialized)
        {
            ApplyVisuals(type);
        }
    }

    private void ApplyVisuals(BallType t)
    {
        var r = GetComponent<Renderer>();
        if (!r) return;

        // Instancier un matériel unique pour éviter de modifier le sharedMaterial
        if (r.sharedMaterial != null && (r.material == null || r.material == r.sharedMaterial))
            r.material = new Material(r.sharedMaterial);

        var mat = r.material;
        switch (t)
        {
            case BallType.White: mat.color = Color.white; break;
            case BallType.Blue: mat.color = Color.blue; break;
            case BallType.Red: mat.color = Color.red; break;
            case BallType.Black: mat.color = Color.black; break;
        }
    }
}
