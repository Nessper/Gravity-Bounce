using UnityEngine;

/// <summary>
/// Contrôle physique du paddle du joueur.
/// Ce script ne lit AUCUN input directement :
/// il se contente d'appliquer une position cible en X,
/// fournie par un autre script d'input (souris, touch, etc.).
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

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();

        // On initialise la targetX à la position actuelle pour éviter un saut au démarrage
        targetX = transform.position.x;
    }

    private void FixedUpdate()
    {
        if (!canControl)
            return;

        Vector3 currentPos = playerRb.position;

        // Déplacement direct vers la targetX (tu peux lisser plus tard si besoin)
        Vector3 nextPos = new Vector3(targetX, currentPos.y, currentPos.z);
        playerRb.MovePosition(nextPos);
    }

    /// <summary>
    /// Définit la position cible en X, en coordonnées monde.
    /// Le clamp est fait ici pour garantir le respect de xRange,
    /// quel que soit l'input utilisé.
    /// </summary>
    public void SetTargetXWorld(float worldX)
    {
        targetX = Mathf.Clamp(worldX, -xRange, xRange);
    }

    /// <summary>
    /// Active ou désactive le contrôle du paddle (pause, fin de niveau, etc.).
    /// </summary>
    public void SetActiveControl(bool state)
    {
        canControl = state;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Adapté à ton setup (tag "Ball" par exemple)
        if (collision.collider.CompareTag("Ball"))
        {
            if (flashFeedback != null)
            {
                flashFeedback.TriggerFlash();
            }
        }
    }
}
