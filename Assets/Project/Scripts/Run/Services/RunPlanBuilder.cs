using System;
using UnityEngine;

/// <summary>
/// Construit un RunPlan à partir d'un WorldCatalog.
/// Tokens supportés:
/// - "SHOP"
/// - "BOSS:LevelId"
/// - "END"
/// - "LevelId"
///
/// Règle:
/// - Un seul ending global.
/// - "END" suffit.
/// </summary>
public static class RunPlanBuilder
{
    public static RunPlan BuildFromWorld(string worldId)
    {
        WorldCatalogService.WorldEntry world;
        if (!WorldCatalogService.TryGetWorld(worldId, out world) ||
            world.levelIds == null || world.levelIds.Length == 0)
        {
            Debug.LogError("[RunPlanBuilder] Monde introuvable ou vide: " + worldId);
            return null;
        }

        RunPlan plan = new RunPlan();
        plan.worldId = worldId;
        plan.currentIndex = 0;

        for (int i = 0; i < world.levelIds.Length; i++)
        {
            string token = (world.levelIds[i] ?? "").Trim();

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("[RunPlanBuilder] Token vide (worldId=" + worldId + ", index=" + i + ")");
                return null;
            }

            RunNode node = new RunNode();
            node.nodeId = worldId + "_N" + (i + 1);
            node.levelId = "";

            // SHOP
            if (string.Equals(token, "SHOP", StringComparison.OrdinalIgnoreCase))
            {
                node.type = RunNodeType.Shop;
            }
            // BOSS
            else if (token.StartsWith("BOSS:", StringComparison.OrdinalIgnoreCase))
            {
                node.type = RunNodeType.Boss;
                node.levelId = token.Substring("BOSS:".Length).Trim();

                if (string.IsNullOrEmpty(node.levelId))
                {
                    Debug.LogError("[RunPlanBuilder] BOSS sans levelId: " + token);
                    return null;
                }
            }
            // END (unique global ending)
            else if (string.Equals(token, "END", StringComparison.OrdinalIgnoreCase))
            {
                node.type = RunNodeType.Ending;
            }
            // LEVEL
            else
            {
                node.type = RunNodeType.Level;
                node.levelId = token;
            }

            plan.nodes.Add(node);
        }

        return plan;
    }
}