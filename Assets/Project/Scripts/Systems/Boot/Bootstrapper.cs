using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Point d'entrée du jeu.
/// - Charge le ShipCatalog depuis Resources (source unique).
/// - Vérifie RunConfig.
/// - Route vers DebugLauncher si debug actif.
/// - Sinon délègue au GameFlowController vers Title,
///   ou fallback sur LoadScene direct vers Title.
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    private const string PlayerPrefKey_DebugMain = "VS_DEBUG_MAIN";

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string debugLauncherSceneName = "DebugLauncher";

    private IEnumerator Start()
    {
        // 1) Charge le ShipCatalog depuis Resources
        LoadShipCatalogFromResources();

        // 2) Petit check : le singleton RunConfig devrait déjà exister dans la Boot scene
        if (RunConfig.Instance == null)
            Debug.LogWarning("[Bootstrapper] RunConfig singleton manquant ?");

        // 3) S'assurer que BootRoot / GameFlow sont prêts avant de déléguer
        yield return EnsureGameFlowReady();

        // 4) Mode debug: on route vers la scene DebugLauncher (qui injecte + charge RunHub/Main)
        if (IsDebugLauncherActive())
        {
            if (string.IsNullOrEmpty(debugLauncherSceneName))
            {
                Debug.LogWarning("[Bootstrapper] Debug active but debugLauncherSceneName is empty. Falling back to Title.");
            }
            else
            {
                Debug.Log("[Bootstrapper] Debug launcher active. Loading scene: " + debugLauncherSceneName);
                SceneManager.LoadScene(debugLauncherSceneName, LoadSceneMode.Single);
                yield break;
            }
        }

        // 5) Flow normal vers Title
        if (BootRoot.GameFlow != null)
        {
            Debug.Log("[Bootstrapper] Delegation vers GameFlowController.GoToTitle().");
            BootRoot.GameFlow.GoToTitle();
        }
        else
        {
            Debug.LogWarning("[Bootstrapper] GameFlowController introuvable. Fallback: chargement direct de la scene Title.");
            SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
        }
    }

    private bool IsDebugLauncherActive()
    {
        // On choisit volontairement un seul interrupteur simple:
        // PlayerPrefs VS_DEBUG_MAIN = 1
        // (Editor ou build, même comportement)
        return PlayerPrefs.GetInt(PlayerPrefKey_DebugMain, 0) == 1;
    }

    private void LoadShipCatalogFromResources()
    {
        // Garde-fou anti double-load
        if (ShipCatalogService.Catalog != null &&
            ShipCatalogService.Catalog.ships != null &&
            ShipCatalogService.Catalog.ships.Count > 0)
        {
            Debug.LogWarning("[Bootstrapper] ShipCatalog deja charge. Double load ignore.");
            return;
        }

        TextAsset jsonAsset = Resources.Load<TextAsset>("Ships/ShipCatalog");
        if (jsonAsset == null)
        {
            Debug.LogError("[Bootstrapper] ShipCatalog introuvable dans Resources/Ships/ShipCatalog.");
            return;
        }

        ShipCatalog catalog = null;

        try
        {
            catalog = JsonUtility.FromJson<ShipCatalog>(jsonAsset.text);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Bootstrapper] Exception lors du parsing ShipCatalog: " + ex.Message);
            return;
        }

        if (catalog == null || catalog.ships == null || catalog.ships.Count == 0)
        {
            Debug.LogError("[Bootstrapper] ShipCatalog invalide ou vide (Resources).");
            return;
        }

        ShipCatalogService.Catalog = catalog;
        Debug.Log("[Bootstrapper] ShipCatalog charge depuis Resources (" + catalog.ships.Count + " vaisseaux).");
    }

    private IEnumerator EnsureGameFlowReady()
    {
        if (BootRoot.Instance == null)
        {
            Debug.LogWarning("[Bootstrapper] BootRoot.Instance est null. GameFlow sera probablement null aussi.");
            yield break;
        }

        const int maxFrames = 3;
        int frames = 0;

        while (BootRoot.GameFlow == null && frames < maxFrames)
        {
            frames++;
            yield return null;
        }

        if (BootRoot.GameFlow == null)
        {
            Debug.LogWarning("[Bootstrapper] BootRoot.GameFlow toujours null apres attente. Fallback LoadScene possible.");
        }
    }
}
