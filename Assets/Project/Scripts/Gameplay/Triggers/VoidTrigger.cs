using UnityEngine;

public class VoidTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner spawner;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        var state = other.GetComponent<BallState>();
        if (state == null || state.collected) return;

        state.collected = true; // éviter double comptage
        scoreManager?.RegisterLost(state.TypeName);

        if (spawner != null)
        {
            spawner.Recycle(other.gameObject, collected: false);
        }
        else
        {
            // Fallback dev (au cas où la ref est manquante)
            Destroy(other.gameObject);
            Debug.LogWarning("[VoidTrigger] Spawner manquant, Destroy utilisé (fallback dev).");
        }
    }
}
