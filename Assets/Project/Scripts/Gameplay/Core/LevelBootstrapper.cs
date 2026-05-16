using UnityEngine;

public class LevelBootstrapper : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Optional UI / Controllers")]
    [SerializeField] private LevelIdUI levelIdUI;
    [SerializeField] private LevelIntroSequenceController introSequenceController;

    public bool TryBuildContext(out LevelContext context)
    {
        context = null;



        if (runSession == null)
        {
            Debug.LogError("[LevelBootstrapper] RunSessionState non assigne.");
            return false;
        }

        if (!runSession.LoadFromSave())
        {
            Debug.LogError("[LevelBootstrapper] LoadFromSave a échoué.");
            return false;
        }

        if (!runSession.EnsurePlanLoaded())
        {
            Debug.LogError("[LevelBootstrapper] EnsurePlanLoaded a échoué.");
            return false;
        }

        // Run terminée => pas de node jouable
        if (runSession.IsRunCompleted)
        {
            Debug.LogWarning("[LevelBootstrapper] Run completed: aucun node jouable.");
            return false;
        }

        RunNode node = runSession.CurrentPlayableNode;
        if (node == null)
        {
            Debug.LogError("[LevelBootstrapper] CurrentPlayableNode null alors que run non-completed.");
            return false;
        }

        bool isPlayable =
    node.type == RunNodeType.Level ||
    node.type == RunNodeType.Boss;

        if (!isPlayable || string.IsNullOrEmpty(node.levelId))
        {
            Debug.LogError(
                "[LevelBootstrapper] Node courant non-playable ou levelId vide. " +
                "nodeId=" + node.nodeId +
                " type=" + node.type +
                " levelId=" + (string.IsNullOrEmpty(node.levelId) ? "<EMPTY>" : node.levelId)
            );
            return false;
        }

        string levelId = node.levelId;

        if (levelIdUI != null)
            levelIdUI.SetLevelId(levelId);

        if (introSequenceController != null)
            introSequenceController.ConfigureLevelId(levelId);

        LevelCatalogService.LevelCatalogEntry meta;
        if (!LevelCatalogService.TryGet(levelId, out meta))
        {
            Debug.LogError("[LevelBootstrapper] LevelId absent du LevelCatalog: " + levelId);
            return false;
        }

        if (string.IsNullOrEmpty(meta.gameplayPath))
        {
            Debug.LogError("[LevelBootstrapper] gameplayPath vide dans LevelCatalog pour levelId=" + levelId);
            return false;
        }

        TextAsset gameplayJson = Resources.Load<TextAsset>(meta.gameplayPath);
        if (gameplayJson == null)
        {
            Debug.LogError("[LevelBootstrapper] JSON gameplay introuvable via gameplayPath=" + meta.gameplayPath);
            return false;
        }

        LevelData data = JsonUtility.FromJson<LevelData>(gameplayJson.text);
        if (data == null)
        {
            Debug.LogError("[LevelBootstrapper] Erreur de parsing JSON gameplay (Resources).");
            return false;
        }

        context = new LevelContext
        {
            levelId = levelId,
            levelMeta = meta,
            levelData = data
        };

        return true;
    }
}
