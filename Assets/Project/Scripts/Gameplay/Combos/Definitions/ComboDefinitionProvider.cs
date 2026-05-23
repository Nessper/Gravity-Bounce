using UnityEngine;

public class ComboDefinitionProvider : MonoBehaviour
{
    public static ComboDefinitionProvider Instance { get; private set; }

    [SerializeField] private ComboDefinitionCatalog catalog;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public ComboDefinition Get(string id)
    {
        if (catalog == null)
        {
            Debug.LogWarning(
                "[ComboDefinitionProvider] Missing catalog.");

            return null;
        }

        ComboDefinition definition =
            catalog.Get(id);

        if (definition == null)
        {
            Debug.LogWarning(
                $"[ComboDefinitionProvider] Missing definition for '{id}'.");
        }

        return definition;
    }
}