using UnityEngine;

public class BoardAlignToUI : MonoBehaviour
{
    public Camera gameplayCamera;          // Caméra orthographic qui voit le plateau
    public RectTransform boardTopAnchorUI; // Le HUD qui donne la ligne "top du board"
    public Transform boardTopMarker;       // Le Ceiling (haut du plateau)

    void Start()
    {
        AlignToUI();
    }

    public void AlignToUI()
    {
        if (gameplayCamera == null || boardTopAnchorUI == null || boardTopMarker == null)
        {
            Debug.LogWarning("BoardAlignToUI: missing references.");
            return;
        }

        // 1. Coordonnées écran (pixels) de l'ancre UI
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, boardTopAnchorUI.position);

        // 2. Distance caméra -> boardTopMarker (profondeur plane)
        float depth = Mathf.Abs(gameplayCamera.transform.position.z - boardTopMarker.position.z);

        // 3. Projection des pixels sur le plan du plateau -> coordonnées MONDE
        Vector3 worldTarget = gameplayCamera.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, depth)
        );

        // 4. Delta Y entre le haut du plateau et cette cible
        float deltaY = worldTarget.y - boardTopMarker.position.y;

        // 5. Déplacement du BoardRoot
        transform.position += new Vector3(0f, deltaY, 0f);
    }
}
