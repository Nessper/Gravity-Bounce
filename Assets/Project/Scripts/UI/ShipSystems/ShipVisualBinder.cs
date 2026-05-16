using UnityEngine;
using UnityEngine.UI;

public class ShipVisualBinder : MonoBehaviour
{
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private Image image;

    private void OnEnable()
    {
        if (runSessionState != null)
            runSessionState.OnShipChanged.AddListener(OnShipChanged);

        Refresh();
    }

    private void OnDisable()
    {
        if (runSessionState != null)
            runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
    }

    private void OnShipChanged(string shipId)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (runSessionState == null || image == null)
            return;

        Debug.Log("[ShipVisualBinder] ShipId=" + runSessionState.ShipId);

        ShipDefinition def = ShipCatalogService.GetById(runSessionState.ShipId);

        if (def == null || string.IsNullOrWhiteSpace(def.imagePath))
        {
            image.sprite = null;
            image.enabled = false;
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(def.imagePath);

        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}