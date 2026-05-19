using UnityEngine;
using System;

/// <summary>
/// Mouvement moteur additif du vaisseau.
/// 
/// Objectif:
/// - casser l'impression statique
/// - simuler des phases de poussee moteur
/// - ne jamais ecraser la position reelle du motionRoot
///
/// A placer sur GameFeelRoot.
/// Cible recommandee: ShipMotionRoot.
/// </summary>
public class ShipEngineSwayController : MonoBehaviour
{
    private enum EngineState
    {
        Waiting,
        Accelerating,
        Holding,
        Releasing
    }

    [Header("Target")]
    [SerializeField] private Transform motionRoot;

    [Header("Idle Sway")]
    [SerializeField] private bool enableIdle = true;
    [SerializeField] private float idleVerticalAmplitude = 0.012f;
    [SerializeField] private float idleHorizontalAmplitude = 0.003f;
    [SerializeField] private float idleVerticalSpeed = 0.55f;
    [SerializeField] private float idleHorizontalSpeed = 0.35f;

    [Header("Engine Push Timing")]
    [SerializeField] private bool enablePush = true;
    [SerializeField] private float waitMin = 2.5f;
    [SerializeField] private float waitMax = 6.5f;
    [SerializeField] private float holdMin = 0.45f;
    [SerializeField] private float holdMax = 1.25f;

    [Header("Engine Push Motion")]
    [SerializeField] private float pushOffsetY = 0.12f;
    [SerializeField] private float accelerationSmoothTime = 0.22f;
    [SerializeField] private float releaseSmoothTime = 0.55f;

    [Header("Safety")]
    [SerializeField] private bool removeOffsetOnDisable = true;

    public event Action OnEnginePushStarted;

    private Vector3 lastAppliedOffset;

    private float idleSeedX;
    private float idleSeedY;

    private EngineState state;
    private float stateTimer;

    private float pushCurrentY;
    private float pushVelocityY;

    private void Awake()
    {
        if (motionRoot == null)
        {
            Debug.LogWarning("[ShipEngineSwayController] motionRoot non assigne.");
            enabled = false;
            return;
        }

        idleSeedX = UnityEngine.Random.Range(0f, 100f);
        idleSeedY = UnityEngine.Random.Range(0f, 100f);

        EnterWaiting();
    }

    private void OnDisable()
    {
        if (!removeOffsetOnDisable)
            return;

        RemoveLastOffset();
    }

    private void LateUpdate()
    {
        if (motionRoot == null)
            return;

        RemoveLastOffset();

        UpdateEnginePush();

        Vector3 newOffset = Vector3.zero;

        if (enableIdle)
        {
            newOffset.x += GetIdleX();
            newOffset.y += GetIdleY();
        }

        newOffset.y += pushCurrentY;

        motionRoot.localPosition += newOffset;
        lastAppliedOffset = newOffset;
    }

    private void RemoveLastOffset()
    {
        if (motionRoot == null)
            return;

        if (lastAppliedOffset == Vector3.zero)
            return;

        motionRoot.localPosition -= lastAppliedOffset;
        lastAppliedOffset = Vector3.zero;
    }

    private float GetIdleX()
    {
        float t = Time.time * idleHorizontalSpeed + idleSeedX;
        return Mathf.Sin(t) * idleHorizontalAmplitude;
    }

    private float GetIdleY()
    {
        float t = Time.time * idleVerticalSpeed + idleSeedY;
        return Mathf.Sin(t) * idleVerticalAmplitude;
    }

    private void UpdateEnginePush()
    {
        if (!enablePush)
        {
            pushCurrentY = Mathf.SmoothDamp(
                pushCurrentY,
                0f,
                ref pushVelocityY,
                releaseSmoothTime
            );

            return;
        }

        stateTimer -= Time.deltaTime;

        if (state == EngineState.Waiting)
        {
            pushCurrentY = Mathf.SmoothDamp(
                pushCurrentY,
                0f,
                ref pushVelocityY,
                releaseSmoothTime
            );

            if (stateTimer <= 0f)
                EnterAccelerating();

            return;
        }

        if (state == EngineState.Accelerating)
        {
            pushCurrentY = Mathf.SmoothDamp(
                pushCurrentY,
                pushOffsetY,
                ref pushVelocityY,
                accelerationSmoothTime
            );

            if (Mathf.Abs(pushCurrentY - pushOffsetY) < 0.005f)
                EnterHolding();

            return;
        }

        if (state == EngineState.Holding)
        {
            pushCurrentY = Mathf.SmoothDamp(
                pushCurrentY,
                pushOffsetY,
                ref pushVelocityY,
                accelerationSmoothTime
            );

            if (stateTimer <= 0f)
                EnterReleasing();

            return;
        }

        if (state == EngineState.Releasing)
        {
            pushCurrentY = Mathf.SmoothDamp(
                pushCurrentY,
                0f,
                ref pushVelocityY,
                releaseSmoothTime
            );

            if (Mathf.Abs(pushCurrentY) < 0.002f)
            {
                pushCurrentY = 0f;
                pushVelocityY = 0f;
                EnterWaiting();
            }
        }
    }

    private void EnterWaiting()
    {
        state = EngineState.Waiting;
        stateTimer = UnityEngine.Random.Range(waitMin, waitMax);
    }

    private void EnterAccelerating()
    {
        state = EngineState.Accelerating;
        stateTimer = 0f;

        OnEnginePushStarted?.Invoke();
    }

    private void EnterHolding()
    {
        state = EngineState.Holding;
        stateTimer = UnityEngine.Random.Range(holdMin, holdMax);
    }

    private void EnterReleasing()
    {
        state = EngineState.Releasing;
        stateTimer = 0f;
    }
}