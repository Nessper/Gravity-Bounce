using System.Collections;
using UnityEngine;

/// <summary>
/// Flash d'impact du paddle via le shader PlayerInnerScan.
/// </summary>
public class PlayerShaderFlashFeedback : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private float flashIntensity = 1f;
    [SerializeField] private float flashDuration = 0.12f;

    private Material runtimeMaterial;
    private Coroutine flashRoutine;

    private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer != null)
            runtimeMaterial = targetRenderer.material;
    }

    public void TriggerFlash()
    {
        if (runtimeMaterial == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float t = 0f;
        float half = flashDuration * 0.5f;

        while (t < half)
        {
            t += Time.deltaTime;
            float k = t / half;
            runtimeMaterial.SetFloat(HitFlashId, Mathf.Lerp(0f, flashIntensity, k));
            yield return null;
        }

        t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            float k = t / half;
            runtimeMaterial.SetFloat(HitFlashId, Mathf.Lerp(flashIntensity, 0f, k));
            yield return null;
        }

        runtimeMaterial.SetFloat(HitFlashId, 0f);
        flashRoutine = null;
    }
}