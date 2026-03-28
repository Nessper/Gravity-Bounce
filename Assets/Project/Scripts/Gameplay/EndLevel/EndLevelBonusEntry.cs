/// <summary>
/// Source d'une ligne de bonus de fin de niveau.
/// Permet de distinguer, si besoin plus tard :
/// - les final combos
/// - les modules
/// - d'autres sources futures
/// </summary>
public enum EndLevelBonusSource
{
    FinalCombo,
    Module
}

/// <summary>
/// Représente une ligne de bonus de score affichable
/// dans la section bonus de la cérémonie de fin.
///
/// Cette structure est volontairement simple :
/// - id : identifiant logique / debug
/// - label : texte affiché dans l'UI
/// - points : delta de score (positif ou négatif)
/// - source : origine de la ligne
///
/// IMPORTANT :
/// - Cette structure ne gère aucune logique métier.
/// - Elle sert uniquement de format commun entre
///   l'évaluation métier et l'affichage UI.
/// </summary>
public struct EndLevelBonusEntry
{
    public string id;
    public string label;
    public int points;
    public EndLevelBonusSource source;

    public EndLevelBonusEntry(string id, string label, int points, EndLevelBonusSource source)
    {
        this.id = id;
        this.label = label;
        this.points = points;
        this.source = source;
    }
}