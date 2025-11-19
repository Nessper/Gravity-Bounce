using UnityEngine;

public class RunConfig : MonoBehaviour
{
    public static RunConfig Instance { get; private set; }

    /// <summary>
    /// Identifiant du vaisseau sélectionné pour la run actuelle.
    /// Cette valeur est synchronisée avec la sauvegarde persistante.
    /// </summary>
    public string SelectedShipId = "CORE_SCOUT";

    /// <summary>
    /// Flag temporaire pour éventuellement skipper l'intro du Title une seule fois.
    /// Non persistant, uniquement pour la session en cours.
    /// </summary>
    public bool SkipTitleIntroOnce = false;

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
}
