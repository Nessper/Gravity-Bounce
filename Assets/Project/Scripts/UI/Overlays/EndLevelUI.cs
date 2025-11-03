using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class EndLevelUI : MonoBehaviour
{
    [Header("Textes principaux")]
    [SerializeField] private TMP_Text objectifText;

    [Header("Détails")]
    [SerializeField] private TMP_Text collecteesText;  // multiline
    [SerializeField] private TMP_Text perduesText;     // multiline
    [SerializeField] private TMP_Text scoreText;       // optionnel

    public void DisplayResult(bool success, float achievedPercent, float targetPercent, EndLevelStats stats)
    {
        if (objectifText)
        {
            string msg = success
                ? $"Niveau réussi ! ({achievedPercent:0.0}% -> {targetPercent}%)"
                : $"Niveau échoué. {achievedPercent:0.0}% atteint sur {targetPercent}% requis.";
            objectifText.text = msg;
        }

        if (collecteesText)
            collecteesText.text = BuildLines("Collectées", stats.collecteesParType, stats.totalCollectees);

        if (perduesText)
        {
            // On peut afficher les points perdus totaux
            var header = $"Perdues (total {stats.totalPerdues})\n";
            var body = BuildLines(null, stats.perduesParType, stats.totalPerdues, prefixHeader: false);
            perduesText.text = header + body + $"\nPoints perdus: {stats.pointsPerdus}";
        }

        if (scoreText)
            scoreText.text = $"Score final: {stats.scoreFinal}";
    }

    public void Clear()
    {
        if (objectifText) objectifText.text = "";
        if (collecteesText) collecteesText.text = "";
        if (perduesText) perduesText.text = "";
        if (scoreText) scoreText.text = "";
    }

    // Utilitaire pour formatter "type: count"
    private string BuildLines(string header, Dictionary<string, int> dict, int total, bool prefixHeader = true)
    {
        var sb = new StringBuilder();
        if (prefixHeader && !string.IsNullOrEmpty(header))
            sb.AppendLine($"{header} (total {total})");

        if (dict == null || dict.Count == 0)
        {
            sb.AppendLine("- (aucune)");
            return sb.ToString();
        }

        foreach (var kv in dict)
        {
            // Affiche "White: 12", "Blue: 3", etc.
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        }
        return sb.ToString();
    }
}
