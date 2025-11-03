using UnityEngine;

public class ComboStyleProvider : MonoBehaviour
{
    [SerializeField] private ComboStyleCatalog catalog;

    public (string line, Color color) Build(string comboId, int points)
    {
        var s = catalog != null ? catalog.Get(comboId) : null;
        if (s == null)
            return (comboId + " +" + points, Color.white);

        var label = Localize(s.displayKey);
        var line = s.format;
        if (!string.IsNullOrEmpty(line))
        {
            line = line.Replace("{label}", label ?? "");
            line = line.Replace("{points}", points.ToString());
            if (line.Contains("{points}"))
            {
                Debug.LogWarning("[ComboStyleProvider] Placeholder {points} non remplacé. Vérifie le champ 'format' dans l'asset (accollades exactes).");
            }
        }
        else
        {
            line = (label ?? comboId) + " +" + points;
        }

        return (line, s.color);
    }

    private string Localize(string key)
    {
        // Mini-dico provisoire (tu peux enlever ça si tu mets le texte final directement en displayKey)
        switch (key)
        {
            case "combo.white_streak": return "White Streak";
            case "combo.blue_rush": return "Blue Rush";
            case "combo.red_storm": return "Red Storm";
            case "combo.fast_flush": return "Fast Flush";
        }
        // Sinon on renvoie la clé telle quelle (comportement actuel)
        return key;
    }
}
