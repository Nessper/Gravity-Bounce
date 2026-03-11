using UnityEngine;

/// <summary>
/// Gère la coque (Hull) pour le niveau courant :
/// - stocke la valeur max et courante (cache local pour l'UI),
/// - met à jour la HullUI,
/// - applique des pénalités (ex : billes noires par flush) via RunSessionState.
///
/// Source de vérité runtime : RunSessionState (persiste via SaveManager).
/// HullSystem ne persiste rien directement : il reflète l'état et drive l'UI/feedback.
/// </summary>
public class HullSystem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private HullUI hullUI;

    [Header("Feedback")]
    [SerializeField] private HullDamageFeedbackController feedbackController;

    [Header("Run (source de vérité)")]
    [SerializeField] private RunSessionState runSessionState;

    private int currentHull;
    private int maxHull;

    // ------------------------------------------------------------
    // INIT / SYNC
    // ------------------------------------------------------------

    /// <summary>
    /// Initialise la coque pour ce niveau.
    /// Appel recommandé au début du gameplay, après chargement de RunSessionState.
    /// </summary>
    public void Initialize(int startHull, int max)
    {
        maxHull = Mathf.Max(1, max);
        currentHull = Mathf.Clamp(startHull, 0, maxHull);

        RefreshUI(fullRefresh: true);
    }

    /// <summary>
    /// Met à jour la valeur courante (sync externe).
    /// </summary>
    public void SetCurrentHull(int value)
    {
        currentHull = Mathf.Clamp(Mathf.Max(0, value), 0, Mathf.Max(1, maxHull));
        RefreshUI(fullRefresh: false);
    }

    /// <summary>
    /// Met à jour la valeur maximale (sync externe).
    /// Clamp la valeur courante, puis full refresh UI.
    /// </summary>
    public void SetMaxHull(int max)
    {
        int newMax = Mathf.Max(1, max);

        if (newMax == maxHull)
            return;

        maxHull = newMax;
        currentHull = Mathf.Clamp(currentHull, 0, maxHull);

        RefreshUI(fullRefresh: true);
    }

    // ------------------------------------------------------------
    // GAMEPLAY PENALTIES
    // ------------------------------------------------------------

    /// <summary>
    /// Applique une pénalité de hull en fonction du nombre de billes noires.
    /// Clamp à 0 pour éviter les valeurs négatives.
    /// </summary>
    public void ApplyBlackPenalty(int blackCount)
    {
        if (blackCount <= 0)
            return;

        // Feedback visuel (avant la maj du chiffre)
        if (feedbackController != null)
            feedbackController.PlayHullDamageFeedback(blackCount);

        // Source de vérité : RunSessionState
        if (runSessionState != null)
        {
            runSessionState.RemoveHull(blackCount);
            return;
        }

        // Fallback dev-only si RunSessionState absent
        currentHull = Mathf.Max(0, currentHull - blackCount);
        RefreshUI(fullRefresh: false);
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
