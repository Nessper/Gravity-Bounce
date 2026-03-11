using UnityEngine;

/// <summary>
/// Détecte les collisions avec les billes et déclenche un pulse visuel.
/// À mettre sur le GameObject du mur (LeftWall / RightWall) qui a le collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallImpactDetector : MonoBehaviour
{
    private EnergyWallFX pulse;

    private void Awake()
    {
        // On prend l'enfant qui porte le visuel
        pulse = GetComponentInChildren<EnergyWallFX>();
        if (pulse == null)
        {
            Debug.LogWarning($"[WallImpactDetector] Aucun EnergyWallPulse trouvé sur {name}.");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Vérifie que c’est une bille
        if (!collision.collider.CompareTag("Ball"))
            return;

        // Déclenche le pulse
        if (pulse != null)
        {
            pulse.TriggerPulse();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Si jamais tes murs passent en trigger un jour
        if (!other.CompareTag("Ball"))
            return;

        if (pulse != null)
        {
            pulse.TriggerPulse();
        }
    }
}
