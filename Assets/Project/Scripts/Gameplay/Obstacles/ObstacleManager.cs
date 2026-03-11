using UnityEngine;

/// <summary>
/// Responsable de la création des obstacles à partir du LevelData (JSON).
/// - Nettoie les anciens obstacles.
/// - Convertit les ObstaclePlacement (data) en instances de prefabs.
/// - Reste agnostique du gameplay : il ne fait que placer les objets.
/// </summary>
public class ObstacleManager : MonoBehaviour
{
    [Header("Parents dans la scène")]
    [Tooltip("Racine du board. Utilisée si ObstaclesRoot n'est pas définie.")]
    [SerializeField] private Transform boardRoot;

    [Tooltip("Parent sous lequel seront instanciés les obstacles.")]
    [SerializeField] private Transform obstaclesRoot;

    [Header("Prefabs d'obstacles")]
    [Tooltip("Prefab utilisé quand obstacleId = \"Obstacle1\".")]
    [SerializeField] private GameObject obstacle1Prefab;

    // Plus tard : tu pourras ajouter d'autres prefabs ici (Obstacle2, Absorber, etc.).

    private void Awake()
    {
        // Sécurité : si aucun parent dédié n'est renseigné, on utilise le boardRoot.
        if (obstaclesRoot == null)
        {
            obstaclesRoot = boardRoot;
        }
    }

    /// <summary>
    /// Appelé par le LevelManager une fois le LevelData chargé.
    /// Construit tous les obstacles décrits dans le JSON.
    /// </summary>
    public void BuildObstacles(ObstaclePlacement[] placements)
    {
        // On supprime les anciens obstacles au cas où on relance un niveau.
        ClearExistingObstacles();

        if (placements == null || placements.Length == 0)
        {
            return;
        }

        foreach (var placement in placements)
        {
            SpawnObstacle(placement);
        }
    }

    /// <summary>
    /// Détruit tous les enfants sous ObstaclesRoot.
    /// </summary>
    private void ClearExistingObstacles()
    {
        if (obstaclesRoot == null)
        {
            return;
        }

        for (int i = obstaclesRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = obstaclesRoot.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Instancie un obstacle à partir d'une entrée JSON.
    /// </summary>
    private void SpawnObstacle(ObstaclePlacement placement)
    {
        if (placement == null)
        {
            return;
        }

        GameObject prefab = ResolvePrefab(placement.obstacleId);
        if (prefab == null)
        {
            Debug.LogWarning($"ObstacleManager: aucun prefab trouvé pour obstacleId '{placement.obstacleId}'.");
            return;
        }

        Transform parent = obstaclesRoot != null ? obstaclesRoot : boardRoot;
        if (parent == null)
        {
            Debug.LogWarning("ObstacleManager: aucun parent valide (boardRoot / obstaclesRoot) assigné.");
            return;
        }

        GameObject instance = Instantiate(prefab, parent);

        // On applique la position / rotation locale telles que définies dans le JSON.
        instance.transform.localPosition = placement.localPosition;
        instance.transform.localEulerAngles = placement.localEulerAngles;

        // Si plus tard tu veux utiliser phaseIndex,
        // tu pourras ajouter ici une logique d'activation/désactivation par phase.
    }

    /// <summary>
    /// Associe un obstacleId (JSON) à un prefab Unity.
    /// </summary>
    private GameObject ResolvePrefab(string obstacleId)
    {
        if (string.IsNullOrEmpty(obstacleId))
        {
            return obstacle1Prefab;
        }

        switch (obstacleId)
        {
            case "Obstacle1":
                return obstacle1Prefab;

            // Plus tard :
            // case "Absorber":
            //     return absorberPrefab;

            default:
                // Fallback : on retourne Obstacle1 par défaut.
                return obstacle1Prefab;
        }
    }
}
