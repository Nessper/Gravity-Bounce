using System;
using System.IO;
using UnityEngine;

public static class ShipCatalogService
{
    public static ShipCatalog Catalog;

    public static void LoadFromStreamingAssets()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Ships", "ShipCatalog.json");

        if (!File.Exists(path))
            throw new Exception("ShipCatalog.json not found at " + path);

        string json = File.ReadAllText(path);
        Catalog = JsonUtility.FromJson<ShipCatalog>(json);

        if (Catalog == null || Catalog.ships == null || Catalog.ships.Count == 0)
            throw new Exception("ShipCatalog invalid or empty");

        Debug.Log($"[ShipCatalogService] Loaded {Catalog.ships.Count} ships from catalog.");
    }

    public static ShipDefinition GetById(string id)
    {
        if (Catalog == null || Catalog.ships == null)
        {
            Debug.LogWarning("[ShipCatalogService] Catalog not loaded.");
            return null;
        }

        return Catalog.ships.Find(s => s.id == id);
    }
}
