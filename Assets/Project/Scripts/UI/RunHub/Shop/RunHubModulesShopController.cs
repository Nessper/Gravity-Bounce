using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controle l affichage du bloc shop modules dans le RunHub.
///
/// Responsabilites :
/// - demander au ModulesHubController les modules visibles
/// - envoyer la liste au ModulesListPanelUI
/// - gerer le refresh global du panneau
///
/// Le rendu des items et le hover details sont delegues a ModulesListPanelUI.
/// </summary>
public class RunHubModulesShopController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ModulesHubController modulesHub;

    [Header("UI")]
    [SerializeField] private ModulesListPanelUI modulesListPanel;

    [Header("Localization")]
    [SerializeField] private string emptyTextKey = "shop.modules.none";
    [SerializeField] private string defaultHoverTextKey = "ship_select.modules.hover_for_details";

    private const string UiPackName = "ui";

    private void Awake()
    {
        if (modulesHub == null)
        {
            Debug.LogError("[RunHubModulesShopController] modulesHub n est pas assigne.");
            enabled = false;
            return;
        }

        if (modulesListPanel == null)
        {
            Debug.LogError("[RunHubModulesShopController] modulesListPanel n est pas assigne.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        modulesHub.OnModulesCollectionChanged += RefreshUI;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (modulesHub != null)
            modulesHub.OnModulesCollectionChanged -= RefreshUI;
    }

    /// <summary>
    /// Rafraichit integralement le bloc shop.
    /// </summary>
    public void RefreshUI()
    {
        List<ModuleDefinition> modules = null;

        bool ok = modulesHub.TryGetShopVisibleModules(out modules);
        if (!ok || modules == null)
            modules = new List<ModuleDefinition>();

        string emptyText = ResolveUiText(emptyTextKey);
        string defaultHoverText = ResolveUiText(defaultHoverTextKey);

        modulesListPanel.ShowModules(modules, emptyText, defaultHoverText);
    }

    private string ResolveUiText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (LocalizationManager.Instance == null)
            return key;

        return LocalizationManager.Instance.GetTextOrKey(UiPackName, key);
    }
}