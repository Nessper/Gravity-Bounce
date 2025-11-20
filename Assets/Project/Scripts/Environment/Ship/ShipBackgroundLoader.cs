using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Charge un sprite de vaisseau depuis StreamingAssets/Ships/Images
/// et l'assigne à un SpriteRenderer pour l'utiliser comme décor
/// (vaisseau flouté sous le plateau).
/// 
/// Le LevelManager (ou un autre contrôleur) doit appeler Init(fileName)
/// avec le nom du fichier image correspondant au vaisseau sélectionné,
/// par exemple "CORE_SCOUT.png".
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShipBackgroundLoader : MonoBehaviour
{
    [Header("SpriteRenderer cible dans la scène")]
    [Tooltip("SpriteRenderer qui affichera le vaisseau en fond.")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private void Awake()
    {
        // Si aucun SpriteRenderer n'est renseigné dans l'inspector,
        // on prend celui du GameObject sur lequel est attaché ce script.
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// Point d'entrée appelé depuis LevelManager (ou équivalent).
    /// On lui passe le nom de fichier du vaisseau à afficher en fond,
    /// par exemple "CORE_SCOUT.png".
    /// </summary>
    /// <param name="fileName">
    /// Nom du fichier dans StreamingAssets/Ships/Images
    /// (sans chemin, avec extension).
    /// </param>
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

        // Lance la coroutine de chargement asynchrone.
        StartCoroutine(LoadSpriteFromStreamingAssetsToRenderer(fileName, targetRenderer));
    }

    /// <summary>
    /// Charge une texture depuis StreamingAssets/Ships/Images, la convertit
    /// en Sprite, puis l'assigne au SpriteRenderer cible.
    /// 
    /// La logique est volontairement proche de ce que tu utilises déjà
    /// dans IntroLevelUI / ShipSelect pour rester cohérent.
    /// </summary>
    private IEnumerator LoadSpriteFromStreamingAssetsToRenderer(string fileName, SpriteRenderer target)
    {
        if (target == null || string.IsNullOrEmpty(fileName))
            yield break;

        // Construit le chemin complet vers StreamingAssets/Ships/Images/<fileName>.
        string url = Path.Combine(Application.streamingAssetsPath, "Ships/Images", fileName);

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            // Envoi de la requête asynchrone.
            yield return req.SendWebRequest();

            // Vérifie le résultat.
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ShipBackgroundLoader] Failed to load texture: " + req.error + " (" + url + ")");
                yield break;
            }

            // Récupère la texture téléchargée.
            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
                yield break;

            // Crée un Sprite à partir de la texture.
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), // pivot au centre
                100f                      // pixels per unit (à adapter si besoin)
            );

            // Assigne le sprite au SpriteRenderer cible.
            target.sprite = sprite;
        }
    }
}
