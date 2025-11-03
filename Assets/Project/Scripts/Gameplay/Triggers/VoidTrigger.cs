using UnityEngine;

public class VoidTrigger : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager; // référence au ScoreManager

    private void OnTriggerEnter(Collider other)
    {
        // On ne traite que les objets taggés "Ball"
        if (!other.CompareTag("Ball")) return;

        var state = other.GetComponent<BallState>();
        if (state == null) return;

        // Si elle a déjà été collectée ou flushée, on ignore
        if (state.collected) return;

        // Marquer la bille comme "hors jeu"
        state.collected = true;

        //  Nouveau : enregistrer la perte dans le ScoreManager
        if (scoreManager != null)
        {
            scoreManager.RegisterLost(state);
            Debug.Log($"[VoidTrigger] Bille perdue : {state.type} ({state.points} pts)");
        }
        else
        {
            Debug.LogWarning("[VoidTrigger] Aucun ScoreManager assigné !");
        }

        // Détruire la bille (ou la renvoyer au pool plus tard)
        Destroy(other.gameObject);
    }
}
