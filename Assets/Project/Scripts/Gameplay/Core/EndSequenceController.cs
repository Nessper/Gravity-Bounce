using System;
using System.Collections;
using UnityEngine;

public class EndSequenceController : MonoBehaviour
{
    [SerializeField] private BallTracker ballTracker;
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private PauseController pauseController;

    [SerializeField] private float endGraceWindow = 0.25f;
    [SerializeField] private float pollIntervalSec = 1.0f;

    private Coroutine co;
    private bool finalFlushDone;

    public void Configure(BallTracker t, BinCollector c, PlayerController p, CloseBinController cb, PauseController pc, float grace = -1f, float poll = -1f)
    {
        ballTracker = t; collector = c; player = p; closeBinController = cb; pauseController = pc;
        if (grace >= 0f) endGraceWindow = grace;
        if (poll > 0f) pollIntervalSec = poll;
    }

    public void ResetState()
    {
        finalFlushDone = false;
        if (co != null) { StopCoroutine(co); co = null; }
    }

    public void BeginEndSequence(Action onCompleted)
    {
        if (co == null) co = StartCoroutine(Run(onCompleted));
    }

    private IEnumerator Run(Action done)
    {
        if (endGraceWindow > 0f)
        {
            if (collector != null && collector.IsAnyFlushActive)
                yield return new WaitUntil(() => !collector.IsAnyFlushActive);
            yield return new WaitForSeconds(endGraceWindow);
        }

        while (true)
        {
            bool flushActive = collector != null && collector.IsAnyFlushActive;
            bool allHandled = ballTracker == null || ballTracker.AllBallsInBinOrCollected();
            if (!flushActive && allHandled) break;
            yield return new WaitForSeconds(pollIntervalSec);
        }

        if (collector != null && collector.IsAnyFlushActive)
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);

        if (!finalFlushDone && collector != null)
        {
            collector.CollectAll(force: true, skipDelay: false);
            finalFlushDone = true;
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        pauseController?.EnablePause(false);
        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);

        done?.Invoke();
        co = null;
    }
}
