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

    public void Init(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[ShipBackgroundLoader] empty path.");
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
        {
            Debug.LogError("[ShipBackgroundLoader] Sprite introuvable: " + path);
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
