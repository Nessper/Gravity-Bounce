using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ComboDefinitionCatalog",
    menuName = "404/Combos/Combo Definition Catalog")]
public class ComboDefinitionCatalog : ScriptableObject
{
    [SerializeField]
    private List<ComboDefinition> definitions =
        new List<ComboDefinition>();

    private Dictionary<string, ComboDefinition> map;

    public ComboDefinition Get(string id)
    {
        if (map == null)
            BuildMap();

        if (string.IsNullOrEmpty(id))
            return null;

        return map.TryGetValue(id, out ComboDefinition definition)
            ? definition
            : null;
    }

    private void BuildMap()
    {
        map = new Dictionary<string, ComboDefinition>();

        for (int i = 0; i < definitions.Count; i++)
        {
            ComboDefinition definition = definitions[i];

            if (definition == null)
                continue;

            if (string.IsNullOrEmpty(definition.Id))
                continue;

            map[definition.Id] = definition;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        map = null;
    }
#endif
}