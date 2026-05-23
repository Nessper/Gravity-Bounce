using UnityEngine;

public class FlushComboOverlayController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    public void Play(FlushResolution resolution)
    {
        if (resolution == null)
            return;

        if (verboseLogs)
        {
            Debug.Log(
                $"[FlushComboOverlay] " +
                $"Base={resolution.BaseTotal} " +
                $"Combo={resolution.ComboTotal} " +
                $"Final={resolution.FinalTotal}");
        }

        PlayBaseLayer(resolution);

        PlayComboLayers(resolution);

        PlayFinalTransfer(resolution);
    }

    // =========================================================
    // BASE LAYER
    // =========================================================

    private void PlayBaseLayer(FlushResolution resolution)
    {
        if (resolution.BaseItems == null)
            return;

        for (int i = 0; i < resolution.BaseItems.Count; i++)
        {
            BaseScoreItem item = resolution.BaseItems[i];

            Debug.Log(
                $"[BaseScoreLayer] " +
                $"{item.BallType} +{item.Points}");
        }
    }

    // =========================================================
    // COMBO LAYERS
    // =========================================================

    private void PlayComboLayers(FlushResolution resolution)
    {
        if (resolution.ComboEvents == null)
            return;

        for (int i = 0; i < resolution.ComboEvents.Count; i++)
        {
            ComboEvent combo = resolution.ComboEvents[i];

            Debug.Log(
                $"[ComboLayer] " +
                $"{combo.Id} " +
                $"+{combo.Points} " +
                $"({combo.Family})");
        }
    }

    // =========================================================
    // FINAL TRANSFER
    // =========================================================

    private void PlayFinalTransfer(FlushResolution resolution)
    {
        Debug.Log(
            $"[FinalTransfer] +{resolution.FinalTotal}");
    }
}