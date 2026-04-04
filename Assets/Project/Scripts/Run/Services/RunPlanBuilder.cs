using System;
using UnityEngine;

/// <summary>
/// Construit un RunPlan à partir d'un WorldCatalog.
/// Tokens supportés:
/// - "SHOP:START"
/// - "SHOP:MID"
/// - "BOSS:LevelId"
/// - "END"
/// - "LevelId"
///
/// Règles:
/// - Un shop doit toujours avoir un stage explicite.
/// - "SHOP" seul est invalide.
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
            node.shopStage = ShopStage.None;

            // SHOP:START / SHOP:MID
            if (token.StartsWith("SHOP:", StringComparison.OrdinalIgnoreCase))
            {
                node.type = RunNodeType.Shop;

                if (!TryParseShopStage(token, out ShopStage parsedStage))
                {
                    Debug.LogError("[RunPlanBuilder] Stage de shop invalide: " + token);
                    return null;
                }

                node.shopStage = parsedStage;
            }
            // SHOP seul interdit
            else if (string.Equals(token, "SHOP", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[RunPlanBuilder] Token SHOP sans stage explicite interdit: " + token);
                return null;
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

    /// <summary>
    /// Parse un token de shop explicite.
    /// Exemples valides:
    /// - SHOP:START
    /// - SHOP:MID
    /// </summary>
    private static bool TryParseShopStage(string token, out ShopStage shopStage)
    {
        shopStage = ShopStage.None;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        string[] parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        string rawStage = parts[1].Trim();

        if (string.Equals(rawStage, "START", StringComparison.OrdinalIgnoreCase))
        {
            shopStage = ShopStage.Start;
            return true;
        }

        if (string.Equals(rawStage, "MID", StringComparison.OrdinalIgnoreCase))
        {
            shopStage = ShopStage.Mid;
            return true;
        }

        return false;
    }
}