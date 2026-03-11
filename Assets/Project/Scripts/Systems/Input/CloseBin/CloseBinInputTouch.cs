using UnityEngine;
using UnityEngine.UI;

public class CloseBinInputTouch : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private CloseBinController closeBin;
    [SerializeField] private RectTransform closeBinZone;

    [Header("Options")]
    [SerializeField] private bool inputEnabled = true;

    // Id du doigt qui contrôle actuellement le CloseBin (-1 = aucun)
    private int activeFingerId = -1;

    private void Update()
    {
        if (!inputEnabled || closeBin == null || closeBinZone == null)
            return;

#if UNITY_EDITOR
        // Mode Editor : souris = doigt
        HandleMouseSimulatedTouch();
#else
    // Mode Build / Mobile : uniquement les vrais touch
    if (!Application.isMobilePlatform)
        return;

    HandleRealTouches();
#endif
    }


    private void HandleRealTouches()
    {
        if (Input.touchCount == 0)
        {
            if (activeFingerId != -1)
            {
                closeBin.SetClosedFromInput(false);
                activeFingerId = -1;
            }
            return;
        }

        // Si aucun doigt n'est encore associé au CloseBin, on cherche un touch qui commence dans la zone
        if (activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase != TouchPhase.Began)
                    continue;

                if (IsInCloseBinZone(t.position))
                {
                    activeFingerId = t.fingerId;
                    closeBin.SetClosedFromInput(true);
                    break;
                }
            }
        }

        // Si on a un doigt actif, on le suit
        if (activeFingerId != -1)
        {
            bool found = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.fingerId != activeFingerId)
                    continue;

                found = true;

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    closeBin.SetClosedFromInput(false);
                    activeFingerId = -1;
                }

                break;
            }

            if (!found)
            {
                closeBin.SetClosedFromInput(false);
                activeFingerId = -1;
            }
        }
    }

    private void HandleMouseSimulatedTouch()
    {
        Vector2 mousePos = Input.mousePosition;
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(closeBinZone, mousePos);

        if (Input.GetMouseButtonDown(0) && inside)
        {
            activeFingerId = 0;
            closeBin.SetClosedFromInput(true);
        }

        if (Input.GetMouseButtonUp(0) && activeFingerId == 0)
        {
            closeBin.SetClosedFromInput(false);
            activeFingerId = -1;
        }
    }

    private bool IsInCloseBinZone(Vector2 screenPosition)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(closeBinZone, screenPosition);
    }

    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;

        if (!inputEnabled && activeFingerId != -1)
        {
            closeBin.SetClosedFromInput(false);
            activeFingerId = -1;
        }
    }
}
