using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Règles d'offres du shop Modules.
/// - Charge un JSON de règles (Resources/Shop/modules_shop_rules.json)
/// - Applique des patterns pondérés par worldId (ex: 80/15/5)
/// - Retourne une offre 0..offerCount (fallback robuste si manque de tiers)
///
/// IMPORTANT:
/// - Ce script ne touche PAS à la save (écriture). Il ne fait que "décider".
/// - Pour seed le RNG, il LIT éventuellement la save (runId + nodeIndex + rerollCount) si dispo.
/// - L'état "deal une fois / consommer sans refill" reste dans ModulesHubController.
/// </summary>
public static class ModulesShopOfferRules
{
    // --------------------------------------------------------------------
    // JSON (Resources)
    // --------------------------------------------------------------------

    private const string RulesPath = "Shop/modules_shop_rules"; // sans extension

    private static RulesRoot cachedRules;
    private static bool triedLoadRules;

    // --------------------------------------------------------------------
    // API PRINCIPALE
    // --------------------------------------------------------------------

    /// <summary>
    /// Construit l'offre du shop (0..offerCount modules).
    /// - Candidats = modules non possédés
    /// - Pattern choisi selon worldId (pondéré, RNG local seedé)
    /// - Sélection par buckets T1/T2/T3 (shuffle RNG local seedé)
    /// - Fallback si pattern impossible
    ///
    /// Exigence:
    /// - Seed stable par run (runId)
    /// - MAIS varie à chaque reroll => rerollCount (persisté)
    /// - Optionnel: nodeIndex pour varier par node
    ///
    /// Si SaveManager absent, fallback sur ticks (debug only).
    /// </summary>
    public static List<ModuleDefinition> BuildOffer(
        List<ModuleDefinition> catalogModules,
        Func<string, bool> isOwned,
        string worldId,
        int rerollCount,
        int offerCount)
    {
        var result = new List<ModuleDefinition>();

        int max = Mathf.Max(0, offerCount);
        if (max <= 0)
            return result;

        if (catalogModules == null || catalogModules.Count == 0)
            return result;

        // RNG local isolé (ne dépend pas de UnityEngine.Random global)
        int seed = ComputeShopSeed(worldId, rerollCount);
        var rng = new System.Random(seed);

        // 1) Candidats = non-owned
        var candidates = new List<ModuleDefinition>(catalogModules.Count);
        for (int i = 0; i < catalogModules.Count; i++)
        {
            ModuleDefinition def = catalogModules[i];
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            if (isOwned != null && isOwned(def.id))
                continue;

            candidates.Add(def);
        }

        if (candidates.Count == 0)
            return result;

        // 2) Buckets par tier
        var t1 = new List<ModuleDefinition>();
        var t2 = new List<ModuleDefinition>();
        var t3 = new List<ModuleDefinition>();

        for (int i = 0; i < candidates.Count; i++)
        {
            ModuleDefinition def = candidates[i];
            int tier = Mathf.Max(1, def.tier);

            if (tier == 1) t1.Add(def);
            else if (tier == 2) t2.Add(def);
            else t3.Add(def);
        }

        // 3) Pattern pondéré (RNG local)
        Pattern p = PickPattern(worldId, rng);

        // 4) Appliquer pattern (shuffle seeded)
        TakeSeeded(t1, p.t1, result, max, rng);
        TakeSeeded(t2, p.t2, result, max, rng);
        TakeSeeded(t3, p.t3, result, max, rng);

        // 5) Fallback: compléter avec ce qu'il reste (du plus bas au plus haut)
        FillSeeded(t1, result, max, rng);
        FillSeeded(t2, result, max, rng);
        FillSeeded(t3, result, max, rng);

        return result;
    }

    // --------------------------------------------------------------------
    // SEED (interne, no caller logic)
    // --------------------------------------------------------------------

    private static int ComputeShopSeed(string worldId, int rerollCount)
    {
        // Source de vérité (si dispo) : SaveManager.runState.runId + nodeIndex + rerollCount
        string runId = "";
        int nodeIndex = 0;

        try
        {
            if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
            {
                RunStateData run = SaveManager.Instance.GetRunState();
                if (run != null)
                {
                    runId = run.runId ?? "";
                    nodeIndex = run.currentNodeIndex;
                }
            }
        }
        catch
        {
            // Défensif: pas de throw dans une lib de rules
        }

        string wid = string.IsNullOrEmpty(worldId) ? "W1" : worldId;
        int rr = Mathf.Max(0, rerollCount);

        // Si runId vide, on est probablement hors run / debug / save ancienne.
        // Fallback: seed volatile (acceptable).
        if (string.IsNullOrEmpty(runId))
        {
            long ticks = DateTime.UtcNow.Ticks;
            unchecked
            {
                // Inclure rr quand même, pour que les rerolls changent même en debug
                int t = (int)(ticks ^ (ticks >> 32) ^ Time.frameCount);
                return StableHash($"{wid}:{nodeIndex}:{rr}:SHOP:{t}");
            }
        }

        // Seed stable & variant:
        // - runId => varie par run
        // - worldId + nodeIndex => peut varier par node
        // - rerollCount => varie à chaque reroll (ce qu'on veut)
        // - "SHOP" => namespace
        return StableHash($"{runId}:{wid}:{nodeIndex}:{rr}:SHOP");
    }

