using System;

/// <summary>
/// Node générique d'une run.
/// Un node représente "une étape" dans le plan.
/// </summary>
[Serializable]
public class RunNode
{
    /// <summary>Identifiant stable du node (ex: W1_N1).</summary>
    public string nodeId;

    /// <summary>Type du node (Level, Shop, etc.).</summary>
    public RunNodeType type;

    /// <summary>
    /// Pour un node de type Level, référence le levelId (ex: W1-L1).
    /// Pour d'autres types, ce champ peut être vide.
    /// </summary>
    public string levelId;
}
