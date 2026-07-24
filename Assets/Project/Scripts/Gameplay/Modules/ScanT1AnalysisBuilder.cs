using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produit le briefing factuel du Scan T1 depuis le plan de mission.
/// Le calcul reprend les quotas du spawner (duree effective, poids, cadence
/// et spawns forces) sans dependre d'une partie en cours.
/// </summary>
public static class ScanT1AnalysisBuilder
{
    private sealed class PhasePlan
    {
        public int Index;
        public Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public static string Build(LevelData data, RunSessionState runSessionState)
    {
        if (data == null || data.Phases == null || data.Phases.Length == 0 || data.MainObjective == null)
            return null;

        List<PhasePlan> plans = BuildPlans(data, ResolveDuration(runSessionState));
        if (plans.Count == 0)
            return null;

        int useful = 0;
        int black = 0;

        for (int i = 0; i < plans.Count; i++)
        {
            foreach (KeyValuePair<string, int> entry in plans[i].Counts)
            {
                if (IsBlack(entry.Key))
                    black += entry.Value;
                else
                    useful += entry.Value;
            }
        }

        List<string> lines = new List<string>(3);
        int margin = useful - Mathf.Max(0, data.MainObjective.ThresholdCount);

        if (margin >= 0)
        {
            lines.Add(useful + " billes utiles sont détectées ; vous pouvez en laisser échapper jusqu’à " + margin + ".");
        }
        else
        {
            lines.Add(useful + " billes utiles sont détectées ; il en manque " + -margin + " pour atteindre l’objectif.");
        }

        if (black > 0)
        {
            string blackLine = black + " noires sont prévues";
            if (ModuleRuntimeStats.Instance != null && ModuleRuntimeStats.Instance.K1CooldownSec > 0f)
                blackLine += " ; K1 peut en neutraliser une";

            lines.Add(blackLine + ".");
        }

        string contextualLine = BuildContextualLine(data, plans, margin);
        if (!string.IsNullOrEmpty(contextualLine))
            lines.Add(contextualLine);

        return string.Join("\n", lines);
    }

    private static float ResolveDuration(RunSessionState runSessionState)
    {
        ShipDefinition ship = null;

        if (runSessionState != null && !string.IsNullOrEmpty(runSessionState.ShipId))
            ship = ShipCatalogService.GetById(runSessionState.ShipId);

        if (ship == null && ShipCatalogService.Catalog != null && ShipCatalogService.Catalog.ships != null && ShipCatalogService.Catalog.ships.Count > 0)
            ship = ShipCatalogService.Catalog.ships[0];

        float duration = ship != null ? ship.baseLevelDurationSec : 0f;

        if (ModuleRuntimeStats.Instance != null)
            duration += Mathf.Max(0f, ModuleRuntimeStats.Instance.LevelDurationBonusSec);

        return Mathf.Max(0f, duration);
    }

    private static List<PhasePlan> BuildPlans(LevelData data, float duration)
    {
        List<PhasePlan> result = new List<PhasePlan>();
        float totalWeight = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
            totalWeight += Mathf.Max(0f, data.Phases[i].Weight);

        if (duration <= 0f || totalWeight <= 0f)
            return result;

        float elapsed = 0f;

        for (int i = 0; i < data.Phases.Length; i++)
        {
            PhaseData phase = data.Phases[i];
            float phaseDuration = Mathf.Max(0f, phase.Weight) / totalWeight * duration;

            if (i == data.Phases.Length - 1)
                phaseDuration = Mathf.Max(0f, duration - elapsed);

            elapsed += phaseDuration;

            float interval = phase.Intervalle > 0f
                ? phase.Intervalle
                : data.Spawn != null && data.Spawn.Intervalle > 0f ? data.Spawn.Intervalle : 0.6f;
            int quota = Mathf.Max(0, Mathf.FloorToInt((phaseDuration - 0.0001f) / interval));

            PhasePlan plan = new PhasePlan { Index = i };
            AllocateMix(phase, quota, plan.Counts);
            ApplyForcedSpawns(phase, quota, plan.Counts);
            result.Add(plan);
        }

        return result;
    }

    private static void AllocateMix(PhaseData phase, int quota, Dictionary<string, int> counts)
    {
        if (phase == null || phase.Mix == null || quota <= 0)
            return;

        float totalWeight = 0f;
        List<PhaseMixEntry> entries = new List<PhaseMixEntry>();

        for (int i = 0; i < phase.Mix.Length; i++)
        {
            PhaseMixEntry entry = phase.Mix[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.BallId) || entry.Poids <= 0f)
                continue;

            entries.Add(entry);
            totalWeight += entry.Poids;
        }

        if (totalWeight <= 0f)
            return;

        int allocated = 0;
        List<KeyValuePair<PhaseMixEntry, float>> residuals = new List<KeyValuePair<PhaseMixEntry, float>>();

        for (int i = 0; i < entries.Count; i++)
        {
            float target = entries[i].Poids / totalWeight * quota;
            int count = Mathf.FloorToInt(target);
            Add(counts, entries[i].BallId, count);
            allocated += count;
            residuals.Add(new KeyValuePair<PhaseMixEntry, float>(entries[i], target - count));
        }

