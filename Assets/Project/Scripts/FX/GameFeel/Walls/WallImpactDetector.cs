using System;
using UnityEngine;

/// <summary>
/// Détecte les impacts de billes sur un mur gauche/droite.
/// Responsabilités :
/// - déclencher le pulse visuel local du mur
/// - émettre un event global indiquant quel mur a été touché
/// - transmettre une intensité d'impact normalisée entre 0 et 1
///
/// À placer sur Wall_Left / Wall_Right, le GameObject qui porte le collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallImpactDetector : MonoBehaviour
{
    public enum WallSide
    {
        Left,
        Right
    }

    public static event Action<WallSide, float> OnWallImpact;

    [Header("Wall")]
    [SerializeField] private WallSide side = WallSide.Left;

    [Header("Impact Strength")]
    [SerializeField] private float minImpactSpeed = 1.5f;
    [SerializeField] private float maxImpactSpeed = 8f;

    [Tooltip("Intensité utilisée si le mur est en trigger et qu'on n'a pas de relativeVelocity.")]
    [Range(0f, 1f)]
    [SerializeField] private float triggerFallbackStrength = 0.4f;

    private EnergyWallFX pulse;

    private void Awake()
    {
        pulse = GetComponentInChildren<EnergyWallFX>();

        if (pulse == null)
            Debug.LogWarning($"[WallImpactDetector] Aucun EnergyWallFX trouvé sur {name}.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Ball"))
            return;

        float rawSpeed = collision.relativeVelocity.magnitude;
        float strength01 = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, rawSpeed);

        TriggerImpact(strength01);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        TriggerImpact(triggerFallbackStrength);
    }

    private void TriggerImpact(float strength01)
    {
        strength01 = Mathf.Clamp01(strength01);

        if (pulse != null)
            pulse.TriggerPulse();

        OnWallImpact?.Invoke(side, strength01);
    }
}