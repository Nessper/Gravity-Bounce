using UnityEngine;

public static class ShipCatalogService
{
    // Source unique : assignée par Bootstrapper depuis Resources.
    public static ShipCatalog Catalog;

    public static ShipDefinition GetById(string id)
    {
        if (Catalog == null || Catalog.ships == null || Catalog.ships.Count == 0)
        {
            Debug.LogWarning("[ShipCatalogService] Catalog non charge ou vide.");
            return null;
        }

        if (string.IsNullOrEmpty(id))
            return null;

        return Catalog.ships.Find(s => s.id == id);
    }
}
