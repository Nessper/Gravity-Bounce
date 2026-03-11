using UnityEngine;

/// <summary>
/// Contrôle du paddle du joueur.
/// Ne lit AUCUN input directement : applique une position cible en X fournie par un autre script.
/// Gère aussi le feedback visuel au contact d'une bille.
/// Le SFX d'impact est externalisé via ImpactSfxEmitter (réutilisable).
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
    private float targetX;

    public float XRange => xRange;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();

        if (playerRb != null)
        {
            playerRb.isKinematic = true;
            playerRb.interpolation = RigidbodyInterpolation.None;
        }

        // Initialise targetX à la position actuelle pour éviter un saut au démarrage.
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
        targetX = Mathf.Clamp(worldX, -xRange, xRange);
    }

    /// <summary>
    /// Active / désactive le contrôle du paddle.
    /// </summary>
    public void SetActiveControl(bool state)
    {
        canControl = state;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Le SFX est géré par ImpactSfxEmitter (sur le même objet).
        // Ici on ne gère que le feedback visuel.
        if (!collision.collider.CompareTag("Ball"))
            return;

        if (flashFeedback != null)
            flashFeedback.TriggerFlash();
    }
}
