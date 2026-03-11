#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class VoidScrappersDebugMenu
{
    private const string PrefKey = "VS_DEBUG_MAIN";
    private const string SaveKey = "GameSave_v1";

    [MenuItem("VoidScrappers/Debug/Enable")]
    private static void Enable()
    {
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[VoidScrappersDebugMenu] VS_DEBUG_MAIN = 1");
    }

    [MenuItem("VoidScrappers/Debug/Disable (Reset RunState)")]
    private static void Disable()
    {
        PlayerPrefs.SetInt(PrefKey, 0);
        PlayerPrefs.Save();

        ResetRunStateInSave();

        Debug.Log("[VoidScrappersDebugMenu] VS_DEBUG_MAIN = 0 (RunState reset)");
    }

    [MenuItem("VoidScrappers/Debug/Status")]
    private static void Status()
    {
        int v = PlayerPrefs.GetInt(PrefKey, 0);
        Debug.Log("[VoidScrappersDebugMenu] VS_DEBUG_MAIN = " + v);
    }

    [MenuItem("VoidScrappers/Debug/Reset RunState Only")]
    private static void ResetRunStateOnly()
    {
        ResetRunStateInSave();
        Debug.Log("[VoidScrappersDebugMenu] RunState reset (only).");
    }

    private static void ResetRunStateInSave()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            Debug.LogWarning("[VoidScrappersDebugMenu] No save found (GameSave_v1 missing).");
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[VoidScrappersDebugMenu] Save JSON is empty.");
            return;
        }

        GameSaveData data;
        try
        {
            data = JsonUtility.FromJson<GameSaveData>(json);
        }
        catch
        {
            Debug.LogWarning("[VoidScrappersDebugMenu] Save JSON parse failed. No changes applied.");
            return;
        }

        if (data == null)
        {
            Debug.LogWarning("[VoidScrappersDebugMenu] Save JSON parsed to null. No changes applied.");
            return;
        }

        if (data.runState == null)
            data.runState = new RunStateData();

        RunStateData run = data.runState;

        run.runId = "";
        run.hasOngoingRun = false;

        run.worldId = "W1";
        run.currentNodeIndex = 0;

        run.remainingHullInRun = 0;
        run.remainingContractLives = 3;
        run.currentRunScore = 0;
        run.nodesClearedInRun = 0;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        run.pendingAbortHullPenaltyFeedback = false;
        run.lastAbortHullPenaltyAmount = 0;
        run.pendingGameOverFromAbort = false;

        run.hasPendingEndToken = false;
        run.pendingEndTokenCommitted = false;
        run.pendingEndToken = default;

        run.hasPendingEndSnapshot = false;
        run.pendingEndSnapshot = null;

        run.unlockedModuleSlotsInRun = 0;

        // Run-only equipment: on purge
        run.equippedModuleIds = null;

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
#endif
