using UnityEngine;

/// <summary>
/// Conteneur de donnees chargees au demarrage du niveau.
/// Source de verite : RunSession -> RunPlan -> RunNode.levelId.
/// </summary>
[System.Serializable]
public class LevelContext
{
    public string levelId;
    public LevelCatalogService.LevelCatalogEntry levelMeta;
    public LevelData levelData;

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(levelId) && levelData != null;
    }
}
