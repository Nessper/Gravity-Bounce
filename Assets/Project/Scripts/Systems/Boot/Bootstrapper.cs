using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Point d'entrée du jeu.
/// - Charge le ShipCatalog depuis StreamingAssets.
/// - Puis enchaîne sur la scène Title.
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private string titleSceneName = "Title";

    /// <summary>
    /// Coroutine de démarrage :
    /// 1) Charge le ShipCatalog (vaisseaux) depuis un JSON dans StreamingAssets.
    /// 2) Log en cas d'erreur ou de succès.
    /// 3) Vérifie la présence de RunConfig.
    /// 4) Charge la scène Title.
    /// </summary>
    private IEnumerator Start()
    {
        // Construit l'URL du fichier ShipCatalog.json dans StreamingAssets
        string url = System.IO.Path.Combine(Application.streamingAssetsPath, "Ships/ShipCatalog.json");

        // Charge ShipCatalog via UnityWebRequest (compatible toutes plateformes)
        using (var req = UnityWebRequest.Get(url))
        {
            // Attente de la fin de la requête
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Erreur réseau ou fichier introuvable
                Debug.LogError("[Bootstrapper] ShipCatalog load failed: " + req.error + " (" + url + ")");
            }
            else
            {
                // Récupère le texte JSON
                var json = req.downloadHandler.text;

                // Désérialise et stocke dans le service global
                ShipCatalogService.Catalog = JsonUtility.FromJson<ShipCatalog>(json);

                // Vérifications basiques sur le catalogue
                if (ShipCatalogService.Catalog == null || ShipCatalogService.Catalog.ships == null || ShipCatalogService.Catalog.ships.Count == 0)
                {
                    Debug.LogError("[Bootstrapper] ShipCatalog invalid or empty.");
                }
                else
                {
                    Debug.Log($"[Bootstrapper] Loaded {ShipCatalogService.Catalog.ships.Count} ship(s).");
                }
            }
        }

        // Petit check : le singleton RunConfig devrait déjà exister dans la Boot scene
        if (RunConfig.Instance == null)
            Debug.LogWarning("[Bootstrapper] RunConfig singleton missing?");

        // Une fois le catalogue chargé, on passe à la scène Title
        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }
}
