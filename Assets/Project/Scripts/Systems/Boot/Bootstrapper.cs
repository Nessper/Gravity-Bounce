using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Point d'entrée du jeu.
/// - Charge le ShipCatalog depuis StreamingAssets.
/// - Vérifie RunConfig.
/// - Puis délègue au GameFlowController si possible,
///   sinon fallback sur un LoadScene direct vers Title.
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private string titleSceneName = "Title";

    /// <summary>
    /// Coroutine de démarrage :
    /// 1) Charge le ShipCatalog (vaisseaux) depuis un JSON dans StreamingAssets.
    /// 2) Log en cas d'erreur ou de succès.
    /// 3) Vérifie la présence de RunConfig.
    /// 4) Passe la main au GameFlowController (GoToTitle) si disponible,
    ///    sinon charge la scène Title directement.
    /// </summary>
    private IEnumerator Start()
    {
        // 1) Construit l'URL du fichier ShipCatalog.json dans StreamingAssets
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

        // 2) Petit check : le singleton RunConfig devrait déjà exister dans la Boot scene
        if (RunConfig.Instance == null)
            Debug.LogWarning("[Bootstrapper] RunConfig singleton missing?");

        // 3) S'assurer que BootRoot / GameFlow sont prêts avant de déléguer
        yield return EnsureGameFlowReady();

        // 4) Si on a un GameFlowController, on lui délègue le passage vers Title
        if (BootRoot.GameFlow != null)
        {
            Debug.Log("[Bootstrapper] Delegating to GameFlowController.GoToTitle().");
            BootRoot.GameFlow.GoToTitle();
        }
        else
        {
            // Fallback sécurisé : on garde l'ancien comportement
            Debug.LogWarning("[Bootstrapper] GameFlowController not found. Fallback: direct load of Title scene.");
            SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Attend une ou deux frames pour laisser BootRoot / GameFlowController
    /// s'initialiser proprement. Ne spamme pas si tout est cassé.
    /// </summary>
    private IEnumerator EnsureGameFlowReady()
    {
        // Si BootRoot n'existe pas, on ne bloque pas : on laissera le fallback gérer.
        if (BootRoot.Instance == null)
        {
            Debug.LogWarning("[Bootstrapper] BootRoot.Instance is null. GameFlow will probably be null too.");
            yield break;
        }

        // On laisse 2–3 frames pour que GameFlowController.Awake() ait le temps de Register.
        const int maxFrames = 3;
        int frames = 0;

        while (BootRoot.GameFlow == null && frames < maxFrames)
        {
            frames++;
            yield return null;
        }

        if (BootRoot.GameFlow == null)
        {
            Debug.LogWarning("[Bootstrapper] BootRoot.GameFlow still null after wait. Will use fallback LoadScene.");
        }
    }
}
