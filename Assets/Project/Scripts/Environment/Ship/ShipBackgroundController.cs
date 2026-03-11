using UnityEngine;

/// <summary>
/// Charge un sprite de vaisseau depuis Resources/Ships/Images
/// et l assigne a un SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShipBackgroundController : MonoBehaviour
{
    [Header("SpriteRenderer cible dans la scene")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[ShipBackgroundLoader] Init called with empty fileName.");
            return;
        }

        if (targetRenderer == null)
        {
            Debug.LogError("[ShipBackgroundLoader] No SpriteRenderer assigned.");
            return;
        }

        string key = StripExtension(fileName);
        Sprite sprite = Resources.Load<Sprite>("Ships/Images/" + key);

        if (sprite == null)
        {
            Debug.LogError("[ShipBackgroundLoader] Sprite introuvable dans Resources: Ships/Images/" + key);
            return;
        }

        targetRenderer.sprite = sprite;
    }

    private string StripExtension(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        if (dot <= 0)
            return fileName;

        return fileName.Substring(0, dot);
    }
}
