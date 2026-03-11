using UnityEngine;
using UnityEngine.EventSystems;

public class ShopTuningToggleController : MonoBehaviour
{
    [Header("Panels (CanvasGroup)")]
    [SerializeField] private CanvasGroup shopPanelGroup;
    [SerializeField] private CanvasGroup shipPanelGroup;

    [Header("Event System (optional)")]
    [SerializeField] private GameObject firstSelectedWhenOpen;
    [SerializeField] private GameObject firstSelectedWhenClose;

    private bool isOpen = false;

    private void Awake()
    {
        // Etat initial
        SetOpen(false);

        // Evite un focus bizarre au lancement
        ClearSelection();
    }

    // Appele par le bouton TUNING
    public void OnClickTuning()
    {
        if (isOpen)
            return;

        SetOpen(true);
    }

    // Appele par le bouton BACK
    public void OnClickBack()
    {
        if (!isOpen)
            return;

        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (open)
        {
            // Shop visible mais desarme (pas de clic)
            SetGroup(shopPanelGroup, 1f, false, false);

            // Ship panel actif
            SetGroup(shipPanelGroup, 1f, true, true);

            // Assure l ordre d affichage
            if (shipPanelGroup != null)
                shipPanelGroup.transform.SetAsLastSibling();

            FixSelection(firstSelectedWhenOpen);
        }
        else
        {
            // Shop actif
            SetGroup(shopPanelGroup, 1f, true, true);

            // Ship panel desactive
            SetGroup(shipPanelGroup, 0f, false, false);

            FixSelection(firstSelectedWhenClose);
        }
    }

    private void SetGroup(CanvasGroup g, float a, bool interactable, bool blocksRaycasts)
    {
        if (g == null)
            return;

        g.alpha = a;
        g.interactable = interactable;
        g.blocksRaycasts = blocksRaycasts;
    }

    private void FixSelection(GameObject target)
    {
        if (EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);

        if (target != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    private void ClearSelection()
    {
        if (EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
    }
}
