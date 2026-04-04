using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Règles de génération d'offres pour le shop Modules.
///
/// Responsabilités :
/// - Charger les règles JSON depuis Resources/Shop/modules_shop_rules.json
/// - Résoudre la clé de règles explicite du shop (ex: W1_START, W1_MID)
/// - Construire une offre de modules non possédés
/// - Appliquer un pattern pondéré T1/T2/T3
/// - Garantir un RNG local, stable par run / node / reroll
///
/// Important :
/// - Ce script ne persiste rien en save.
/// - Ce script ne décide pas quand l'offre doit être "dealée" ou consommée.
/// - Ce script ne fait que choisir quels modules doivent être proposés.
/// - La logique "deal une fois / consommer sans refill" reste dans ModulesHubController.
/// </summary>
public static class ModulesShopOfferRules
{
    // ---------------------------------------------------------------------
    // CONFIG JSON
    // ---------------------------------------------------------------------

    /// <summary>
    /// Chemin Resources du fichier de règles, sans extension.
    /// </summary>
    private const string RulesPath = "Shop/modules_shop_rules";

    /// <summary>
    /// Cache lazy des règles chargées depuis le JSON.
    /// </summary>
    private static RulesRoot cachedRules;

    /// <summary>
    /// Évite de recharger plusieurs fois un JSON introuvable/invalide.
    /// </summary>
    private static bool triedLoadRules;

    // ---------------------------------------------------------------------
    // API PUBLIQUE
    // ---------------------------------------------------------------------

    /// <summary>
    /// Construit l'offre du shop.
    ///
    /// Pipeline :
    /// 1) filtre les modules déjà possédés
    /// 2) regroupe les candidats par tier (T1 / T2 / T3)
    /// 3) résout la clé explicite du shop (ex: W1_START)
    /// 4) choisit un pattern pondéré pour cette clé
    /// 5) applique le pattern avec un RNG local seedé
    /// 6) complète si nécessaire avec les modules restants
    ///
    /// Important :
    /// - L'offre peut contenir de 0 à offerCount modules.
    /// - Si les règles sont absentes ou invalides, l'offre peut revenir vide.
    /// - Le RNG dépend du runId, du nodeIndex et du rerollCount.
    /// </summary>
    public static List<ModuleDefinition> BuildOffer(
        List<ModuleDefinition> catalogModules,
        Func<string, bool> isOwned,
        string worldId,
        ShopStage shopStage,
        int rerollCount,
        int offerCount)
    {
        var result = new List<ModuleDefinition>();

        int maxOfferCount = Mathf.Max(0, offerCount);
        if (maxOfferCount <= 0)
            return result;

        if (catalogModules == null || catalogModules.Count == 0)
            return result;

        string rulesKey = BuildRulesKey(worldId, shopStage);
        if (string.IsNullOrEmpty(rulesKey))
            return result;

        int seed = ComputeShopSeed(worldId, rerollCount);
        var rng = new System.Random(seed);

        List<ModuleDefinition> candidates = BuildNonOwnedCandidates(catalogModules, isOwned);
        if (candidates.Count == 0)
            return result;

        SplitCandidatesByTier(
            candidates,
            out List<ModuleDefinition> tier1Candidates,
            out List<ModuleDefinition> tier2Candidates,
            out List<ModuleDefinition> tier3Candidates);

        Pattern selectedPattern = PickPattern(rulesKey, rng);

        ApplyPattern(
            selectedPattern,
            tier1Candidates,
            tier2Candidates,
            tier3Candidates,
            result,
            maxOfferCount,
            rng);

        FillRemaining(
            tier1Candidates,
            tier2Candidates,
            tier3Candidates,
            result,
            maxOfferCount,
            rng);

        return result;
    }

    // ---------------------------------------------------------------------
    // CONSTRUCTION DE L'OFFRE
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne la liste des modules candidats :
    /// tous les modules valides du catalogue qui ne sont pas déjà possédés.
    /// </summary>
    private static List<ModuleDefinition> BuildNonOwnedCandidates(
        List<ModuleDefinition> catalogModules,
        Func<string, bool> isOwned)
    {
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

        return candidates;
    }

    /// <summary>
    /// Répartit les candidats en trois buckets : T1 / T2 / T3+.
    /// </summary>
    private static void SplitCandidatesByTier(
        List<ModuleDefinition> candidates,
        out List<ModuleDefinition> tier1,
        out List<ModuleDefinition> tier2,
        out List<ModuleDefinition> tier3)
    {
        tier1 = new List<ModuleDefinition>();
        tier2 = new List<ModuleDefinition>();
        tier3 = new List<ModuleDefinition>();

        for (int i = 0; i < candidates.Count; i++)
        {
            ModuleDefinition def = candidates[i];
            int tier = Mathf.Max(1, def.tier);

            if (tier == 1)
                tier1.Add(def);
            else if (tier == 2)
                tier2.Add(def);
            else
                tier3.Add(def);
        }
    }

