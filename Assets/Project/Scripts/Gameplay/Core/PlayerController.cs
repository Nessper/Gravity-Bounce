using UnityEngine;

/// <summary>
/// Contrôle du paddle du joueur.
/// Ne lit AUCUN input directement :
/// applique une position cible en X fournie par un autre script.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Limites de déplacement en X")]
    [SerializeField] private float xRange = 1.7f;

    [Header("Etat du contrôle")]
    [SerializeField] private bool canControl = true;

    [Header("Feedback visuel")]
    [SerializeField] private PlayerFlashFeedback flashFeedback;

    private Rigidbody playerRb;
    private float targetX; // Position cible en X (définie par l'input externe)

    public float XRange
    {
        get { return xRange; }
    }

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();

        if (playerRb != null)
        {
            playerRb.isKinematic = true;
            playerRb.interpolation = RigidbodyInterpolation.None;
        }

        // On initialise la targetX à la position actuelle pour éviter un saut au démarrage
        targetX = transform.position.x;
    }

    private void Update()
    {
        if (!canControl)
            return;

        Vector3 currentPos = transform.position;
        Vector3 nextPos = new Vector3(targetX, currentPos.y, currentPos.z);
        transform.position = nextPos;
    }


    /// <summary>
    /// Définit la position cible en X, en coordonnées monde.
    /// Clampée dans [-xRange, xRange].
    /// </summary>
    public void SetTargetXWorld(float worldX)
    {
        float clamped = Mathf.Clamp(worldX, -xRange, xRange);
        targetX = clamped;
    }


    public void SetActiveControl(bool state)
    {
        canControl = state;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            if (flashFeedback != null)
            {
                flashFeedback.TriggerFlash();
            }
        }
    }
}
