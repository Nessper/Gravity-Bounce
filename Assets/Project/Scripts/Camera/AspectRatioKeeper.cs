using UnityEngine;

/// <summary>
/// Force la caméra à conserver un ratio fixe (ex : 9:16 pour un jeu vertical).
/// Ajoute automatiquement des bandes noires sur les côtés si l'écran est plus large.
/// </summary>
[RequireComponent(typeof(Camera))]
public class AspectRatioKeeper : MonoBehaviour
{
    public float targetAspect = 9f / 16f; // ratio portrait

    void Start()
    {
        Camera cam = GetComponent<Camera>();

        float windowAspect = (float)Screen.width / (float)Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Rect rect = cam.rect;

        if (scaleHeight < 1.0f)
        {
            // bandes en haut/bas
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
        }
        else
        {
            // bandes gauche/droite
            float scaleWidth = 1.0f / scaleHeight;

            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
        }

        cam.rect = rect;
    }
}