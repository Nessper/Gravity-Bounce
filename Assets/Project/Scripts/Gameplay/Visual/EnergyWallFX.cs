using UnityEngine;

/// <summary>
/// Gère l'effet visuel complet du mur :
/// - "respiration" idle (léger pulse de lumière)
/// - pulse d'impact (éclair + épaississement temporaire)
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EnergyWallFX : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Renderer du quad visuel du mur. Si vide : prend celui sur ce GameObject.")]
    [SerializeField] private Renderer wallRenderer;

    [Tooltip("Transform à scaler sur l'impact. Si vide : ce transform.")]
    [SerializeField] private Transform wallVisual;

    [Header("Idle (respiration permanente)")]
    [SerializeField] private Color baseColor = Color.cyan;
    [SerializeField] private float idleAmplitude = 0.15f;  // 0.1–0.2
    [SerializeField] private float idleSpeed = 1.0f;       // vitesse de respiration

    [Header("Impact (pulse court et fort)")]
    [SerializeField] private float impactScaleMultiplier = 0.6f; // +60% de largeur au max
    [SerializeField] private float impactColorMultiplier = 1.0f; // +100% d'intensité couleur au max
    [SerializeField] private float impactDecaySpeed = 4.0f;      // plus grand = retour plus rapide

    private MaterialPropertyBlock block;
    private Vector3 baseScale;
    private float impactAmount; // 0 -> pas d'impact, 1 -> impact max

    private void Awake()
    {
        if (wallRenderer == null)
            wallRenderer = GetComponent<Renderer>();

        if (wallVisual == null)
            wallVisual = transform;

        baseScale = wallVisual.localScale;
        block = new MaterialPropertyBlock();

        // Essaie de récupérer la couleur actuelle comme base si tu ne l'as pas mise dans l'inspector
        if (baseColor == default(Color) && wallRenderer.sharedMaterial != null)
        {
            if (wallRenderer.sharedMaterial.HasProperty("_BaseColor"))
                baseColor = wallRenderer.sharedMaterial.GetColor("_BaseColor");
            else if (wallRenderer.sharedMaterial.HasProperty("_Color"))
                baseColor = wallRenderer.sharedMaterial.GetColor("_Color");
        }
    }

    private void Update()
    {
        float t = Time.time;

        // 1) Respiration idle (facteur entre 1 - A et 1 + A)
        float idleFactor = 1f + Mathf.Sin(t * idleSpeed) * idleAmplitude;

        // 2) Décroissance de l'impact vers 0
        if (impactAmount > 0f)
        {
            impactAmount = Mathf.MoveTowards(
                impactAmount,
                0f,
                impactDecaySpeed * Time.deltaTime
            );
        }

        // 3) Facteurs d'impact
        float impactColorFactor = 1f + impactColorMultiplier * impactAmount;
        float impactScaleFactor = 1f + impactScaleMultiplier * impactAmount;

        // 4) Couleur finale (idle + impact)
        Color finalColor = baseColor * idleFactor * impactColorFactor;

        // Selon ton shader, essaie _BaseColor, sinon _Color
        block.SetColor("_BaseColor", finalColor);
        // Si ça ne marche pas, commente la ligne au-dessus et décommente celle-ci :
        // block.SetColor("_Color", finalColor);

        wallRenderer.SetPropertyBlock(block);

        // 5) Scale final (larger sur impact, uniquement en X)
        Vector3 s = baseScale;
        s.x *= impactScaleFactor;
        wallVisual.localScale = s;
    }

    /// <summary>
    /// A appeler quand une bille frappe le mur.
    /// </summary>
    public void TriggerPulse()
    {
        impactAmount = 1f;
    }
}
