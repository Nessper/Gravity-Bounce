using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "RunSessionState", menuName = "Game/Run Session State")]
public class RunSessionState : ScriptableObject
{
    [SerializeField] private int hull;

    // Flag interne : si true, le prochain restart de niveau doit conserver les vies actuelles
    [SerializeField] private bool keepCurrentHullOnNextRestart;

    public UnityEvent<int> OnHullChanged = new UnityEvent<int>();

    public int Hull => hull;

    public void InitHull(int value)
    {
        hull = Mathf.Max(0, value);
        OnHullChanged.Invoke(hull);
    }

    public void RemoveHull(int amount = 1)
    {
        int prev = hull;
        hull = Mathf.Max(0, hull - Mathf.Max(1, amount));
        if (hull != prev)
            OnHullChanged.Invoke(hull);
    }

    public void AddHull(int amount = 1)
    {
        hull += Mathf.Max(1, amount);
        OnHullChanged.Invoke(hull);
    }

    // Demande explicite : "au prochain restart, garde les vies actuelles" (true)
    // ou "au prochain restart, réinitialise depuis le vaisseau" (false)
    public void MarkCarryHullOnNextRestart(bool keep)
    {
        keepCurrentHullOnNextRestart = keep;
    }

    // Consomme le flag (il repasse automatiquement à false après l'appel)
    public bool ConsumeKeepFlag()
    {
        bool v = keepCurrentHullOnNextRestart;
        keepCurrentHullOnNextRestart = false;
        return v;
    }
}
