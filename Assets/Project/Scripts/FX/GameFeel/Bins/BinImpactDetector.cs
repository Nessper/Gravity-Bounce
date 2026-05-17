using System;
using UnityEngine;

/// <summary>
/// Détecte les impacts de billes sur les colliders physiques d'un bin.
/// 
/// Responsabilités :
/// - identifier le côté du bin
/// - identifier le type d'impact
/// - calculer une force normalisée entre 0 et 1
/// - émettre un event global pour le game feel
///
/// À placer sur les colliders du bin :
/// - close wall
/// - collider intérieur
/// - autre zone physique du bin si besoin plus tard
/// </summary>
[RequireComponent(typeof(Collider))]
public class BinImpactDetector : MonoBehaviour
{
    public enum ImpactKind
    {
        CloseWall,
        InnerWall
    }

    public static event Action<Side, ImpactKind, float> OnBinImpact;

    [Header("Identity")]
    [SerializeField] private Side side = Side.Left;
    [SerializeField] private ImpactKind impactKind = ImpactKind.CloseWall;

    [Header("Impact Strength")]
    [SerializeField] private float minImpactSpeed = 1.5f;
    [SerializeField] private float maxImpactSpeed = 8f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Ball"))
            return;

        float rawSpeed = collision.relativeVelocity.magnitude;
        float strength01 = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, rawSpeed);

        OnBinImpact?.Invoke(side, impactKind, Mathf.Clamp01(strength01));
    }
}