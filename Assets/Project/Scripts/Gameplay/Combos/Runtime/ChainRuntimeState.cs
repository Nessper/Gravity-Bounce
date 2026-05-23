using System;

public enum ChainColor
{
    White,
    Blue,
    Red
}

[Serializable]
public class ChainProgress
{
    public ChainColor Color;
    public int CurrentBalls;
    public int StepBalls;
    public int AwardedLevel;

    public int CurrentLevel => StepBalls > 0 ? CurrentBalls / StepBalls : 0;
    public int ProgressInCurrentLevel => StepBalls > 0 ? CurrentBalls % StepBalls : 0;
    public int RemainingToNextLevel => StepBalls > 0 ? StepBalls - ProgressInCurrentLevel : 0;

    public ChainProgress(
        ChainColor color,
        int stepBalls)
    {
        Color = color;
        StepBalls = stepBalls;
        CurrentBalls = 0;
        AwardedLevel = 0;
    }

    public void AddBalls(int count)
    {
        if (count <= 0)
            return;

        CurrentBalls += count;
    }

    public void Reset()
    {
        CurrentBalls = 0;
        AwardedLevel = 0;
    }
}

public class ChainRuntimeState
{
    public ChainProgress White { get; } =
        new ChainProgress(ChainColor.White, 10);

    public ChainProgress Blue { get; } =
        new ChainProgress(ChainColor.Blue, 8);

    public ChainProgress Red { get; } =
        new ChainProgress(ChainColor.Red, 6);

    public event Action<ChainProgress> OnChainProgressChanged;
    public event Action<ChainProgress, int> OnChainLevelReached;
    public event Action OnChainsReset;

    public ChainProgress Get(ChainColor color)
    {
        switch (color)
        {
            case ChainColor.White:
                return White;

            case ChainColor.Blue:
                return Blue;

            case ChainColor.Red:
                return Red;

            default:
                return White;
        }
    }

    public void AddProgress(
        ChainColor color,
        int count)
    {
        ChainProgress progress = Get(color);

        int previousLevel = progress.CurrentLevel;

        progress.AddBalls(count);

        int newLevel = progress.CurrentLevel;

        OnChainProgressChanged?.Invoke(progress);

        if (newLevel > previousLevel)
            OnChainLevelReached?.Invoke(progress, newLevel);
    }

    public void MarkAwarded(
        ChainColor color,
        int level)
    {
        ChainProgress progress = Get(color);

        if (level > progress.AwardedLevel)
            progress.AwardedLevel = level;
    }

    public void ResetAll()
    {
        White.Reset();
        Blue.Reset();
        Red.Reset();

        OnChainsReset?.Invoke();

        OnChainProgressChanged?.Invoke(White);
        OnChainProgressChanged?.Invoke(Blue);
        OnChainProgressChanged?.Invoke(Red);
    }
}