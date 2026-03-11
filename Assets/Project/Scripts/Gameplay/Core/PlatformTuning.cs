using UnityEngine;

public class PlatformTuning : MonoBehaviour
{
    public static PlatformTuning Instance { get; private set; }

    [Header("Gravité (multiplicateur)")]
    public float desktopGravityMult = 1.0f;
    public float mobileGravityMult = 0.9f;

    private float _activeGravityMult = 1.0f;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // NE PAS appeler ResolveGravityProfile ici
    }

    private void Start()
    {
        ResolveGravityProfile();
    }

    private void ResolveGravityProfile()
    {
        bool isMobile = false;

        if (RunConfig.Instance != null)
            isMobile = RunConfig.Instance.IsMobileProfile;

        _activeGravityMult = isMobile ? mobileGravityMult : desktopGravityMult;
    }

    public float ActiveGravityMult => _activeGravityMult;

    public void RefreshProfile()
    {
        ResolveGravityProfile();
    }
}
