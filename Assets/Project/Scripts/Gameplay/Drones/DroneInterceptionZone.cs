using System;
using UnityEngine;

/// <summary>
/// Zone evenementielle placee apres la derniere possibilite de collecte,
/// mais avant le Void. Elle ne decide pas quel drone peut sauver la bille.
/// </summary>
public sealed class DroneInterceptionZone : MonoBehaviour
{
    public event Action<BallState> OnCandidateEntered;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        BallState state = other.GetComponent<BallState>();

        if (state == null ||
            state.collected ||
            state.inBin ||
            state.isTutorialBall ||
            state.IsTemporarilyExcludedFromGameplay ||
            state.LinearVelocity.y >= 0f)
        {
            return;
        }

        OnCandidateEntered?.Invoke(state);
    }
}
