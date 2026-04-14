using UnityEngine;
using UnityEngine.UI;

public class ShipVisualBinder : MonoBehaviour
{
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private Image image;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (runSessionState == null || image == null)
            return;

        var def = ShipCatalogService.GetById(runSessionState.ShipId);

        if (def == null || string.IsNullOrWhiteSpace(def.imagePath))
        {
            image.sprite = null;
            image.enabled = false;
            return;
        }

        var sprite = Resources.Load<Sprite>(def.imagePath);

        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}