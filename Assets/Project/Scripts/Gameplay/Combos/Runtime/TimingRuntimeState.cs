using System;

public class TimingRuntimeState
{
    public bool IsWindowActive { get; private set; }

    public float WindowDuration { get; private set; }
    public float WindowRemaining { get; private set; }

    public float LastFlushTimestamp { get; private set; } = -1f;

    public event Action<float> OnTimingWindowStarted;
    public event Action<float> OnTimingWindowUpdated;
    public event Action OnTimingWindowConsumed;
    public event Action OnTimingWindowExpired;
    public event Action OnTimingWindowReset;

    public void StartWindow(
        float duration,
        float currentTime)
    {
        WindowDuration = duration;
        WindowRemaining = duration;

        LastFlushTimestamp = currentTime;

        IsWindowActive = true;

        OnTimingWindowStarted?.Invoke(duration);
        OnTimingWindowUpdated?.Invoke(WindowRemaining);
    }

    public void Update(
        float currentTime)
    {
        if (!IsWindowActive)
            return;

        float elapsed =
            currentTime - LastFlushTimestamp;

        WindowRemaining =
            Math.Max(0f, WindowDuration - elapsed);

        OnTimingWindowUpdated?.Invoke(WindowRemaining);

        if (WindowRemaining <= 0f)
        {
            IsWindowActive = false;

            OnTimingWindowExpired?.Invoke();
        }
    }

    public void Consume(
        float currentTime)
    {
        OnTimingWindowConsumed?.Invoke();

        LastFlushTimestamp = currentTime;
    }

    public void Reset()
    {
        IsWindowActive = false;

        WindowDuration = 0f;
        WindowRemaining = 0f;

        LastFlushTimestamp = -1f;

        OnTimingWindowReset?.Invoke();
    }
}