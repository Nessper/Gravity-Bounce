using System.Collections;
using UnityEngine;

public class PlayerFlashFeedback : MonoBehaviour
{
    [Header("Sprite à faire pulser (PlayerGlow)")]
    [SerializeField] private SpriteRenderer flashRenderer;

    [Header("Réglages du flash")]
    [SerializeField] private float flashDuration = 0.15f;   // durée totale du flash
    [SerializeField] private float intensityMultiplier = 2f; // 2 = deux fois plus lumineux

    private Color baseColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (flashRenderer == null)
        {
            flashRenderer = GetComponent<SpriteRenderer>();
        }

        if (flashRenderer != null)
        {
            baseColor = flashRenderer.color;
        }
    }

    public void TriggerFlash()
    {
        if (flashRenderer == null)
            return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        float halfDuration = flashDuration * 0.5f;

        // On calcule une couleur plus lumineuse en gardant la teinte
        Color brightColor = baseColor * intensityMultiplier;
        brightColor.a = baseColor.a; // on garde la même alpha

        // Phase 1 : montée vers la couleur brillante
        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float lerp = t / halfDuration;
            flashRenderer.color = Color.Lerp(baseColor, brightColor, lerp);
            yield return null;
        }

        // Phase 2 : retour à la couleur de base
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float lerp = t / halfDuration;
            flashRenderer.color = Color.Lerp(brightColor, baseColor, lerp);
            yield return null;
        }

        flashRenderer.color = baseColor;
        flashRoutine = null;
    }
}
