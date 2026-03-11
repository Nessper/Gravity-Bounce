using UnityEngine;

public class RunSessionBootstrapper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RunSessionState runSessionState;

    private void Awake()
    {
        if (runSessionState == null)
        {
            Debug.LogError("[RunSessionBootstrapper] RunSessionState manquant.");
            enabled = false;
        }
    }

    private void Start()
    {

        if (!enabled)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[RunSessionBootstrapper] SaveManager ou SaveData introuvable.");
            SafeReturnToTitleOrDisable();
            enabled = false;
            return;
        }

        RunStateData run = SaveManager.Instance.GetRunState();
        if (!IsRunStateValid(run))
        {
            Debug.LogWarning("[RunSessionBootstrapper] RunState invalide / pas de run.");
            SafeReturnToTitleOrDisable();
            enabled = false;
            return;
        }

        if (!runSessionState.LoadFromSave())
        {
            Debug.LogError("[RunSessionBootstrapper] LoadFromSave a échoué.");
            SafeReturnToTitleOrDisable();
            enabled = false;
            return;
        }

        if (!runSessionState.EnsurePlanLoaded())
        {
            Debug.LogError("[RunSessionBootstrapper] EnsurePlanLoaded a échoué.");
            SafeReturnToTitleOrDisable();
            enabled = false;
            return;
        }


        if (runSessionState.IsRunCompleted)
        {
            Debug.LogWarning("[RunSessionBootstrapper] Run completed (nodeIndex == Count).");
            SafeReturnToTitleOrDisable();
            enabled = false;
            return;
        }

        RunNode node = runSessionState.CurrentPlayableNode;
        string nodeInfo = (node != null) ? (node.nodeId + " / " + node.levelId) : "NULL";
        Debug.Log("[RunSessionBootstrapper] Run chargée: worldId=" + runSessionState.WorldId
                  + " nodeIndex=" + runSessionState.CurrentNodeIndex
                  + " node=" + nodeInfo);
    }

    private bool IsRunStateValid(RunStateData run)
    {
        if (run == null) return false;
        if (!run.hasOngoingRun) return false;
        if (string.IsNullOrEmpty(run.currentShipId)) return false;
        if (string.IsNullOrEmpty(run.worldId)) return false;
        if (run.currentNodeIndex < 0) return false;
        return true;
    }

    private void SafeReturnToTitleOrDisable()
    {
        if (BootRoot.GameFlow != null)
        {
            BootRoot.GameFlow.GoToTitle();
        }
        else
        {
            // Standalone : pas de Title. On log et on laisse la scene en place.
            Debug.LogWarning("[RunSessionBootstrapper] GameFlow absent (standalone). Aucun retour Title possible.");
        }
    }
}
