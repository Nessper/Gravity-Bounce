using UnityEngine;

/// <summary>
/// Flash lumineux autonome utilise aux deux extremites d'une teleportation.
/// Il ne contient aucune logique de gameplay et se detruit a la fin du pulse.
/// </summary>
public sealed class DroneTeleportFlashEffect : MonoBehaviour
{
    private SpriteRenderer flashRenderer;
    private Color flashColor;
    private float duration;
    private float maximumScale;
    private float elapsed;

    public void Initialize(
        SpriteRenderer renderer,
        Color color,
        float flashDuration,
        float flashMaximumScale)
    {
        flashRenderer = renderer;
        flashColor = color;
        duration = Mathf.Max(0.01f, flashDuration);
        maximumScale = Mathf.Max(0.01f, flashMaximumScale);
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float expansion = 1f - Mathf.Pow(1f - progress, 3f);
        float lightPulse = Mathf.Sin(progress * Mathf.PI);

        transform.localScale = Vector3.one *
            Mathf.Lerp(0.08f, maximumScale, expansion);

        if (flashRenderer != null)
        {
            Color color = flashColor;
            color.a *= lightPulse;
            flashRenderer.color = color;
        }

        if (progress >= 1f)
            Destroy(gameObject);
    }
}
