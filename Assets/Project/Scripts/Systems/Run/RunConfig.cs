using UnityEngine;

public class RunConfig : MonoBehaviour
{
    public static RunConfig Instance { get; private set; }

    /// <summary>
    /// Identifiant du vaisseau sélectionné pour la run actuelle.
    /// Cette valeur est synchronisée avec la sauvegarde persistante.
    /// </summary>
    [Header("Progression / Vaisseau")]
    public string SelectedShipId = "CORE_SCOUT";

    /// <summary>
    /// Flag temporaire pour éventuellement skipper l'intro du Title une seule fois.
    /// Non persistant, uniquement pour la session en cours.
    /// </summary>
    [Header("Flow / Title")]
    public bool SkipTitleIntroOnce = false;

    /// <summary>
    /// En mode éditeur uniquement, permet de simuler un profil "mobile"
    /// même si l'on tourne sur PC. Pratique pour tester les réglages
    /// de vitesse/gravité/spawn sans faire un build Android à chaque fois.
    /// 
    /// IMPORTANT :
    /// - En build, ce flag est ignoré.
    /// - Le runtime s'appuie toujours sur la plateforme réelle (Android, iOS, etc.).
    /// </summary>
#if UNITY_EDITOR
    [Header("Debug / Simulation")]
    public bool SimulateMobileInEditor = false;
#endif

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Au démarrage, on récupère la valeur depuis la sauvegarde si possible.
        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            SelectedShipId = SaveManager.Instance.Current.selectedShipId;
        }
    }

    /// <summary>
    /// Met à jour le vaisseau sélectionné pour la session
    /// et synchronise la valeur avec la sauvegarde persistante.
    /// </summary>
    public void SetSelectedShip(string shipId)
    {
        SelectedShipId = shipId;

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            SaveManager.Instance.Current.selectedShipId = shipId;

            if (!SaveManager.Instance.Current.unlockedShips.Contains(shipId))
            {
                SaveManager.Instance.Current.unlockedShips.Add(shipId);
            }

            SaveManager.Instance.Save();
        }
    }

    /// <summary>
    /// Indique si l'on doit utiliser le profil "mobile" (gravité/vitesse/spawn adaptés).
    /// 
    /// - Sur device réel : vrai si la plateforme est Android (et éventuellement iOS plus tard).
    /// - Dans l'éditeur : peut être forcé avec SimulateMobileInEditor.
    /// </summary>
    public bool IsMobileProfile
    {
        get
        {
            // Détection runtime réelle
            bool isRuntimeMobile =
                Application.platform == RuntimePlatform.Android
                || Application.platform == RuntimePlatform.IPhonePlayer;

#if UNITY_EDITOR
            // En mode éditeur, on peut forcer le profil mobile pour les tests.
            if (SimulateMobileInEditor)
            {
                return true;
            }
#endif

            return isRuntimeMobile;
        }
    }
}
