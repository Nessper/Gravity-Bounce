using UnityEngine;

public class CloseBinZoneFollower : MonoBehaviour
{
    [Header("Références monde -> UI")]
    [SerializeField] private Transform worldTarget;      // TouchAnchor sur le bin
    [SerializeField] private Camera worldCamera;         // Main Camera (jeu)
    [SerializeField] private RectTransform uiRect;       // CloseBinZone (UI)
    [SerializeField] private Canvas rootCanvas;          // Canvas principal (Screen Space Overlay)

    private RectTransform canvasRect;

    private void Awake()
    {
        if (rootCanvas != null)
        {
            canvasRect = rootCanvas.GetComponent<RectTransform>();
        }

        if (uiRect == null)
        {
            uiRect = GetComponent<RectTransform>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (worldTarget == null || uiRect == null || canvasRect == null || worldCamera == null)
            return;

        // Monde -> écran
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldTarget.position);

        // Écran -> position locale dans le canvas
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                null, // Canvas Screen Space Overlay -> null
                out localPoint))
        {
            uiRect.anchoredPosition = localPoint;
        }
    }
}