    public static int StableHash(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;

        unchecked
        {
            int h = 23;
            for (int i = 0; i < s.Length; i++)
                h = h * 31 + s[i];
            return h;
        }
    }

    // --------------------------------------------------------------------
    // PATTERNS
    // --------------------------------------------------------------------

    private struct Pattern
    {
        public int t1;
        public int t2;
        public int t3;

        public Pattern(int t1, int t2, int t3)
        {
            this.t1 = Mathf.Max(0, t1);
            this.t2 = Mathf.Max(0, t2);
            this.t3 = Mathf.Max(0, t3);
        }
    }

    private static Pattern PickPattern(string worldId, System.Random rng)
    {
        List<PatternRule> rules = GetPatternRulesForWorld(worldId);

        if (rules == null || rules.Count == 0)
            return new Pattern(2, 1, 0);

        float total = 0f;
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i] == null) continue;
            total += Mathf.Max(0f, rules[i].weight);
        }

        if (total <= 0f)
            return new Pattern(2, 1, 0);

        float r = (float)rng.NextDouble() * total;
        float acc = 0f;

        for (int i = 0; i < rules.Count; i++)
        {
            PatternRule pr = rules[i];
            if (pr == null) continue;

            acc += Mathf.Max(0f, pr.weight);
            if (r <= acc)
                return new Pattern(pr.t1, pr.t2, pr.t3);
        }

        PatternRule last = rules[rules.Count - 1];
        if (last == null) return new Pattern(2, 1, 0);
        return new Pattern(last.t1, last.t2, last.t3);
    }

    // --------------------------------------------------------------------
    // JSON LOAD (lazy + cache)
    // --------------------------------------------------------------------

    private static bool EnsureRulesLoaded()
    {
        if (cachedRules != null)
            return true;

        if (triedLoadRules)
            return false;

        triedLoadRules = true;

        TextAsset asset = Resources.Load<TextAsset>(RulesPath);
        if (asset == null || string.IsNullOrEmpty(asset.text))
        {
            Debug.LogWarning("[ModulesShopOfferRules] Règles shop introuvables: Resources/" + RulesPath + ".json");
            return false;
        }

        try
        {
            cachedRules = JsonUtility.FromJson<RulesRoot>(asset.text);
        }
        catch
        {
            Debug.LogWarning("[ModulesShopOfferRules] JSON invalide: Resources/" + RulesPath + ".json");
            cachedRules = null;
            return false;
        }

        if (cachedRules == null)
            return false;

        if (cachedRules.worldRules == null)
            cachedRules.worldRules = new List<WorldRule>();

        if (cachedRules.defaultPatterns == null)
            cachedRules.defaultPatterns = new List<PatternRule>();

        return true;
    }

    private static List<PatternRule> GetPatternRulesForWorld(string worldId)
    {
        if (!EnsureRulesLoaded() || cachedRules == null)
            return null;

        if (!string.IsNullOrEmpty(worldId) && cachedRules.worldRules != null)
        {
            for (int i = 0; i < cachedRules.worldRules.Count; i++)
            {
                WorldRule wr = cachedRules.worldRules[i];
                if (wr == null) continue;

                if (string.Equals(wr.worldId, worldId, StringComparison.Ordinal))
                {
                    if (wr.patterns != null && wr.patterns.Count > 0)
                        return wr.patterns;
                    break;
                }
            }
        }

        return cachedRules.defaultPatterns;
    }

    // --------------------------------------------------------------------
    // SÉLECTION SEEDED (shuffle RNG local)
    // --------------------------------------------------------------------

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        if (list == null || list.Count <= 1 || rng == null)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void TakeSeeded(List<ModuleDefinition> bucket, int amount, List<ModuleDefinition> result, int max, System.Random rng)
    {
        if (bucket == null || bucket.Count == 0) return;
        if (amount <= 0) return;
        if (result == null) return;
        if (result.Count >= max) return;

        Shuffle(bucket, rng);

        int take = Mathf.Min(amount, bucket.Count);
        for (int i = 0; i < take && result.Count < max; i++)
            result.Add(bucket[i]);

        bucket.RemoveRange(0, take);
    }

    private static void FillSeeded(List<ModuleDefinition> bucket, List<ModuleDefinition> result, int max, System.Random rng)
    {
        if (bucket == null || bucket.Count == 0) return;
        if (result == null) return;
        if (result.Count >= max) return;

        Shuffle(bucket, rng);

        int i = 0;
        while (i < bucket.Count && result.Count < max)
        {
            result.Add(bucket[i]);
            i++;
        }

        if (i > 0)
            bucket.RemoveRange(0, i);
    }

    // --------------------------------------------------------------------
    // DATA JSON (classes internes => 1 seul script)
    // --------------------------------------------------------------------

    [Serializable]
    private class RulesRoot
    {
        public List<WorldRule> worldRules = new List<WorldRule>();
        public List<PatternRule> defaultPatterns = new List<PatternRule>();
    }

    [Serializable]
    private class WorldRule
    {
        public string worldId;
        public List<PatternRule> patterns = new List<PatternRule>();
    }

    [Serializable]
    private class PatternRule
    {
        public int t1;
        public int t2;
        public int t3;
        public float weight;
    }
}