using UnityEngine;

public class BallPhysicsTuning : MonoBehaviour
{
    [Header("Gravité de référence")]
    public Vector3 baseGravity = new Vector3(0f, -9.81f, 0f);

    private void Start()
    {
        float mult = 1.0f;

        if (PlatformTuning.Instance != null)
        {
            // S'assure que le profil est bien calculé
            PlatformTuning.Instance.RefreshProfile();
            mult = PlatformTuning.Instance.ActiveGravityMult;
        }

        Physics.gravity = baseGravity * mult;
    }
}
