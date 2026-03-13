using System;
using UnityEngine;

/// <summary>
/// Trigger de perte des billes.
/// 
/// Regles :
/// - Bille normale -> enregistre la perte + recycle via le spawner
/// - Bille de tuto -> emet un event puis detruit la bille isolee
/// </summary>
public class VoidTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner spawner;

    // Event reserve au tuto pour savoir quand une bille de tuto atteint le void
    public event Action<BallState> OnTutorialBallLost;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        BallState state = other.GetComponent<BallState>();
        if (state == null || state.collected)
            return;

        // Protection contre les doubles traitements
        state.collected = true;

        // Cas tuto : aucune interaction avec le pipeline gameplay normal
        if (state.isTutorialBall)
        {
            OnTutorialBallLost?.Invoke(state);

            if (spawner != null)
            {
                spawner.DestroyTutorialBall(other.gameObject);
            }
            else
            {
                Destroy(other.gameObject);
                Debug.LogWarning("[VoidTrigger] Spawner manquant, Destroy utilise (fallback dev).");
            }

            return;
        }

        // Cas gameplay normal
        scoreManager?.RegisterLost(state.TypeName);

        if (spawner != null)
        {
            spawner.Recycle(other.gameObject, state.type, collected: false);
        }
        else
        {
            Destroy(other.gameObject);
            Debug.LogWarning("[VoidTrigger] Spawner manquant, Destroy utilise (fallback dev).");
        }
    }
}