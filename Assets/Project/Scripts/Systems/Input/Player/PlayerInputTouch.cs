using UnityEngine;
using UnityEngine.UI; // pour RectTransformUtility

/// <summary>
/// Source d'input pour le paddle en mode mobile.
/// Seul un touch qui COMMENCE dans la zone ThumbTouchArea
/// peut contrôler le paddle.
/// </summary>
public class PlayerInputTouch : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Camera mainCam;
    [SerializeField] private PlayerController player;
    [SerializeField] private RectTransform thumbTouchArea;

    // Id du doigt actuellement utilisé pour contrôler le paddle (-1 = aucun)
    private int activeFingerId = -1;

    private bool isDragging = false;
    private float dragOffsetX = 0f;

    private void Update()
    {
        // Ne tourne que sur mobile
        if (!Application.isMobilePlatform)
            return;

        if (player == null || mainCam == null || thumbTouchArea == null)
            return;

        if (Input.touchCount == 0)
        {
            // Plus de touch : on reset
            activeFingerId = -1;
            isDragging = false;
            return;
        }

        // 1) Si aucun doigt n'est encore assigné, on cherche un touch qui commence dans la zone
        if (activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase == TouchPhase.Began)
                {
                    if (IsInThumbArea(t.position))
                    {
                        // On capture ce doigt comme contrôleur du paddle
                        activeFingerId = t.fingerId;

                        float distance = Mathf.Abs(mainCam.transform.position.z - player.transform.position.z);
                        Vector3 screenPos = new Vector3(t.position.x, t.position.y, distance);
                        Vector3 worldPos = mainCam.ScreenToWorldPoint(screenPos);

                        dragOffsetX = player.transform.position.x - worldPos.x;
                        isDragging = true;
                        break;
                    }
                }
            }
        }

        // 2) Si on a un doigt actif, on le suit
        if (activeFingerId != -1)
        {
            bool found = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.fingerId != activeFingerId)
                    continue;

                found = true;

                float distance = Mathf.Abs(mainCam.transform.position.z - player.transform.position.z);
                Vector3 screenPos = new Vector3(t.position.x, t.position.y, distance);
                Vector3 worldPos = mainCam.ScreenToWorldPoint(screenPos);

                switch (t.phase)
                {
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (isDragging)
                        {
                            float targetX = worldPos.x + dragOffsetX;
                            player.SetTargetXWorld(targetX);
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        activeFingerId = -1;
                        isDragging = false;
                        break;
                }

                break;
            }

            // Si le doigt actif a disparu (cas bizarre), on reset
            if (!found)
            {
                activeFingerId = -1;
                isDragging = false;
            }
        }
    }

    /// <summary>
    /// Retourne true si la position écran donnée est dans la zone de pouce.
    /// </summary>
    private bool IsInThumbArea(Vector2 screenPosition)
    {
        // On suppose que thumbTouchArea est dans le même Canvas que l'UI principale
        return RectTransformUtility.RectangleContainsScreenPoint(thumbTouchArea, screenPosition);
    }
}
