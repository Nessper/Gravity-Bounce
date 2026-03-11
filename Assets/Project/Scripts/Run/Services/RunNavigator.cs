using UnityEngine;

public static class RunNavigator
{
    public static RunNode GetCurrentPlayableNode(RunPlan plan)
    {
        if (plan == null)
            return null;

        return plan.CurrentPlayableNode;
    }

    /// <summary>
    /// Avance l'index courant d'un cran.
    /// Autorise currentIndex == NodeCount (run completed).
    /// Retourne false seulement si:
    /// - plan invalide
    /// - ou currentIndex est deja > NodeCount (etat impossible)
    /// - ou plan vide
    /// </summary>
    public static bool TryAdvance(RunPlan plan)
    {
        if (plan == null || !plan.HasNodes)
            return false;

        int next = plan.currentIndex + 1;

        // Autoriser EXACTEMENT Count
        if (next > plan.NodeCount)
            return false;

        plan.currentIndex = next;
        return true;
    }
}
