using UnityEngine;

public class PaddleGuideUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private PlayerController player;
    [SerializeField] private RectTransform trackRect;   // le rail (PaddleGuideTrack)
    [SerializeField] private RectTransform handleRect;  // le fantôme (PaddleGuideHandle)



    // Caméra à utiliser pour la conversion écran -> UI.
    // - Si ton Canvas est en Screen Space - Overlay : laisse null.
    // - Si ton Canvas est en Screen Space - Camera : mets la caméra du Canvas.
    [SerializeField] private Camera uiCamera;

    private void LateUpdate()
    {
        if (player == null || trackRect == null || handleRect == null)
            return;

        // 1) Position monde du paddle
        Vector3 worldPos = player.transform.position;

        // 2) Monde -> écran
        // Si tu as une caméra de gameplay spécifique, tu peux la sérialiser au lieu de Camera.main.
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // 3) Écran -> coordonnées locales dans le rail
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                trackRect,
                screenPos,
                uiCamera,      // null si Canvas overlay
                out localPoint))
        {
            return;
        }

        // 4) On applique seulement le X, on laisse le Y tel quel (ou 0 si tu préfères)
        Vector2 lp = handleRect.localPosition;
        lp.x = localPoint.x;
        handleRect.localPosition = lp;
    }
}
