using UnityEngine;

public class ComboStyleProvider : MonoBehaviour
{
    [SerializeField] private ComboStyleCatalog catalog;

    public (string line, Color color) Build(string comboId, int points)
    {
        // 1) Extraire multiplicateur depuis un suffixe xN éventuel
        int mult = 1;
        string baseId = comboId;
        {
            int xIndex = comboId.LastIndexOf('x');
            if (xIndex > 0 && xIndex < comboId.Length - 1)
            {
                string suf = comboId.Substring(xIndex + 1);
                if (int.TryParse(suf, out int m) && m >= 1)
                {
                    mult = m;
                    baseId = comboId.Substring(0, xIndex); // ex: "WhiteFlushChainx3" -> "WhiteFlushChain"
                }
            }
        }

        // 2) Résolution via catalog sur l'id de base
        var s = catalog != null ? catalog.Get(baseId) : null;
        if (s == null)
            return (comboId + " +" + points, Color.white);

        // 3) Localisation + format
        var label = Localize(s.displayKey);
        var line = s.format;

        if (string.IsNullOrEmpty(line))
            line = (label ?? baseId) + " +" + points;
        else
        {
            line = line.Replace("{label}", label ?? "");
            line = line.Replace("{points}", points.ToString());
            line = line.Replace("{mult}", mult.ToString());

            // Option: masquer "x1" proprement si format contient "x{mult}"
            if (mult == 1)
            {
                line = line.Replace(" x1", "");   // espace + x1 -> rien
                line = line.Replace("x1", "");    // fallback si pas d'espace
                line = System.Text.RegularExpressions.Regex.Replace(line, @"\s{2,}", " "); // nettoie doubles espaces
                line = line.Trim();
            }

            if (line.Contains("{points}") || line.Contains("{mult}"))
                Debug.LogWarning("[ComboStyleProvider] Placeholder non remplacé. Vérifie 'format'.");
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
            case "combo.white_chain": return "White Chain";
            case "combo.blue_chain": return "Blue Chain";
            case "combo.red_chain": return "Red Chain";
            case "combo.super_flush": return "Super Flush";
            case "combo.ultra_flush": return "Ultra Flush";
            case "combo.monster_flush": return "Monster Flush";

            // === FINAUX (invisibles en partie, visibles à la fin) ===
            case "combo.perfect_run": return "Perfect Run";
            case "combo.combos_collector": return "Combos Collector";


        }
        // Sinon on renvoie la clé telle quelle (comportement actuel)
        return key;
    }
}
