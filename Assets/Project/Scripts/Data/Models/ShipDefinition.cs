using System;
using System.Collections.Generic;

[Serializable]
public class ShipDefinition
{
    public string id;
    public string displayName;
    public int lives;
    public float shieldSecondsPerLevel;
    public float closeBinHoldGrace;
    public float paddleWidthMult;
    public bool binAutoFlushOnEvac;
}

[Serializable]
public class ShipCatalog
{
    public string schema;
    public List<ShipDefinition> ships;
}
