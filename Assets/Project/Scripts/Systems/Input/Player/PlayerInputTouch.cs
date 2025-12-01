using UnityEngine;

public class PlayerInputTouch : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private PlayerController player;
    [SerializeField] private RectTransform thumbTouchArea;

    // Pour le halo / hints
    private bool hasActivePointer;
    public bool HasActivePointer
    {
        get { return hasActivePointer; }
    }

    [Header("Options")]
    [SerializeField] private bool inputEnabled = true;
    [SerializeField] private float responsiveness = 3.5f;
    [SerializeField] private float expo = 1.4f;

    // Id du doigt qui contrôle actuellement le paddle (-1 = aucun)
    private int activeFingerId = -1;

    private void Update()
    {
        if (!inputEnabled || player == null)
            return;

#if UNITY_EDITOR
        HandleMouseSimulatedTouch();
#else
        if (!Application.isMobilePlatform)
        {
            hasActivePointer = false;
            activeFingerId = -1;
            return;
        }

        HandleRealTouches();
#endif
    }

#if UNITY_EDITOR
    private void HandleMouseSimulatedTouch()
    {
        if (!Input.GetMouseButton(0))
        {
            hasActivePointer = false;
            return;
        }

        Vector2 mousePos = Input.mousePosition;

        // Si une ThumbTouchArea est définie, on ne prend que les clics dedans
        if (thumbTouchArea != null &&
            !RectTransformUtility.RectangleContainsScreenPoint(thumbTouchArea, mousePos))
        {
            hasActivePointer = false;
            return;
        }

        hasActivePointer = true;
        UpdatePaddleFromScreenPos(mousePos);
    }
#endif

    private void HandleRealTouches()
    {
        if (Input.touchCount == 0)
        {
            hasActivePointer = false;
            activeFingerId = -1;
            return;
        }

        // Si aucun doigt n'est encore associé, on en cherche un qui commence dans la ThumbZone
        if (activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase != TouchPhase.Began)
                    continue;

                if (IsInThumbZone(t.position))
                {
                    activeFingerId = t.fingerId;
                    hasActivePointer = true;
                    UpdatePaddleFromScreenPos(t.position);
                    return;
                }
            }

            // Aucun touch Began dans la zone
            hasActivePointer = false;
            return;
        }

        // Sinon, on suit le doigt déjà actif
        bool found = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.fingerId != activeFingerId)
                continue;

            found = true;

            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                activeFingerId = -1;
                hasActivePointer = false;
            }
            else
            {
                hasActivePointer = true;
                UpdatePaddleFromScreenPos(t.position);
            }

            break;
        }

        // Cas bizarre : le doigt actif a disparu de la liste
        if (!found)
        {
            activeFingerId = -1;
            hasActivePointer = false;
        }
    }

    private bool IsInThumbZone(Vector2 screenPos)
    {
        if (thumbTouchArea == null)
            return true; // fallback : tout l'écran si pas de zone

        // Canvas en Screen Space Overlay -> camera = null
        return RectTransformUtility.RectangleContainsScreenPoint(
            thumbTouchArea,
            screenPos,
            null
        );
    }

    private void UpdatePaddleFromScreenPos(Vector2 screenPos)
    {
        float tNorm = screenPos.x / Screen.width;
        float centered = (tNorm - 0.5f) * 2f;

        // expo > 1 = meilleure précision sur petits mouvements
        float abs = Mathf.Abs(centered);
        float curved = Mathf.Pow(abs, expo);
        centered = Mathf.Sign(centered) * curved;

        // nervosité globale
        centered = Mathf.Clamp(centered * responsiveness, -1f, 1f);

        float targetX = centered * player.XRange;
        player.SetTargetXWorld(targetX);
    }

    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;

        if (!state)
        {
            activeFingerId = -1;
            hasActivePointer = false;
        }
    }
}
