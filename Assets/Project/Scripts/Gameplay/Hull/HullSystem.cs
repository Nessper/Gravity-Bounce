using UnityEngine;

/// <summary>
/// Gere la coque (Hull) pour le niveau courant :
/// - stocke la valeur max et courante (cache local pour l'UI),
/// - met a jour la HullUI,
/// - applique des penalites via RunSessionState.
///
/// Source de verite runtime : RunSessionState.
/// HullSystem ne persiste rien directement : il reflete l'etat et drive l'UI/feedback.
/// </summary>
public class HullSystem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private HullUI hullUI;

    [Header("Feedback")]
    [SerializeField] private HullDamageFeedbackController feedbackController;

    [Header("Run (source de verite)")]
    [SerializeField] private RunSessionState runSessionState;

    private int currentHull;
    private int maxHull;
    private int lastKnownHull;

    private bool isInitialized;
    private bool isApplyingMaxHullUpgrade;

    private void OnEnable()
    {
        isInitialized = false;
        isApplyingMaxHullUpgrade = false;

        if (runSessionState != null)
        {
            runSessionState.OnHullChanged.AddListener(HandleHullChanged);
            runSessionState.OnHullMaxChanged.AddListener(HandleHullMaxChanged);
        }
    }

    private void OnDisable()
    {
        if (runSessionState != null)
        {
            runSessionState.OnHullChanged.RemoveListener(HandleHullChanged);
            runSessionState.OnHullMaxChanged.RemoveListener(HandleHullMaxChanged);
        }

        isInitialized = false;
        isApplyingMaxHullUpgrade = false;
    }

    public void Initialize(int startHull, int max)
    {
        isInitialized = false;

        maxHull = Mathf.Max(1, max);
        currentHull = Mathf.Clamp(startHull, 0, maxHull);
        lastKnownHull = currentHull;

        RefreshUI(fullRefresh: true);

        isInitialized = true;
    }

    public void SetCurrentHull(int value)
    {
        currentHull = Mathf.Clamp(Mathf.Max(0, value), 0, Mathf.Max(1, maxHull));
        lastKnownHull = currentHull;
        RefreshUI(fullRefresh: false);
    }

    public void SetMaxHull(int max)
    {
        int newMax = Mathf.Max(1, max);

        if (newMax == maxHull)
            return;

        maxHull = newMax;
        currentHull = Mathf.Clamp(currentHull, 0, maxHull);
        lastKnownHull = currentHull;

        RefreshUI(fullRefresh: true);
    }

    private void HandleHullChanged(int newHull)
    {
        int clamped = Mathf.Clamp(Mathf.Max(0, newHull), 0, Mathf.Max(1, maxHull));

        bool repaired = isInitialized && clamped > lastKnownHull;

        currentHull = clamped;
        RefreshUI(fullRefresh: false);

        if (repaired && !isApplyingMaxHullUpgrade)
            hullUI?.PlayRepairFeedback();

        lastKnownHull = clamped;
    }

    private void HandleHullMaxChanged(int newMax)
    {
        maxHull = Mathf.Max(1, newMax);

        if (runSessionState != null)
            currentHull = Mathf.Clamp(runSessionState.Hull, 0, maxHull);
        else
            currentHull = Mathf.Clamp(currentHull, 0, maxHull);

        lastKnownHull = currentHull;

        RefreshUI(fullRefresh: true);
    }

    public void ApplyBlackPenalty(int blackCount)
    {
        if (blackCount <= 0)
            return;

        if (feedbackController != null)
            feedbackController.PlayHullDamageFeedback(blackCount);

        if (runSessionState != null)
        {
            runSessionState.RemoveHull(blackCount);
            return;
        }

        currentHull = Mathf.Max(0, currentHull - blackCount);
        lastKnownHull = currentHull;
        RefreshUI(fullRefresh: false);
    }

    public void BeginMaxHullUpgrade()
    {
        isApplyingMaxHullUpgrade = true;
    }

    public void EndMaxHullUpgrade()
    {
        isApplyingMaxHullUpgrade = false;
    }

    public void PlayMaxHullUpgradeFeedback()
    {
        hullUI?.PlayMaxHullFeedback();
    }

    /// <summary>
    /// Restaure completement l'etat runtime du Hull apres le tuto.
    /// Source de verite : RunSessionState.
    /// </summary>
    public void RestoreRuntimeState(int restoredHull, int restoredMaxHull)
    {
        int safeMax = Mathf.Max(1, restoredMaxHull);
        int safeHull = Mathf.Clamp(restoredHull, 0, safeMax);

        if (runSessionState != null)
        {
            int shipBaseMax = GetShipBaseHullMaxSafe();
            int restoredBonus = Mathf.Max(0, safeMax - shipBaseMax);

            runSessionState.SetBonusHullMaxInRunDirect(restoredBonus);
            runSessionState.SetHullDirect(safeHull);

            maxHull = runSessionState.HullMax;
            currentHull = runSessionState.Hull;
            lastKnownHull = currentHull;
        }
        else
        {
            maxHull = safeMax;
            currentHull = safeHull;
            lastKnownHull = currentHull;
        }

        RefreshUI(fullRefresh: true);
        hullUI?.ResetVisualState();
    }

    private int GetShipBaseHullMaxSafe()
    {
        if (runSessionState == null)
            return Mathf.Max(1, maxHull);

        ShipDefinition def = ShipCatalogService.GetById(runSessionState.ShipId);
        if (def == null)
            return Mathf.Max(1, maxHull);

        return Mathf.Max(1, def.baseHull);
    }

    public int GetCurrentHull()
    {
        return currentHull;
    }

    public int GetMaxHull()
    {
        return maxHull;
    }

    private void RefreshUI(bool fullRefresh)
    {
        if (hullUI == null)
            return;

        if (fullRefresh)
            hullUI.SetMaxHull(maxHull);

        hullUI.SetCurrentHull(currentHull);
    }
}