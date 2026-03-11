using UnityEngine;

/// <summary>
/// Service statique de chargement du LevelCatalog depuis Resources.
/// 
/// Rôle :
/// - Fournir les métadonnées UI d'un niveau (worldId, title, gameplayPath).
/// - Le nom affiché du monde est résolu via WorldCatalog (source de vérité).
/// </summary>
public static class LevelCatalogService
{
    /// <summary>
    /// Chemin relatif à Resources/ (sans extension).
    /// Ex : Resources/Levels/LevelCatalog.json -> "Levels/LevelCatalog"
    /// </summary>
    private const string CatalogPath = "Levels/LevelCatalog";

    [System.Serializable]
    private class LevelCatalogRoot
    {
        public LevelCatalogEntry[] levels;
    }

    /// <summary>
    /// Métadonnées d'un niveau.
    /// - levelId : identifiant unique (ex : W1-L1)
    /// - worldId : identifiant du monde (ex : W1)
    /// - title : titre du niveau (UI)
    /// - gameplayPath : chemin Resources vers le JSON gameplay (sans extension)
    /// </summary>
    [System.Serializable]
    public class LevelCatalogEntry
    {
        public string levelId;
        public string worldId;
        public string title;
        public string gameplayPath;
    }

    private static bool isLoaded;
    private static LevelCatalogRoot cached;

    /// <summary>
    /// Retourne l'entrée du catalog pour un levelId.
    /// </summary>
    public static bool TryGet(string levelId, out LevelCatalogEntry entry)
    {
        EnsureLoaded();

        if (cached == null || cached.levels == null)
        {
            entry = null;
            return false;
        }

        for (int i = 0; i < cached.levels.Length; i++)
        {
            LevelCatalogEntry e = cached.levels[i];
            if (e != null && e.levelId == levelId)
            {
                entry = e;
                return true;
            }
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// Charge le catalog une seule fois depuis Resources.
    /// </summary>
    private static void EnsureLoaded()
    {
        if (isLoaded)
            return;

        isLoaded = true;

        TextAsset json = Resources.Load<TextAsset>(CatalogPath);
        if (json == null)
        {
            Debug.LogError("[LevelCatalogService] LevelCatalog introuvable a Resources/" + CatalogPath + ".json");
            cached = null;
            return;
        }

        cached = JsonUtility.FromJson<LevelCatalogRoot>(json.text);
        if (cached == null)
        {
            Debug.LogError("[LevelCatalogService] Parsing LevelCatalog impossible.");
        }
    }
}
