using System;
using System.Collections.Generic;

[Serializable]
public class ShipModuleSlotLayout
{
    public int slotIndex;
    public float normalizedX;
    public float normalizedY;
}

[Serializable]
public class ShipDefinition
{
    public string id;

    public string displayNameLocKey;
    public string descriptionLocKey;

    public string imagePath;
    public string imagePathInterior;

    public int baseHull;
    public float baseLevelDurationSec;

    public int totalModuleSlots;
    public int startingUnlockedModuleSlots;
    
    public int startingMoney;

    public List<string> startingEquippedModuleIds;
    public List<ShipModuleSlotLayout> moduleSlotLayouts;

    public int sortOrder;

    public bool isUnlockedByDefault;
    public bool isHidden;
}

[Serializable]
public class ShipCatalog
{
    public string schema;
    public List<ShipDefinition> ships;
}