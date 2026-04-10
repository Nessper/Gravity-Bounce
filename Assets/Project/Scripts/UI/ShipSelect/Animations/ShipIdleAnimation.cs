using UnityEngine;

/// <summary>
/// Animation idle tres legere du vaisseau.
/// Donne une sensation de vie sans nuire a la lisibilite.
/// </summary>
public class ShipIdleAnimation : MonoBehaviour
{
    [Header("Scale Breathing")]
    [SerializeField] private float scaleAmplitude = 0.015f;
    [SerializeField] private float scaleSpeed = 1.2f;

    [Header("Vertical Float")]
    [SerializeField] private float floatAmplitude = 0.02f;
    [SerializeField] private float floatSpeed = 0.8f;

    private Vector3 baseScale;
    private Vector3 basePosition;

    private float scaleTimeOffset;
    private float floatTimeOffset;

    private void Awake()
    {
        baseScale = transform.localScale;
        basePosition = transform.localPosition;

        // Décalage aléatoire pour éviter un mouvement trop “robot”
        scaleTimeOffset = Random.Range(0f, 10f);
        floatTimeOffset = Random.Range(0f, 10f);
    }

    private void Update()
    {
        AnimateScale();
        AnimateFloat();
    }

    private void AnimateScale()
    {
        float t = Time.time * scaleSpeed + scaleTimeOffset;
        float factor = 1f + Mathf.Sin(t) * scaleAmplitude;

        transform.localScale = baseScale * factor;
    }

    private void AnimateFloat()
    {
        float t = Time.time * floatSpeed + floatTimeOffset;
        float offsetY = Mathf.Sin(t) * floatAmplitude;

        Vector3 pos = basePosition;
        pos.y += offsetY;

        transform.localPosition = pos;
    }
}