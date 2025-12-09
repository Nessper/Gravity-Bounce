using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "RunSessionState", menuName = "Game/Run Session State")]
public class RunSessionState : ScriptableObject
{
    // ============================================================
    // HULL (inchangé)
    // ============================================================

    [Header("Hull")]
    [SerializeField] private int hull;
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

    // ============================================================
    // CONTRACT STRIKES (nouvelle section)
    // ============================================================

    [Header("Contract Strikes")]
    [SerializeField] private int contractLives;

    public UnityEvent<int> OnContractLivesChanged = new UnityEvent<int>();

    /// <summary>
    /// Nombre de vies de contrat restantes (0..max).
    /// </summary>
    public int ContractLives => contractLives;

    /// <summary>
    /// Initialise les vies de contrat pour la run en cours.
    /// Appelé au boot du niveau par RunSessionBootstrapper.
    /// </summary>
    public void InitContractLives(int value)
    {
        contractLives = Mathf.Max(0, value);
        OnContractLivesChanged.Invoke(contractLives);
    }

    /// <summary>
    /// Perdre 1 ou plusieurs vies de contrat (échec mission).
    /// </summary>
    public void LoseContractLife(int amount = 1)
    {
        int prev = contractLives;
        contractLives = Mathf.Max(0, contractLives - Mathf.Max(1, amount));
        if (contractLives != prev)
            OnContractLivesChanged.Invoke(contractLives);
    }

    /// <summary>
    /// Ajouter des vies de contrat (rare, bonus éventuel).
    /// </summary>
    public void AddContractLife(int amount = 1)
    {
        int prev = contractLives;
        contractLives = Mathf.Max(0, contractLives + Mathf.Max(1, amount));
        OnContractLivesChanged.Invoke(contractLives);
    }
}
