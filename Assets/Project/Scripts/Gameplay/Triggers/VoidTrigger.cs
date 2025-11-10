using UnityEngine;

public class VoidTrigger : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        var state = other.GetComponent<BallState>();
        if (state == null || state.collected) return;

        state.collected = true;
        scoreManager?.RegisterLost(state.TypeName);

        Destroy(other.gameObject);
    }
}