    /// <summary>
    /// Applique le pattern choisi en tirant dans chaque bucket.
    /// </summary>
    private static void ApplyPattern(
        Pattern pattern,
        List<ModuleDefinition> tier1,
        List<ModuleDefinition> tier2,
        List<ModuleDefinition> tier3,
        List<ModuleDefinition> result,
        int maxOfferCount,
        System.Random rng)
    {
        TakeSeeded(tier1, pattern.t1, result, maxOfferCount, rng);
        TakeSeeded(tier2, pattern.t2, result, maxOfferCount, rng);
        TakeSeeded(tier3, pattern.t3, result, maxOfferCount, rng);
    }

    /// <summary>
    /// Complète l'offre avec les modules restants si le pattern n'a pas suffi.
    /// Ordre de fallback :
    /// - T1
    /// - T2
    /// - T3
    /// </summary>
    private static void FillRemaining(
        List<ModuleDefinition> tier1,
        List<ModuleDefinition> tier2,
        List<ModuleDefinition> tier3,
        List<ModuleDefinition> result,
        int maxOfferCount,
        System.Random rng)
    {
        FillSeeded(tier1, result, maxOfferCount, rng);
        FillSeeded(tier2, result, maxOfferCount, rng);
        FillSeeded(tier3, result, maxOfferCount, rng);
    }

    // ---------------------------------------------------------------------
    // CLÉ DE RÈGLES / PATTERNS
    // ---------------------------------------------------------------------

    /// <summary>
    /// Construit la clé explicite utilisée dans le JSON.
    /// Exemples :
    /// - W1_START
    /// - W1_MID
    ///
    /// Important :
    /// - Le shop doit toujours avoir un ShopStage explicite.
    /// - Aucun fallback implicite n'est autorisé ici.
    /// </summary>
    private static string BuildRulesKey(string worldId, ShopStage shopStage)
    {
        string resolvedWorldId = string.IsNullOrWhiteSpace(worldId) ? "W1" : worldId.Trim();

        if (shopStage == ShopStage.None)
        {
            Debug.LogError("[ModulesShopOfferRules] ShopStage.None invalide pour la construction des règles de shop.");
            return null;
        }

        return resolvedWorldId + "_" + shopStage.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Choisit un pattern pondéré pour une clé de règles donnée.
    /// Si aucune règle n'est trouvée, retourne un pattern vide et log une erreur.
    /// </summary>
    private static Pattern PickPattern(string rulesKey, System.Random rng)
    {
        List<PatternRule> rules = GetPatternRulesForKey(rulesKey);

        if (rules == null || rules.Count == 0)
        {
            Debug.LogError("[ModulesShopOfferRules] Aucune règle trouvée pour la clé: " + rulesKey);
            return Pattern.Empty;
        }

        float totalWeight = 0f;
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i] == null)
                continue;

