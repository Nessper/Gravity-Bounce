using System;
using UnityEngine;

/// <summary>
/// Porte les modeles d'effets de destruction propres aux billes.
/// La copie jouee est detachee du BallNode afin de survivre a son recyclage.
/// </summary>
public sealed class BallDestructionEffects : MonoBehaviour
{
    [SerializeField, HideInInspector] private int editorPresetVersion;

    [Header("Effets par couleur")]
    [SerializeField] private ParticleSystem whiteEffect;
    [SerializeField] private ParticleSystem blueEffect;
    [SerializeField] private ParticleSystem redEffect;
    [SerializeField] private ParticleSystem blackEffect;

    private void Awake()
    {
        StopTemplates();
    }

    private void OnEnable()
    {
        StopTemplates();
    }

    public void Play(string ballId, Vector3 worldPosition)
    {
        ParticleSystem template = GetTemplate(ballId);
        if (template == null)
            return;

        ParticleSystem instance = Instantiate(
            template,
            worldPosition,
            template.transform.rotation
        );
        instance.name = $"Ball Destruction FX ({ballId})";
        instance.transform.SetParent(null, true);
        instance.gameObject.SetActive(true);

        ParticleSystem.MainModule main = instance.main;
        main.loop = false;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystemRenderer particleRenderer =
            instance.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
            particleRenderer.enabled = true;

        instance.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        // Le burst est place exactement a t = 0. Une amorce minuscule garantit
        // son emission meme si la bille est recyclee dans cette meme frame.
        instance.Simulate(
            0.01f,
            true,
            true,
            false
        );
        instance.Play(true);

        // L'auto-destruction ne doit etre armee qu'apres le nettoyage et le
        // demarrage. Sinon StopEmittingAndClear peut supprimer la copie avant
        // que son burst ait eu le temps d'etre rendu.
        main.stopAction = ParticleSystemStopAction.Destroy;
    }

    private void StopTemplates()
    {
        StopTemplate(whiteEffect);
        StopTemplate(blueEffect);
        StopTemplate(redEffect);
        StopTemplate(blackEffect);
    }

    private static void StopTemplate(ParticleSystem template)
    {
        if (template == null)
            return;

        template.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private ParticleSystem GetTemplate(string ballId)
    {
        if (string.Equals(ballId, "white", StringComparison.OrdinalIgnoreCase))
            return whiteEffect;

        if (string.Equals(ballId, "blue", StringComparison.OrdinalIgnoreCase))
            return blueEffect;

        if (string.Equals(ballId, "red", StringComparison.OrdinalIgnoreCase))
            return redEffect;

        if (string.Equals(ballId, "black", StringComparison.OrdinalIgnoreCase))
            return blackEffect;

        return whiteEffect;
    }
}
