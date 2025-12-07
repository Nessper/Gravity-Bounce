using System;
using System.Collections.Generic;

[Serializable]
public class ShipDefinition
{
    public string id;
    public string displayName;
    public string description;    
    public int maxHull;
    public float shieldSecondsPerLevel;
    public float closeBinHoldGrace;
    public float paddleWidthMult;
    public bool binAutoFlushOnEvac;
    public string imageFile;       
}


[Serializable]
public class ShipCatalog
{
    public string schema;
    public List<ShipDefinition> ships;
}
