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

    [Tooltip("Nombre d'unites monde de deplacement du paddle par pixel horizontal de doigt.")]
    [SerializeField] private float pixelsToUnits = 0.015f;

    [Tooltip("Exponent pour lisser les petits mouvements (1 = lineaire).")]
    [SerializeField] private float deltaExpo = 1.0f;

    // Id du doigt qui controle actuellement le paddle (-1 = aucun)
    private int activeFingerId = -1;

    // Etat du drag relatif
    private bool dragging = false;
    private float startTouchX;   // position ecran X au debut du drag
    private float startPaddleX;  // position monde X du paddle au debut du drag

    private void Update()
    {
        if (!inputEnabled || player == null)
        {
            hasActivePointer = false;
            activeFingerId = -1;
            dragging = false;
            return;
        }

#if UNITY_EDITOR
        HandleMouseSimulatedTouch();
#else
        if (!Application.isMobilePlatform)
        {
            hasActivePointer = false;
            activeFingerId = -1;
            dragging = false;
            return;
        }

        HandleRealTouches();
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Mode debug dans l'editeur : on simule un doigt avec la souris
    /// pour tester la logique relative sans build mobile.
    /// </summary>
    private void HandleMouseSimulatedTouch()
    {
        if (!Input.GetMouseButton(0))
        {
            hasActivePointer = false;
            dragging = false;
            return;
        }

        Vector2 mousePos = Input.mousePosition;

        // Si une ThumbTouchArea est definie, on ne prend que les clics dedans
        if (thumbTouchArea != null &&
            !RectTransformUtility.RectangleContainsScreenPoint(thumbTouchArea, mousePos))
        {
            hasActivePointer = false;
            dragging = false;
            return;
        }

        // Debut de drag
        if (!dragging)
        {
            dragging = true;
            hasActivePointer = true;
            startTouchX = mousePos.x;
            startPaddleX = player.transform.position.x;
        }

        hasActivePointer = true;
        UpdatePaddleFromRelativeDelta(mousePos.x);
    }
#endif

    /// <summary>
    /// Gestion du tactile reel sur mobile.
    /// </summary>
    private void HandleRealTouches()
    {
        if (Input.touchCount == 0)
        {
            hasActivePointer = false;
            activeFingerId = -1;
            dragging = false;
            return;
        }

        // Si aucun doigt n'est encore associe, on en cherche un qui commence dans la ThumbZone
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
                    dragging = true;

                    startTouchX = t.position.x;
                    startPaddleX = player.transform.position.x;

                    // Premier update immediat
                    UpdatePaddleFromRelativeDelta(t.position.x);
                    return;
                }
            }

            // Aucun touch Began dans la zone
            hasActivePointer = false;
            dragging = false;
            return;
        }

        // Sinon, on suit le doigt deja actif
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
                dragging = false;
            }
            else
            {
                hasActivePointer = true;
                dragging = true;
                UpdatePaddleFromRelativeDelta(t.position.x);
            }

            break;
        }

        // Cas bizarre : le doigt actif a disparu de la liste
        if (!found)
        {
            activeFingerId = -1;
            hasActivePointer = false;
            dragging = false;
        }
    }

    /// <summary>
    /// Verifie si la position ecran est dans la zone de pouce definie.
    /// </summary>
    private bool IsInThumbZone(Vector2 screenPos)
    {
        if (thumbTouchArea == null)
            return true; // fallback : tout l'ecran si pas de zone

        // Canvas en Screen Space Overlay -> camera = null
        return RectTransformUtility.RectangleContainsScreenPoint(
            thumbTouchArea,
            screenPos,
            null
        );
    }

    /// <summary>
    /// Met a jour la position cible du paddle a partir d'un delta horizontal relatif
    /// entre la position actuelle du doigt et la position de debut du drag.
    /// </summary>
    private void UpdatePaddleFromRelativeDelta(float currentPointerX)
    {
        if (!dragging)
            return;

        // Delta en pixels par rapport au debut du drag
        float deltaPixels = currentPointerX - startTouchX;

        // Optionnel : courbe exponentielle pour mieux controler les petits mouvements
        float sign = Mathf.Sign(deltaPixels);
        float abs = Mathf.Abs(deltaPixels);

        if (deltaExpo > 1.0f)
        {
            abs = Mathf.Pow(abs, deltaExpo);
        }

        float curvedDelta = sign * abs;

        // Conversion pixels -> unites monde
        float deltaWorld = curvedDelta * pixelsToUnits;

        float targetX = startPaddleX + deltaWorld;

        // Clamp dans le range du player
        targetX = Mathf.Clamp(targetX, -player.XRange, player.XRange);

        player.SetTargetXWorld(targetX);
    }

    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;

        if (!state)
        {
            activeFingerId = -1;
            hasActivePointer = false;
            dragging = false;
        }
    }
}
