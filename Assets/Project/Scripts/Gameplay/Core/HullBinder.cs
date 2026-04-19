using UnityEngine;

/// <summary>
/// Synchronise le Hull runtime (RunSessionState) avec :
/// - HullSystem (qui met a jour HullUI)
/// - LevelBriefingController (preview hull)
///
/// Source de verite : RunSessionState.
/// </summary>
public class HullBinder : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Targets")]
    [SerializeField] private HullSystem hullSystem;
    [SerializeField] private LevelBriefingOverlayController briefingController;

    private bool hullSystemInitialized;

    private void OnEnable()
    {
        if (runSession != null)
        {
            runSession.OnHullChanged.AddListener(HandleHullChanged);
            runSession.OnHullMaxChanged.AddListener(HandleHullMaxChanged);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (runSession != null)
        {
            runSession.OnHullChanged.RemoveListener(HandleHullChanged);
            runSession.OnHullMaxChanged.RemoveListener(HandleHullMaxChanged);
        }
    }

    private void Refresh()
    {
        if (runSession == null)
            return;

        Apply(runSession.Hull, runSession.HullMax);
    }

    private void HandleHullChanged(int hull)
    {
        if (runSession == null)
            return;

        Apply(hull, runSession.HullMax);
    }

    private void HandleHullMaxChanged(int maxHull)
    {
        if (runSession == null)
            return;

        Apply(runSession.Hull, maxHull);
    }

    private void Apply(int hull, int maxHull)
    {
        int m = Mathf.Max(1, maxHull);
        int h = Mathf.Clamp(hull, 0, m);

        if (hullSystem != null)
        {
            if (!hullSystemInitialized)
            {
                hullSystem.Initialize(h, m);
                hullSystemInitialized = true;
            }
            else
            {
                // IMPORTANT : HullSystem doit supporter le changement de max.
                hullSystem.SetMaxHull(m);
                hullSystem.SetCurrentHull(h);
            }
        }

       // if (briefingController != null)
         //   briefingController.SetShipRuntimeHull(h, m);
    }
}
