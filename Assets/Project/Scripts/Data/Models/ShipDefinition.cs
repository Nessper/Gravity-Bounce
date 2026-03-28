using System;
using System.Collections.Generic;

[Serializable]
public class ShipDefinition
{
    public string id;
    public string displayName;
    public string description;    
    public int maxHull;
    public float levelDurationSec;
    public float closeBinHoldGrace;
    public float paddleWidthMult;
    public bool binAutoFlushOnEvac;
    public string imageFile;
    public int unlockedModuleSlots; // ex: 3 => slots 0,1,2 ouverts ; 3,4,5 fermés
    public bool debugOnly; 
}


[Serializable]
public class ShipCatalog
{
    public string schema;
    public List<ShipDefinition> ships;
}
