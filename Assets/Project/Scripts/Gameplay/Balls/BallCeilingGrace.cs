using UnityEngine;

public class BallCeilingGrace : MonoBehaviour
{
    [Header("Refs")]
    public Collider ballCollider;

    [Header("Config")]
    public float epsilonBelowCeiling = 0.05f; // marge sous le plafond

    private Collider ceilingCollider;
    private bool graceActive;

    public void SetCeiling(Collider ceiling)
    {
        ceilingCollider = ceiling;
    }

    public void StartGrace()
    {
        if (ballCollider == null || ceilingCollider == null)
            return;

        // On démarre en ignorant le plafond
        Physics.IgnoreCollision(ballCollider, ceilingCollider, true);
        graceActive = true;
    }

    private void Update()
    {
        if (!graceActive || ceilingCollider == null || ballCollider == null)
            return;

        // Y du bord inférieur de la zone de "non-ignorance"
        float limitY = ceilingCollider.bounds.max.y - epsilonBelowCeiling;

        // Dès que la balle est bien passée sous le plafond, on réactive la collision
        if (transform.position.y < limitY)
        {
            Physics.IgnoreCollision(ballCollider, ceilingCollider, false);
            graceActive = false;
        }
    }
}
