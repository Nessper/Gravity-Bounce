using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "RunSessionState", menuName = "Game/Run Session State")]
public class RunSessionState : ScriptableObject
{
    [SerializeField] private int lives;

    // Flag interne : si true, le prochain restart de niveau doit conserver les vies actuelles
    [SerializeField] private bool keepCurrentLivesOnNextRestart;

    public UnityEvent<int> OnLivesChanged = new UnityEvent<int>();

    public int Lives => lives;

    public void InitLives(int value)
    {
        lives = Mathf.Max(0, value);
        OnLivesChanged.Invoke(lives);
    }

    public void RemoveLife(int amount = 1)
    {
        int prev = lives;
        lives = Mathf.Max(0, lives - Mathf.Max(1, amount));
        if (lives != prev)
            OnLivesChanged.Invoke(lives);
    }

    public void AddLife(int amount = 1)
    {
        lives += Mathf.Max(1, amount);
        OnLivesChanged.Invoke(lives);
    }

    // Demande explicite : "au prochain restart, garde les vies actuelles" (true)
    // ou "au prochain restart, réinitialise depuis le vaisseau" (false)
    public void MarkCarryLivesOnNextRestart(bool keep)
    {
        keepCurrentLivesOnNextRestart = keep;
    }

    // Consomme le flag (il repasse automatiquement à false après l'appel)
    public bool ConsumeKeepFlag()
    {
        bool v = keepCurrentLivesOnNextRestart;
        keepCurrentLivesOnNextRestart = false;
        return v;
    }
}
