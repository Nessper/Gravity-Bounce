using UnityEngine;

/// <summary>
/// Gère la coque (Hull) pour le niveau courant :
/// - stocke la valeur max et courante,
/// - met à jour la HullUI,
/// - applique des pénalités (ex : billes noires par flush).
/// 
/// Les valeurs initiales (startHull / maxHull) sont fournies par LevelManager.
/// </summary>
public class HullSystem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private HullUI hullUI;
    [Header("Feedback")]
    [SerializeField] private HullDamageFeedbackController feedbackController;


    private int currentHull;
    private int maxHull;

    /// <summary>
    /// Initialise la coque pour ce niveau.
    /// À appeler depuis LevelManager, avec les valeurs issues du vaisseau / RunSession.
    /// </summary>
    public void Initialize(int startHull, int max)
    {
        maxHull = Mathf.Max(0, max);
        currentHull = Mathf.Clamp(startHull, 0, maxHull);

        if (hullUI != null)
        {
            hullUI.SetMaxHull(maxHull);
            hullUI.SetCurrentHull(currentHull);
        }
    }

    /// <summary>
    /// Sync externe (ex : RunSession qui change le hull).
    /// </summary>
    public void SetCurrentHull(int value)
    {
        if (maxHull > 0)
            currentHull = Mathf.Clamp(value, 0, maxHull);
        else
            currentHull = Mathf.Max(0, value);

        if (hullUI != null)
            hullUI.SetCurrentHull(currentHull);
    }

    /// <summary>
    /// Applique une pénalité de hull en fonction du nombre de billes noires.
    /// Clamp à 0 pour éviter les valeurs négatives.
    /// </summary>
    public void ApplyBlackPenalty(int blackCount)
    {
        if (blackCount <= 0)
            return;

        int previousHull = currentHull;

        currentHull -= blackCount;
        if (currentHull < 0)
            currentHull = 0;

        // Mise à jour UI
        if (hullUI != null)
            hullUI.SetCurrentHull(currentHull);

        // Feedback visuels
        if (feedbackController != null && currentHull < previousHull)
        {
            feedbackController.PlayHullDamageFeedback(blackCount);
        }

        // Ici tu peux aussi notifier RunSession / sauvegarde si besoin
    }


    public int GetCurrentHull()
    {
        return currentHull;
    }

    public int GetMaxHull()
    {
        return maxHull;
    }
}
