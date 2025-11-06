using UnityEngine;

public static class PlanEstimator
{
    private const float eps = 1e-4f;

    /// <summary>
    /// Calcule le nombre total de billes prévues à spawn selon les durées et intervalles de phase.
    /// </summary>
    public static int Estimate(float[] phaseDurations, float[] phaseIntervals, bool spawnAtT0)
    {
        if (phaseDurations == null || phaseIntervals == null || phaseDurations.Length != 3 || phaseIntervals.Length != 3)
        {
            Debug.LogWarning("[PlanEstimator] Paramètres invalides.");
            return 0;
        }

        int planned = spawnAtT0 ? 1 : 0;

        for (int i = 0; i < 3; i++)
        {
            float dur = Mathf.Max(0f, phaseDurations[i]);
            float iv = Mathf.Max(0.0001f, phaseIntervals[i]);
            if (dur > 0f)
                planned += Mathf.FloorToInt((dur - eps) / iv);
        }

        return Mathf.Max(0, planned);
    }
}
