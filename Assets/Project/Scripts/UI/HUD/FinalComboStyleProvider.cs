using UnityEngine;

/// <summary>
/// Fournit les libellés lisibles pour les combos finaux,
/// à partir de leurs identifiants techniques.
/// 
/// Rôle :
/// - Découpler complètement LevelManager (gameplay) de la couche UI / texte.
/// - Centraliser les labels des combos de fin de niveau.
/// 
/// Utilisation prévue :
/// - Référencé dans EndLevelUI (champ [SerializeField] FinalComboStyleProvider).
/// - EndLevelUI lui passe l'id (ex: "PerfectRun") et récupère "Perfect run".
/// </summary>
[CreateAssetMenu(
    fileName = "FinalComboStyleProvider",
    menuName = "VoidScrappers/Combos/FinalComboStyleProvider")]
public class FinalComboStyleProvider : ScriptableObject
{
    /// <summary>
    /// Retourne le label lisible pour un combo final
    /// à partir de son identifiant interne.
    /// 
    /// Exemple :
    /// - "PerfectRun"       -> "Perfect run"
    /// - "CombosCollector"  -> "Combos collector"
    /// - "WhiteMaster"      -> "White master"
    /// 
    /// Si l'id n'est pas connu, on renvoie l'id brut
    /// pour faciliter le debug.
    /// </summary>
    public string GetLabel(string comboId)
    {
        switch (comboId)
        {
            case "PerfectRun":
                return "Perfect run";

            case "CombosCollector":
                return "Combos collector";

            case "NoBlackCollected":
                return "No black collected";

            case "MaxChainBonus":
                return "Max chain bonus";

            case "ComboDiversity":
                return "Combo diversity";

            case "ColorTrinity":
                return "Color trinity";

            case "ChainDuo":
                return "Chain duo";

            case "FastFinisher":
                return "Fast finisher";

            case "ClutchFinisher":
                return "Clutch finisher";

            case "JustInTime":
                return "Just in time";

            default:
                // Fallback : id brut pour faciliter le debug
                return comboId;
        }
    }
}
