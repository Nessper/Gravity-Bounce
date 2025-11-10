using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private string titleSceneName = "Title";

    private IEnumerator Start()
    {
        // Charge ShipCatalog via UnityWebRequest (OK partout)
        string url = System.IO.Path.Combine(Application.streamingAssetsPath, "Ships/ShipCatalog.json");
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[Bootstrapper] ShipCatalog load failed: " + req.error + " (" + url + ")");
            }
            else
            {
                // Désérialise et stocke dans le service
                var json = req.downloadHandler.text;
                ShipCatalogService.Catalog = JsonUtility.FromJson<ShipCatalog>(json);

                if (ShipCatalogService.Catalog == null || ShipCatalogService.Catalog.ships == null || ShipCatalogService.Catalog.ships.Count == 0)
                    Debug.LogError("[Bootstrapper] ShipCatalog invalid or empty.");
                else
                    Debug.Log($"[Bootstrapper] Loaded {ShipCatalogService.Catalog.ships.Count} ship(s).");
            }
        }

        if (RunConfig.Instance == null)
            Debug.LogWarning("[Bootstrapper] RunConfig singleton missing?");

        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }
}
