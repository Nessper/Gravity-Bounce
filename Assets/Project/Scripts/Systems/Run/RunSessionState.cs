using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "RunSessionState", menuName = "Game/Run Session State")]
public class RunSessionState : ScriptableObject
{
    // ============================================================
    // HULL
    // ============================================================

    [Header("Hull")]
    [SerializeField] private int hull;
    [SerializeField] private bool keepCurrentHullOnNextRestart;

    public UnityEvent<int> OnHullChanged = new UnityEvent<int>();

    public int Hull => hull;

    /// <summary>
    /// Initialise le hull de la run en cours.
    /// Persiste dans la sauvegarde si SaveManager est present.
    /// </summary>
    public void InitHull(int value)
    {
        hull = Mathf.Max(0, value);
        PersistHull();
        OnHullChanged.Invoke(hull);
    }

    /// <summary>
    /// Retire du hull (degats).
    /// Persiste uniquement si la valeur change.
    /// </summary>
    public void RemoveHull(int amount = 1)
    {
        int prev = hull;
        hull = Mathf.Max(0, hull - Mathf.Max(1, amount));

        if (hull != prev)
        {
            PersistHull();
            OnHullChanged.Invoke(hull);
        }
    }

    /// <summary>
    /// Ajoute du hull (reparation / bonus).
    /// Persiste systematiquement.
    /// </summary>
    public void AddHull(int amount = 1)
    {
        hull += Mathf.Max(1, amount);
        PersistHull();
        OnHullChanged.Invoke(hull);
    }

    public void MarkCarryHullOnNextRestart(bool keep)
    {
        keepCurrentHullOnNextRestart = keep;
    }

    public bool ConsumeKeepFlag()
    {
        bool v = keepCurrentHullOnNextRestart;
        keepCurrentHullOnNextRestart = false;
        return v;
    }

    private void PersistHull()
    {
        if (SaveManager.Instance == null)
            return;

        // Ecrit dans la run persistante
        SaveManager.Instance.SetRemainingHullInRun(hull);
    }

    // ============================================================
    // CONTRACT STRIKES
    // ============================================================

    [Header("Contract Strikes")]
    [SerializeField] private int contractLives;

    public UnityEvent<int> OnContractLivesChanged = new UnityEvent<int>();

    public int ContractLives => contractLives;

    public void InitContractLives(int value)
    {
        contractLives = Mathf.Max(0, value);
        PersistContractLives();
        OnContractLivesChanged.Invoke(contractLives);
    }

    public void LoseContractLife(int amount = 1)
    {
        int prev = contractLives;
        contractLives = Mathf.Max(0, contractLives - Mathf.Max(1, amount));

        if (contractLives != prev)
        {
            PersistContractLives();
            OnContractLivesChanged.Invoke(contractLives);
        }
    }

    public void AddContractLife(int amount = 1)
    {
        int prev = contractLives;
        contractLives = Mathf.Max(0, contractLives + Mathf.Max(1, amount));

        if (contractLives != prev)
        {
            PersistContractLives();
            OnContractLivesChanged.Invoke(contractLives);
        }
    }

    private void PersistContractLives()
    {
        if (SaveManager.Instance == null)
            return;

        SaveManager.Instance.SetRemainingContractLives(contractLives);
    }
}