            totalWeight += Mathf.Max(0f, rules[i].weight);
        }

        if (totalWeight <= 0f)
        {
            Debug.LogError("[ModulesShopOfferRules] Somme des poids invalide pour la clé: " + rulesKey);
            return Pattern.Empty;
        }

        float randomValue = (float)rng.NextDouble() * totalWeight;
        float accumulatedWeight = 0f;

        for (int i = 0; i < rules.Count; i++)
        {
            PatternRule rule = rules[i];
            if (rule == null)
                continue;

            accumulatedWeight += Mathf.Max(0f, rule.weight);
            if (randomValue <= accumulatedWeight)
                return new Pattern(rule.t1, rule.t2, rule.t3);
        }

        PatternRule lastRule = rules[rules.Count - 1];
        if (lastRule == null)
            return Pattern.Empty;

        return new Pattern(lastRule.t1, lastRule.t2, lastRule.t3);
    }

    /// <summary>
    /// Retourne les règles associées à une clé explicite.
    /// Aucun fallback n'est utilisé :
    /// si la clé n'existe pas, on retourne null.
    /// </summary>
    private static List<PatternRule> GetPatternRulesForKey(string rulesKey)
    {
        if (!EnsureRulesLoaded() || cachedRules == null)
            return null;

        if (string.IsNullOrWhiteSpace(rulesKey) || cachedRules.worldRules == null)
            return null;

        for (int i = 0; i < cachedRules.worldRules.Count; i++)
        {
            WorldRule worldRule = cachedRules.worldRules[i];
            if (worldRule == null)
                continue;

            if (!string.Equals(worldRule.worldId, rulesKey, StringComparison.Ordinal))
                continue;

            if (worldRule.patterns != null && worldRule.patterns.Count > 0)
                return worldRule.patterns;

            return null;
        }

        return null;
    }

    // ---------------------------------------------------------------------
    // CHARGEMENT JSON
    // ---------------------------------------------------------------------

    /// <summary>
    /// Charge les règles JSON une seule fois et les garde en cache.
    /// </summary>
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

    // ---------------------------------------------------------------------
    // RNG / SEED
    // ---------------------------------------------------------------------

    /// <summary>
    /// Calcule un seed local pour le shop.
    ///
    /// Objectifs :
    /// - stable au sein d'une même run
    /// - différent par node
    /// - différent à chaque reroll
    ///
    /// Fallback debug :
    /// - si aucun runId n'est disponible, on utilise un seed volatil
    /// </summary>
    private static int ComputeShopSeed(string worldId, int rerollCount)
    {
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
            // Défensif :
            // ce script de règles ne doit jamais throw à cause d'un accès save.
        }

        string resolvedWorldId = string.IsNullOrEmpty(worldId) ? "W1" : worldId;
        int resolvedRerollCount = Mathf.Max(0, rerollCount);

        if (string.IsNullOrEmpty(runId))
        {
            long ticks = DateTime.UtcNow.Ticks;

            unchecked
            {
                int volatileSeed = (int)(ticks ^ (ticks >> 32) ^ Time.frameCount);
                return StableHash($"{resolvedWorldId}:{nodeIndex}:{resolvedRerollCount}:SHOP:{volatileSeed}");
            }
        }

        return StableHash($"{runId}:{resolvedWorldId}:{nodeIndex}:{resolvedRerollCount}:SHOP");
    }

    /// <summary>
    /// Hash simple et stable pour dériver un seed entier depuis une string.
    /// </summary>
    public static int StableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];

            return hash;
        }
    }

    // ---------------------------------------------------------------------
    // SÉLECTION SEEDED
    // ---------------------------------------------------------------------

    /// <summary>
    /// Mélange une liste avec le RNG local.
    /// </summary>
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

    /// <summary>
    /// Prend une quantité demandée dans un bucket après mélange seedé.
    /// Les éléments pris sont retirés du bucket.
    /// </summary>
    private static void TakeSeeded(
        List<ModuleDefinition> bucket,
        int amount,
        List<ModuleDefinition> result,
        int maxOfferCount,
        System.Random rng)
    {
        if (bucket == null || bucket.Count == 0)
            return;

        if (amount <= 0)
            return;

        if (result == null || result.Count >= maxOfferCount)
            return;

        Shuffle(bucket, rng);

        int takeCount = Mathf.Min(amount, bucket.Count);
        for (int i = 0; i < takeCount && result.Count < maxOfferCount; i++)
            result.Add(bucket[i]);

        bucket.RemoveRange(0, takeCount);
    }

    /// <summary>
    /// Complète l'offre avec tout ce qui reste dans un bucket, après mélange seedé.
    /// Les éléments ajoutés sont retirés du bucket.
    /// </summary>
    private static void FillSeeded(
        List<ModuleDefinition> bucket,
        List<ModuleDefinition> result,
        int maxOfferCount,
        System.Random rng)
    {
        if (bucket == null || bucket.Count == 0)
            return;

        if (result == null || result.Count >= maxOfferCount)
            return;

        Shuffle(bucket, rng);

        int index = 0;
        while (index < bucket.Count && result.Count < maxOfferCount)
        {
            result.Add(bucket[index]);
            index++;
        }

        if (index > 0)
            bucket.RemoveRange(0, index);
    }

    // ---------------------------------------------------------------------
    // MODÈLES INTERNES
    // ---------------------------------------------------------------------

    /// <summary>
    /// Pattern de distribution T1 / T2 / T3.
    /// </summary>
    private struct Pattern
    {
        public int t1;
        public int t2;
        public int t3;

        public static Pattern Empty => new Pattern(0, 0, 0);

        public Pattern(int t1, int t2, int t3)
        {
            this.t1 = Mathf.Max(0, t1);
            this.t2 = Mathf.Max(0, t2);
            this.t3 = Mathf.Max(0, t3);
        }
    }

    /// <summary>
    /// Racine JSON du fichier de règles.
    /// </summary>
    [Serializable]
    private class RulesRoot
    {
        public List<WorldRule> worldRules = new List<WorldRule>();
        public List<PatternRule> defaultPatterns = new List<PatternRule>();
    }

    /// <summary>
    /// Bloc de règles pour une clé explicite de shop.
    /// Exemples :
    /// - W1_START
    /// - W1_MID
    /// </summary>
    [Serializable]
    private class WorldRule
    {
        public string worldId;
        public List<PatternRule> patterns = new List<PatternRule>();
    }

    /// <summary>
    /// Ligne de pattern dans le JSON.
    /// </summary>
    [Serializable]
    private class PatternRule
    {
        public int t1;
        public int t2;
        public int t3;
        public float weight;
    }
}