        residuals.Sort((a, b) => b.Value.CompareTo(a.Value));
        for (int i = allocated; i < quota; i++)
            Add(counts, residuals[(i - allocated) % residuals.Count].Key.BallId, 1);
    }

    // Un spawn force remplace une bille de la phase : le total reste inchange.
    private static void ApplyForcedSpawns(PhaseData phase, int quota, Dictionary<string, int> counts)
    {
        if (phase == null || phase.ForcedSpawns == null || quota <= 0)
            return;

        for (int i = 0; i < phase.ForcedSpawns.Length; i++)
        {
            ForcedSpawnEntry forced = phase.ForcedSpawns[i];
            if (forced == null || string.IsNullOrWhiteSpace(forced.BallId))
                continue;

            int count = Mathf.Min(Mathf.Max(0, forced.Count), quota);
            for (int k = 0; k < count; k++)
            {
                string replaced = FindMostCommonOtherBall(counts, forced.BallId);
                if (!string.IsNullOrEmpty(replaced))
                    Add(counts, replaced, -1);

                Add(counts, forced.BallId, 1);
            }
        }
    }

    private static string BuildContextualLine(LevelData data, List<PhasePlan> plans, int primaryMargin)
    {
        if (data.SecondaryObjectives == null)
            return null;

        for (int i = 0; i < data.SecondaryObjectives.Length; i++)
        {
            SecondaryObjectiveData objective = data.SecondaryObjectives[i];
            if (objective == null)
                continue;

            if (string.Equals(objective.Type, "MaxLost", StringComparison.OrdinalIgnoreCase) && objective.Threshold < primaryMargin)
                return "L’objectif secondaire réduit votre tolérance réelle à " + objective.Threshold + " billes perdues.";

            if (!string.Equals(objective.Type, "BallCount", StringComparison.OrdinalIgnoreCase))
                continue;

            int planned = CountObjectiveBalls(plans, objective);
            int secondaryMargin = planned - Mathf.Max(0, objective.Threshold);

            if (planned <= 0)
                continue;

            if (objective.PhaseIndex > 0 && secondaryMargin >= 0)
            {
                return "Jusqu’à " + secondaryMargin + " " + GetBallLabel(objective.TargetId, secondaryMargin) + " de " + GetMissionMoment(objective.PhaseIndex - 1, plans.Count) + " peuvent être manquées.";
            }

            if (planned == objective.Threshold && TryFindForcedMoment(data, objective.TargetId, out string moment))
                return "La seule " + GetBallLabel(objective.TargetId, 1) + " prévue apparaît " + moment + ".";
        }

        return null;
    }

    private static int CountObjectiveBalls(List<PhasePlan> plans, SecondaryObjectiveData objective)
    {
        int start = objective.PhaseIndex > 0 ? objective.PhaseIndex - 1 : 0;
        int end = objective.PhaseIndex > 0 ? start + 1 : plans.Count;
        int result = 0;

        for (int i = Mathf.Max(0, start); i < Mathf.Min(end, plans.Count); i++)
            if (plans[i].Counts.TryGetValue(objective.TargetId, out int count))
                result += count;

        return result;
    }

    private static bool TryFindForcedMoment(LevelData data, string ballId, out string moment)
    {
        for (int i = 0; i < data.Phases.Length; i++)
        {
            ForcedSpawnEntry[] forced = data.Phases[i].ForcedSpawns;
            if (forced == null)
                continue;

            for (int k = 0; k < forced.Length; k++)
            {
                if (forced[k] != null && string.Equals(forced[k].BallId, ballId, StringComparison.OrdinalIgnoreCase))
                {
                    moment = "en " + GetMissionMoment(i, data.Phases.Length) + " de mission";
                    return true;
                }
            }
        }

        moment = null;
        return false;
    }

    private static string GetMissionMoment(int phaseIndex, int phaseCount)
    {
        if (phaseIndex <= 0)
            return "debut";
        if (phaseIndex >= phaseCount - 1)
            return "fin";
        return "milieu";
    }

    private static string GetBallLabel(string ballId, int count)
    {
        string id = string.IsNullOrWhiteSpace(ballId) ? "bille" : ballId.ToLowerInvariant();
        if (id == "blue") return count == 1 ? "bleue" : "bleues";
        if (id == "red") return count == 1 ? "bille rouge" : "billes rouges";
        if (id == "white") return count == 1 ? "blanche" : "blanches";
        return count == 1 ? "bille" : "billes";
    }

    private static string FindMostCommonOtherBall(Dictionary<string, int> counts, string excluded)
    {
        string result = null;
        int highest = 0;
        foreach (KeyValuePair<string, int> entry in counts)
        {
            if (!string.Equals(entry.Key, excluded, StringComparison.OrdinalIgnoreCase) && entry.Value > highest)
            {
                result = entry.Key;
                highest = entry.Value;
            }
        }

        return result;
    }

    private static void Add(Dictionary<string, int> counts, string id, int value)
    {
        if (string.IsNullOrWhiteSpace(id) || value == 0)
            return;

        counts.TryGetValue(id, out int previous);
        counts[id] = Mathf.Max(0, previous + value);
    }

    private static bool IsBlack(string ballId)
    {
        return string.Equals(ballId, "black", StringComparison.OrdinalIgnoreCase);
    }
}
