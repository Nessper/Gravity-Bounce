using TMPro;
using UnityEngine;

/// <summary>
/// Vue reutilisable pour le bloc Ship Status.
///
/// Cette classe ne contient aucune logique metier.
/// Elle expose uniquement les references UI du panneau
/// et quelques helpers simples pour les controllers de contexte.
///
/// Objectif :
/// - mutualiser le layout / prefab
/// - garder la logique actuelle dans ShipSelectController, RunHub, LevelBriefing
/// - eviter un gros refactor brutal
/// </summary>
public class ShipStatusPanelUI : MonoBehaviour
{
    [Header("Optional Roots")]
    [SerializeField] private GameObject descriptionRoot;
    [SerializeField] private GameObject tuningButtonRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text openSlotsText;
    [SerializeField] private TMP_Text noModulesText;

    [Header("Modules")]
    [SerializeField] private Transform equippedModulesRoot;
    [SerializeField] private ModuleDetailsPanelUI moduleDetailsPanel;

    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text HullText => hullText;
    public TMP_Text DurationText => durationText;
    public TMP_Text MoneyText => moneyText;
    public TMP_Text OpenSlotsText => openSlotsText;
    public TMP_Text NoModulesText => noModulesText;

    public Transform EquippedModulesRoot => equippedModulesRoot;
    public ModuleDetailsPanelUI ModuleDetailsPanel => moduleDetailsPanel;

    public void SetDescriptionVisible(bool visible)
    {
        if (descriptionRoot != null)
            descriptionRoot.SetActive(visible);
    }

    public void SetTuningVisible(bool visible)
    {
        if (tuningButtonRoot != null)
            tuningButtonRoot.SetActive(visible);
    }

    public void SetDescriptionText(string value)
    {
        if (descriptionText != null)
            descriptionText.text = value ?? string.Empty;
    }

    public void SetHullText(string value)
    {
        if (hullText != null)
            hullText.text = value ?? string.Empty;
    }

    public void SetDurationText(string value)
    {
        if (durationText != null)
            durationText.text = value ?? string.Empty;
    }

    public void SetMoneyText(string value)
    {
        if (moneyText != null)
            moneyText.text = value ?? string.Empty;
    }

    public void SetOpenSlotsText(string value)
    {
        if (openSlotsText != null)
            openSlotsText.text = value ?? string.Empty;
    }

    public void SetNoModulesVisible(bool visible, string text = null)
    {
        if (noModulesText == null)
            return;

        noModulesText.gameObject.SetActive(visible);

        if (visible && text != null)
            noModulesText.text = text;
    }
}