using UnityEngine;

/// <summary>
/// Service statique chargeant la structure des mondes depuis Resources.
/// 
/// Responsabilités :
/// - Charger une fois le WorldCatalog (JSON).
/// - Fournir l'ordre canonique des niveaux pour un monde donné.
/// - Ne contient AUCUNE logique de gameplay ou de progression.
/// 
/// Ce service est la base du système de nodes (linéaire ou non).
/// </summary>
public static class WorldCatalogService
{
    // Chemin Resources vers le JSON (sans extension)
    private const string CatalogPath = "Worlds/WorldCatalog";

    // Racine du JSON
    [System.Serializable]
    private class WorldCatalogRoot
    {
        public WorldEntry[] worlds;
    }

    /// <summary>
    /// Décrit un monde jouable.
    /// </summary>
    [System.Serializable]
    public class WorldEntry
    {
        public string worldId;        // Identifiant stable (ex: "W1")
        public string displayName;    // Nom affiché dans l'UI
        public string[] levelIds;     // Ordre canonique des niveaux
    }

    private static bool isLoaded;
    private static WorldCatalogRoot cached;

    /// <summary>
    /// Récupère un monde par son identifiant.
    /// </summary>
    public static bool TryGetWorld(string worldId, out WorldEntry world)
    {
        EnsureLoaded();

        if (cached == null || cached.worlds == null)
        {
            world = null;
            return false;
        }

        for (int i = 0; i < cached.worlds.Length; i++)
        {
            var w = cached.worlds[i];
            if (w != null && w.worldId == worldId)
            {
                world = w;
                return true;
            }
        }

        world = null;
        return false;
    }

    /// <summary>
    /// Charge le catalog une seule fois.
    /// </summary>
    private static void EnsureLoaded()
    {
        if (isLoaded)
            return;

        isLoaded = true;

        TextAsset json = Resources.Load<TextAsset>(CatalogPath);
        if (json == null)
        {
            Debug.LogError("[WorldCatalogService] WorldCatalog introuvable dans Resources/" + CatalogPath + ".json");
            cached = null;
            return;
        }

        cached = JsonUtility.FromJson<WorldCatalogRoot>(json.text);
        if (cached == null)
        {
            Debug.LogError("[WorldCatalogService] Parsing du WorldCatalog impossible.");
        }
    }

    /// <summary>
    /// Retourne le nom affiché d'un monde.
    /// Si worldId est invalide ou introuvable, retourne une chaîne vide.
    /// </summary>
    public static string GetWorldDisplayName(string worldId)
    {
        if (string.IsNullOrEmpty(worldId))
            return "";

        WorldEntry world;
        if (TryGetWorld(worldId, out world) && world != null)
            return string.IsNullOrEmpty(world.displayName) ? "" : world.displayName;

        return "";
    }

}
