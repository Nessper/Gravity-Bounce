using System.Collections;
using UnityEngine;
using TMPro;

public class PhaseBannerUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private GameObject overlay;
    [SerializeField] private TMP_Text label;
    [SerializeField] private float showDuration = 1.0f;

    private Coroutine routine;

    private void OnEnable()
    {
        if (spawner == null) spawner = Object.FindFirstObjectByType<BallSpawner>();
        if (spawner == null || overlay == null || label == null)
        {
            Debug.LogWarning("[PhaseBannerUI] Références manquantes.");
            return;
        }

        overlay.SetActive(false);
        spawner.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnPhaseChanged -= HandlePhaseChanged;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void HandlePhaseChanged(int index, string name)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowOnce(name));
    }

    private IEnumerator ShowOnce(string name)
    {
        label.text = (name ?? "PHASE").ToUpperInvariant();
        overlay.SetActive(true);
        yield return new WaitForSeconds(showDuration);
        overlay.SetActive(false);
        routine = null;
    }

    // --- AJOUT MINIMAL : affichage manuel, sans toucher au wiring du spawner ---
    public void ShowPhaseText(string text, float duration)
    {
        if (overlay == null || label == null)
        {
            Debug.LogWarning("[PhaseBannerUI] Overlay ou label manquant.");
            return;
        }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowCustom(text, duration));
    }

    private IEnumerator ShowCustom(string text, float duration)
    {
        label.text = (text ?? string.Empty).ToUpperInvariant();
        overlay.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
        overlay.SetActive(false);
        routine = null;
    }
}
