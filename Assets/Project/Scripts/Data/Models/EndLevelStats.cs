using System.Collections.Generic;

[System.Serializable]
public class EndLevelStats
{
    public int totalPrevues;
    public int totalCollectees;
    public int totalPerdues;
    public int scoreFinal;
    public int pointsPerdus;

    public Dictionary<string, int> collecteesParType;
    public Dictionary<string, int> perduesParType;
}
