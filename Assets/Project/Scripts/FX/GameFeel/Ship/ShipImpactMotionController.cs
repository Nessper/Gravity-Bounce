using UnityEngine;

/// <summary>
/// Réaction inertielle du vaisseau aux impacts sur les murs.
/// 
/// Idée physique :
/// - impact sur mur gauche  => le vaisseau part légèrement à droite
/// - impact sur mur droit   => le vaisseau part légèrement à gauche
/// - plusieurs impacts du même côté se cumulent
/// - retour lent vers la position initiale pour garder une sensation d'inertie
///
/// À placer sur GameFeelRoot.
/// </summary>
public class ShipImpactMotionController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform motionRoot;

    [Header("Impact Motion")]
    [SerializeField] private float impactOffset = 0.025f;
    [SerializeField] private float maxOffset = 0.20f;
    [SerializeField] private float returnSpeed = 3f;

    [Header("Filtering")]
    [Range(0f, 1f)]
    [SerializeField] private float minVisibleStrength = 0.20f;

    private Vector3 baseLocalPosition;
    private float currentOffsetX;

    private void Awake()
    {
        if (motionRoot == null)
        {
            Debug.LogWarning("[ShipImpactMotionController] motionRoot non assigné.");
            enabled = false;
            return;
        }

        baseLocalPosition = motionRoot.localPosition;
    }

    private void OnEnable()
    {
        WallImpactDetector.OnWallImpact += HandleWallImpact;
    }

    private void OnDisable()
    {
        WallImpactDetector.OnWallImpact -= HandleWallImpact;
    }

    private void Update()
    {
        currentOffsetX = Mathf.Lerp(
            currentOffsetX,
            0f,
            returnSpeed * Time.deltaTime
        );

        Vector3 pos = baseLocalPosition;
        pos.x += currentOffsetX;
        motionRoot.localPosition = pos;
    }

    private void HandleWallImpact(WallImpactDetector.WallSide side, float strength01)
    {
        if (strength01 < minVisibleStrength)
            return;

        // Impact gauche => reaction vers la droite.
        // Impact droite => reaction vers la gauche.
        float direction = side == WallImpactDetector.WallSide.Left ? 1f : -1f;

        float offset = impactOffset * strength01;

        currentOffsetX += direction * offset;
        currentOffsetX = Mathf.Clamp(currentOffsetX, -maxOffset, maxOffset);
    }
}