using System;
using System.Collections.Generic;

[Serializable]
public class RunPlan
{
    public string worldId;
    public List<RunNode> nodes = new List<RunNode>();

    // Convention:
    // - currentIndex = index du node A JOUER MAINTENANT
    // - 0..Count-1 => jouable
    // - Count      => run terminee (completed)
    public int currentIndex = 0;

    public bool HasNodes
    {
        get { return nodes != null && nodes.Count > 0; }
    }

    public int NodeCount
    {
        get { return HasNodes ? nodes.Count : 0; }
    }

    // Index jouable (node courant existant)
    public bool IsPlayableIndex
    {
        get { return HasNodes && currentIndex >= 0 && currentIndex < nodes.Count; }
    }

    // Run terminee (index == Count)
    public bool IsCompleted
    {
        get { return HasNodes && currentIndex == nodes.Count; }
    }

    // Index dans une plage acceptable (0..Count) pour notre convention.
    // Utile pour des asserts / sanity checks.
    public bool IsIndexInRange
    {
        get { return HasNodes && currentIndex >= 0 && currentIndex <= nodes.Count; }
    }

    public RunNode CurrentPlayableNode
    {
        get { return IsPlayableIndex ? nodes[currentIndex] : null; }
    }
}
