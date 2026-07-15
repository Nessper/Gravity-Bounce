using System;
using UnityEngine;

public class VoidTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner spawner;

    public event Action<BallState> OnTutorialBallLost;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        BallState state = other.GetComponent<BallState>();

        if (state == null ||
            state.collected ||
            state.IsTemporarilyExcludedFromGameplay)
            return;

        state.collected = true;

        if (state.isTutorialBall)
        {
            OnTutorialBallLost?.Invoke(state);

            if (spawner != null)
                spawner.DestroyTutorialBall(other.gameObject);
            else
                Destroy(other.gameObject);

            return;
        }

        scoreManager?.RegisterLost(state.BallId);

        if (spawner != null)
            spawner.Recycle(other.gameObject, collected: false);
        else
            Destroy(other.gameObject);
    }
}
