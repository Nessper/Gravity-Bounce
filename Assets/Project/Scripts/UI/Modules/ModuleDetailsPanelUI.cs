using TMPro;
using UnityEngine;

/// <summary>
/// Panneau de detail reutilisable pour afficher les infos d un module.
/// </summary>
public class ModuleDetailsPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text detailsText;

    private string defaultText;

    public void SetDefaultText(string text)
    {
        defaultText = text;
    }

    public void ShowDefault()
    {
        if (detailsText != null)
            detailsText.text = defaultText;
    }

    public void ShowModule(ModuleDefinition def)
    {
        if (detailsText == null)
            return;

        detailsText.text = ModuleTextFormatter.BuildLocalizedDetails(def);
    }

    public void Clear()
    {
        if (detailsText != null)
            detailsText.text = string.Empty;
    }
}