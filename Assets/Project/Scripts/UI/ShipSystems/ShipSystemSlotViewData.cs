using System;
using UnityEngine;

/// <summary>
/// Donnees d affichage d un slot dans Ship Systems.
///
/// Cette classe est purement UI / presentation.
/// Elle ne contient aucune logique metier.
/// </summary>
[Serializable]
public class ShipSystemSlotViewData
{
    public int slotIndex;
    public bool isLocked;
    public string moduleId;
    public ModuleDefinition moduleDefinition;
    public Sprite moduleIcon;

    public bool HasModule
    {
        get { return moduleDefinition != null; }
    }

    public bool IsEmptyUnlocked
    {
        get { return !isLocked && moduleDefinition == null; }
    }
}