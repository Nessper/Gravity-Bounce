using System;
using UnityEngine;

/// <summary>
/// Compte les billes noires actuellement actives dans la scene.
/// Sert de source de verite pour les effets de menace.
/// </summary>
public class BlackThreatTracker : MonoBehaviour
{
    public static BlackThreatTracker Instance { get; private set; }

    public int ActiveBlackCount { get; private set; }

    public event Action<int> OnBlackCountChanged;

    [Header("Debug")]
    [SerializeField] private bool logChanges = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BlackThreatTracker] Instance dupliquee detectee.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ActiveBlackCount = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterBlackBall()
    {
        ActiveBlackCount++;
        NotifyChanged();
    }

    public void UnregisterBlackBall()
    {
        ActiveBlackCount = Mathf.Max(0, ActiveBlackCount - 1);
        NotifyChanged();
    }

    public void ResetTracker()
    {
        ActiveBlackCount = 0;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (logChanges)
            Debug.Log("[BlackThreatTracker] ActiveBlackCount = " + ActiveBlackCount);

        OnBlackCountChanged?.Invoke(ActiveBlackCount);
    }
}