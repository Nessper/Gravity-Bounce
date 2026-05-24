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
    // Compat UI / logs
    public BinSide binSide = BinSide.None;

    // Legacy compat
    public string binSource => binSide.ToString();

    // Runtime
    public float timestamp;

    // Global flush stats
    public int nombreDeBilles;
    public int totalPointsDuLot;

    // BallId -> count
    // ex:
    // "white" => 12
    // "black" => 3
    public Dictionary<string, int> parBallId =
        new Dictionary<string, int>();

    // BallId -> total points
    // ex:
    // "white" => 1200
    // "black" => -360
    public Dictionary<string, int> pointsParBallId =
        new Dictionary<string, int>();

    // Phase info
    public int phaseIndex1Based = 0;

    // EndLevel special flush
    public bool isFinalFlush;
}