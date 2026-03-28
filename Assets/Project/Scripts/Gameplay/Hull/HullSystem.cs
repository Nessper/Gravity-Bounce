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

    // Empeche les faux feedbacks pendant la phase de sync initiale.
    private bool isInitialized;

    // True pendant l'application d'un bonus de Max Hull.
    // Permet d'eviter qu'une hausse de Hull courant liee a ce bonus
    // soit interpretee comme une simple reparation.
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

    // ------------------------------------------------------------
    // INIT / SYNC
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // GAMEPLAY PENALTIES
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // MAX HULL UPGRADE FLOW
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // GETTERS
    // ------------------------------------------------------------

    public int GetCurrentHull()
    {
        return currentHull;
    }

    public int GetMaxHull()
    {
        return maxHull;
    }

    // ------------------------------------------------------------
    // UI
    // ------------------------------------------------------------

    private void RefreshUI(bool fullRefresh)
    {
        if (hullUI == null)
            return;

        if (fullRefresh)
            hullUI.SetMaxHull(maxHull);

        hullUI.SetCurrentHull(currentHull);
    }
}