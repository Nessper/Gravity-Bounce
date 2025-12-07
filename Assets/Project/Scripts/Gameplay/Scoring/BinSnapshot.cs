using System.Collections.Generic;

public enum BinSide
{
    Left,
    Right,
    None
}

[System.Serializable]
public class BinSnapshot
{
    public BinSide binSide = BinSide.None;  // << plus sûr que du texte libre
    public string binSource => binSide.ToString();  // compatibilité avec l’ancien code

    public float timestamp;
    public int nombreDeBilles;
    public int totalPointsDuLot;

    public Dictionary<string, int> parType = new Dictionary<string, int>();
    public Dictionary<string, int> pointsParType = new Dictionary<string, int>();

    public bool isFinalFlush; // flag pour signaler que c'est le flush de fin
}
