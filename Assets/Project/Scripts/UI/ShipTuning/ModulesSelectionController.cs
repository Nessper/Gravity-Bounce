using System;
using UnityEngine;

/// <summary>
/// Gère la sélection d'un module.
/// - Un seul module sélectionné à la fois.
/// - Toggle au re-clic.
/// - Notifie les listeners (UI, description, slots).
/// </summary>
public class ModulesSelectionController : MonoBehaviour
{
    /// <summary>
    /// Id du module actuellement sélectionné (null si aucun).
    /// </summary>
    public string SelectedModuleId { get; private set; }

    /// <summary>
    /// Event déclenché à chaque changement de sélection.
    /// Paramètre : moduleId sélectionné ou null.
    /// </summary>
    public event Action<string> OnSelectionChanged;

    /// <summary>
    /// Sélectionne un module (toggle).
    /// </summary>
    public void Select(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
        {
            Clear();
            return;
        }

        // Toggle : re-clic sur le même module
        if (SelectedModuleId == moduleId)
        {
            Clear();
            return;
        }

        SelectedModuleId = moduleId;

        Debug.Log($"[ModulesSelectionController] Select {moduleId} -> Selected={SelectedModuleId} instance={GetInstanceID()}", this);
        OnSelectionChanged?.Invoke(SelectedModuleId);
    }

    /// <summary>
    /// Désélectionne le module courant.
    /// </summary>
    public void Clear()
    {
        if (SelectedModuleId == null)
            return;

        SelectedModuleId = null;

        Debug.Log($"[ModulesSelectionController] Clear -> instance={GetInstanceID()}", this);
        OnSelectionChanged?.Invoke(null);
    }

    /// <summary>
    /// Force un rafraîchissement UI sans changer la sélection.
    /// Utile quand un événement de gameplay (ex: equip fail) doit mettre à jour
    /// le panneau description alors que SelectedModuleId ne change pas.
    /// </summary>
    public void Refresh()
    {
        OnSelectionChanged?.Invoke(SelectedModuleId);
    }
}
