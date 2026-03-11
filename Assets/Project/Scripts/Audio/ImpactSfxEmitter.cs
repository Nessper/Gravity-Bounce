using UnityEngine;

/// <summary>
/// Joue un SFX d'impact basé sur la vitesse de collision (relativeVelocity).
/// Conçu pour être réutilisé sur paddle / obstacles / murs.
/// Supporte la variation de pitch ET de volume en fonction de l'impact.
/// </summary>
public class ImpactSfxEmitter : MonoBehaviour
{
    [Header("SFX")]
    [SerializeField] private SfxId sfxId = SfxId.None;

    [Tooltip("Si non vide, joue uniquement si l'autre collider a ce tag.")]
    [SerializeField] private string otherTagFilter = "Ball";

    [Header("Anti-spam")]
    [Tooltip("Délai minimal entre deux sons (secondes, time unscaled).")]
    [SerializeField] private float cooldownSec = 0.06f;

    [Header("Seuil impact")]
    [Tooltip("Vitesse minimale pour jouer le son (0 = toujours).")]
    [SerializeField] private float minImpactSpeed = 0.2f;

    [Header("Pitch par vitesse")]
    [SerializeField] private bool usePitchFromImpact = true;

    [Tooltip("Pitch min quand impactSpeed est faible.")]
    [SerializeField] private float minPitch = 0.95f;

    [Tooltip("Pitch max quand impactSpeed atteint impactSpeedForMaxPitch.")]
    [SerializeField] private float maxPitch = 1.05f;

    [Tooltip("Vitesse à partir de laquelle on atteint maxPitch.")]
    [SerializeField] private float impactSpeedForMaxPitch = 8f;

    [Header("Volume par vitesse")]
    [SerializeField] private bool useVolumeFromImpact = true;

    [Tooltip("Multiplicateur min de volume quand impactSpeed est faible.")]
    [SerializeField] private float minVolumeMult = 0.3f;

    [Tooltip("Multiplicateur max de volume quand impactSpeed atteint impactSpeedForMaxVolume.")]
    [SerializeField] private float maxVolumeMult = 1.0f;

    [Tooltip("Vitesse à partir de laquelle on atteint maxVolumeMult.")]
    [SerializeField] private float impactSpeedForMaxVolume = 8f;

    private float lastPlayTimeUnscaled = -999f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsAllowedOther(collision.collider))
            return;

        if (BootRoot.Audio == null || sfxId == SfxId.None)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (minImpactSpeed > 0f && impactSpeed < minImpactSpeed)
            return;

        float now = Time.unscaledTime;
        if (cooldownSec > 0f && (now - lastPlayTimeUnscaled) < cooldownSec)
            return;

        lastPlayTimeUnscaled = now;

        float volumeMult = ComputeVolumeMult(impactSpeed);

        if (!usePitchFromImpact)
        {
            // Volume variable, pitch par défaut (entry.pitch).
            BootRoot.Audio.PlaySfx(sfxId, pitchOverride: 1f, volumeMult: volumeMult);
            return;
        }

        float pitch = ComputePitch(impactSpeed);

        // Pitch + volume variables.
        BootRoot.Audio.PlaySfx(sfxId, pitch, volumeMult);
    }

    private float ComputePitch(float impactSpeed)
    {
        float t = Mathf.InverseLerp(0f, impactSpeedForMaxPitch, impactSpeed);
        return Mathf.Lerp(minPitch, maxPitch, t);
    }

    private float ComputeVolumeMult(float impactSpeed)
    {
        if (!useVolumeFromImpact)
            return 1f;

        float t = Mathf.InverseLerp(0f, impactSpeedForMaxVolume, impactSpeed);
        return Mathf.Lerp(minVolumeMult, maxVolumeMult, t);
    }

    private bool IsAllowedOther(Collider other)
    {
        if (string.IsNullOrEmpty(otherTagFilter))
            return true;

        return other.CompareTag(otherTagFilter);
    }
}
