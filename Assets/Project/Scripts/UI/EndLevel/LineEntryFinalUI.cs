using UnityEngine;
using TMPro;

/// <summary>
/// Variante "finale" d'une ligne label + valeur,
/// utilisée pour les scores importants (RawScore, FinalScore, ComboScore, etc.).
/// 
/// La logique de base est la même que LineEntryUI:
/// - label : texte statique (ex: "Score brut")
/// - value : texte de valeur (ex: "12 345")
/// 
/// La différence principale est d'intention :
/// ce composant est prévu pour recevoir des animations (AnimatedIntText, effets visuels),
/// donc il est séparé pour éviter de mélanger les usages.
/// </summary>
public class LineEntryFinalUI : MonoBehaviour
{
    [Header("UI Refs")]
    public TMP_Text label;
    public TMP_Text value;

    /// <summary>
    /// Définit instantanément le texte du label (facultatif).
    /// </summary>
    public void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    /// <summary>
    /// Définit instantanément le texte de la valeur (sans animation).
    /// </summary>
    public void SetValue(string text)
    {
        if (value != null)
            value.text = text;
    }

    /// <summary>
    /// Affiche immédiatement la ligne (label + valeur).
    /// Utile si le GameObject est désactivé par défaut dans la scène.
    /// </summary>
    public void ShowInstant()
    {
        gameObject.SetActive(true);

        if (label != null)
            label.gameObject.SetActive(true);

        if (value != null)
            value.gameObject.SetActive(true);
    }
}
