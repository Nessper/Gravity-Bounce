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

        if (snapshot.parBallId == null || snapshot.pointsParBallId == null)
            return resolution;

        foreach (var kv in snapshot.parBallId)
        {
            AddBalls(
                resolution,
                snapshot,
                kv.Key
            );
        }

        return resolution;
    }

    private static void AddBalls(
        FlushResolution resolution,
        BinSnapshot snapshot,
        string ballId)
    {
        if (string.IsNullOrWhiteSpace(ballId))
            return;

        if (!snapshot.parBallId.TryGetValue(ballId, out int count))
            return;

        if (count <= 0)
            return;

        snapshot.pointsParBallId.TryGetValue(ballId, out int totalPoints);

        int perBall = Mathf.RoundToInt((float)totalPoints / count);

        for (int i = 0; i < count; i++)
            resolution.AddBaseItem(ballId, perBall);
    }
}