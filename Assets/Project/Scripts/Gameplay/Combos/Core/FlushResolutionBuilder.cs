using UnityEngine;

public static class FlushResolutionBuilder
{
    public static FlushResolution BuildBase(BinSnapshot snapshot)
    {
        FlushResolution resolution = new FlushResolution();

        if (snapshot == null)
            return resolution;

        resolution.BinSource = snapshot.binSource;
        resolution.BinSide = snapshot.binSide;
        resolution.IsFinalFlush = snapshot.isFinalFlush;

        AddBalls(
            resolution,
            snapshot,
            "White");

        AddBalls(
            resolution,
            snapshot,
            "Blue");

        AddBalls(
            resolution,
            snapshot,
            "Red");

        AddBalls(
            resolution,
            snapshot,
            "Black");

        return resolution;
    }

    private static void AddBalls(
        FlushResolution resolution,
        BinSnapshot snapshot,
        string type)
    {
        if (snapshot.parType == null)
            return;

        if (snapshot.pointsParType == null)
            return;

        if (!snapshot.parType.TryGetValue(type, out int count))
            return;

        if (count <= 0)
            return;

        snapshot.pointsParType.TryGetValue(type, out int totalPoints);

        int perBall = 0;

        if (count > 0)
            perBall = Mathf.RoundToInt((float)totalPoints / count);

        for (int i = 0; i < count; i++)
        {
            resolution.AddBaseItem(type, perBall);
        }
    }
}