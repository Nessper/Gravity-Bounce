using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ComboStyle
{
    public string id;           // ex: "WhiteStreak"
    public string displayKey;   // ex: "combo.white_streak"
    public string format = "{label} +{points}";
    public Color color = Color.white;
}

[CreateAssetMenu(menuName = "GravityBounce/Combo Style Catalog")]
public class ComboStyleCatalog : ScriptableObject
{
    public List<ComboStyle> entries = new List<ComboStyle>();
    private Dictionary<string, ComboStyle> map;

    public ComboStyle Get(string id)
    {
        if (map == null)
        {
            map = new Dictionary<string, ComboStyle>();
            foreach (var e in entries) if (!string.IsNullOrEmpty(e.id)) map[e.id] = e;
        }
        return map.TryGetValue(id, out var s) ? s : null;
    }
}
