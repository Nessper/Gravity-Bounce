using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Types d'objectifs secondaires supportés.
/// - BallCount : collecter au moins X (>= Threshold)
/// - ComboCount : declencher au moins X combos (>= Threshold)
/// - MaxCount : ne pas depasser X collectes d'un type (<= Threshold)
/// - MaxLost : ne pas depasser X pertes (<= Threshold)
///
/// Notes design:
/// - MaxLost exclut les billes noires (elles ne comptent pas comme des pertes).
/// - TargetId peut etre "Any" ou un BallType ("White","Blue","Red","Black") ou un comboId.
/// - PhaseIndex (optionnel dans SecondaryObjectiveData) est 1-based (Phase 1,2,3...).
///
/// Refactor:
/// - Source de verite = ScoreManager via:
///   - OnFlushSnapshotRegistered (collectes via snapshots)
///   - OnBallLost (pertes via void/cleanup)
/// - Les combos restent notifies explicitement (car ScoreManager ne stocke pas les counts).
/// - Pour filtrer les pertes par phase (void n'a pas de snapshot), on maintient une phase courante 1-based.
/// </summary>
public enum SecondaryObjectiveType
{
    BallCount,
    ComboCount,
    MaxCount,
    MaxLost
}

/// <summary>
/// Etat runtime d'un objectif secondaire.
/// </summary>
public struct SecondaryObjectiveRuntime
{
    public SecondaryObjectiveData Definition;
    public SecondaryObjectiveType Type;

    public int CurrentValue;
    public bool Achieved;
}

/// <summary>
/// Resultat final d'un objectif secondaire.
/// </summary>
[Serializable]
public struct SecondaryObjectiveResult
{
    public string Text;
    public int Current;
    public int Required;
    public bool Achieved;
    public int AwardedScore;
}

public class SecondaryObjectivesManager
{
    // Definitions / runtime
    private readonly List<SecondaryObjectiveRuntime> objectives = new List<SecondaryObjectiveRuntime>();

    // Binding ScoreManager
    private ScoreManager scoreManager;
    private bool bound;

    // Aggregats (global + per phase)
    private readonly Dictionary<string, int> collectedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lostByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // phaseIndex1Based -> (type -> count)
    private readonly Dictionary<int, Dictionary<string, int>> collectedByTypePerPhase =
        new Dictionary<int, Dictionary<string, int>>();

    private readonly Dictionary<int, Dictionary<string, int>> lostByTypePerPhase =
        new Dictionary<int, Dictionary<string, int>>();

    // Combos counts
    private readonly Dictionary<string, int> comboCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Phase courante pour attribuer les pertes (void) a une phase.
    // 1-based: Phase 1 => 1. 0 => inconnu / pas de filtrage.
    private int currentPhaseIndex1Based = 0;

    // ---------------------------------------------------------------------
    // Setup / Reset
    // ---------------------------------------------------------------------

    /// <summary>
    /// Setup depuis LevelData.SecondaryObjectives. A appeler au debut du niveau.
    /// </summary>
    public void Setup(SecondaryObjectiveData[] definitions)
    {
        objectives.Clear();

        collectedByType.Clear();
        lostByType.Clear();
        collectedByTypePerPhase.Clear();
        lostByTypePerPhase.Clear();
        comboCounts.Clear();

        currentPhaseIndex1Based = 0;

        if (definitions == null || definitions.Length == 0)
            return;

        for (int i = 0; i < definitions.Length; i++)
        {
            SecondaryObjectiveData def = definitions[i];
            if (def == null)
                continue;

            if (!TryParseType(def.Type, out SecondaryObjectiveType parsed))
            {
                Debug.LogWarning("[SecondaryObjectivesManager] Type inconnu: '" + def.Type + "' (id=" + def.Id + "). Ignore.");
                continue;
            }

            SecondaryObjectiveRuntime rt = new SecondaryObjectiveRuntime
            {
                Definition = def,
                Type = parsed,
                CurrentValue = 0,
                Achieved = false
            };

            // Max* : vrai par defaut, peut echouer si on depasse
            if (parsed == SecondaryObjectiveType.MaxCount || parsed == SecondaryObjectiveType.MaxLost)
                rt.Achieved = true;

            objectives.Add(rt);
        }
    }

    /// <summary>
    /// Bind au ScoreManager (flush snapshots + pertes).
    /// </summary>
    public void Bind(ScoreManager sm)
    {
        if (bound)
            Unbind();

        scoreManager = sm;
        if (scoreManager == null)
            return;

        scoreManager.OnFlushSnapshotRegistered += HandleFlushSnapshot;
        scoreManager.OnBallLost += HandleBallLost;

        bound = true;
    }

    public void Unbind()
    {
        if (!bound)
            return;

        if (scoreManager != null)
        {
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushSnapshot;
            scoreManager.OnBallLost -= HandleBallLost;
        }

        scoreManager = null;
        bound = false;
    }

    /// <summary>
    /// Donne la phase courante pour attribuer les pertes void a une phase.
    /// 1-based: Phase 1 => 1.
    /// </summary>
    public void SetCurrentPhaseIndex1Based(int phaseIndex1Based)
    {
        currentPhaseIndex1Based = Mathf.Max(0, phaseIndex1Based);
    }

    // ---------------------------------------------------------------------
    // Intake events
    // ---------------------------------------------------------------------

    /// <summary>
    /// Collectes = uniquement via les flushs (BinSnapshot).
    /// </summary>
    private void HandleFlushSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0)
            return;

        int phase = Mathf.Max(0, snapshot.phaseIndex1Based);

        if (snapshot.parBallId != null)
        {
            foreach (var kv in snapshot.parBallId)
            {
                string type = kv.Key;
                int count = kv.Value;

                AddToDict(collectedByType, type, count);

                if (phase > 0)
                {
                    Dictionary<string, int> dict = GetOrCreatePhaseDict(collectedByTypePerPhase, phase);
                    AddToDict(dict, type, count);
                }
            }
        }

        RecomputeAllObjectives();
    }

    /// <summary>
    /// Pertes = via ScoreManager.RegisterLost(type) (VoidTrigger, cleanup, etc.).
    /// </summary>
    private void HandleBallLost(string ballType)
    {
        if (string.IsNullOrWhiteSpace(ballType))
            ballType = "Unknown";

        // On conserve l'info brute (y compris Black) pour debug,
        // mais MaxLost filtrera Black au calcul.
        AddToDict(lostByType, ballType, 1);

        int phase = currentPhaseIndex1Based;
        if (phase > 0)
        {
            Dictionary<string, int> dict = GetOrCreatePhaseDict(lostByTypePerPhase, phase);
            AddToDict(dict, ballType, 1);
        }

        RecomputeAllObjectives();
    }

    /// <summary>
    /// Combos : notifies explicitement depuis le gameplay.
    /// </summary>
    public void NotifyComboTriggered(string comboId)
    {
        if (string.IsNullOrWhiteSpace(comboId))
            return;

        AddToDict(comboCounts, comboId, 1);
        RecomputeAllObjectives();
    }

    // ---------------------------------------------------------------------
    // Evaluation
    // ---------------------------------------------------------------------

    private void RecomputeAllObjectives()
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            SecondaryObjectiveRuntime obj = objectives[i];
            obj.CurrentValue = ComputeCurrentValue(obj);
            obj.Achieved = ComputeAchieved(obj, obj.CurrentValue);
            objectives[i] = obj;
        }
    }

    private int ComputeCurrentValue(SecondaryObjectiveRuntime obj)
    {
        SecondaryObjectiveData def = obj.Definition;

        // PhaseIndex optionnel (1-based). <=0 => global.
        int phase = GetPhaseIndex1Based(def);

        switch (obj.Type)
        {
            case SecondaryObjectiveType.BallCount:
            case SecondaryObjectiveType.MaxCount:
                {
                    string target = def.TargetId;

                    Dictionary<string, int> dict = (phase > 0)
                        ? GetPhaseDictOrNull(collectedByTypePerPhase, phase)
                        : collectedByType;

                    if (string.Equals(target, "Any", StringComparison.OrdinalIgnoreCase))
                        return SumDict(dict);

                    return GetCount(dict, target);
                }

            case SecondaryObjectiveType.MaxLost:
                {
                    string target = def.TargetId;

                    Dictionary<string, int> dict = (phase > 0)
                        ? GetPhaseDictOrNull(lostByTypePerPhase, phase)
                        : lostByType;

                    if (dict == null)
                        return 0;

                    if (string.Equals(target, "Any", StringComparison.OrdinalIgnoreCase))
                    {
                        int sum = 0;
                        foreach (var kv in dict)
                        {
                            if (IsBlackType(kv.Key))
                                continue;
                            sum += kv.Value;
                        }
                        return sum;
                    }

                    if (IsBlackType(target))
                        return 0; // par design: Black ne compte pas comme perte

                    return GetCount(dict, target);
                }

            case SecondaryObjectiveType.ComboCount:
                {
                    string comboId = def.TargetId;
                    return GetCount(comboCounts, comboId);
                }

            default:
                return 0;
        }
    }

    private bool ComputeAchieved(SecondaryObjectiveRuntime obj, int current)
    {
        int threshold = Mathf.Max(0, obj.Definition.Threshold);

        switch (obj.Type)
        {
            case SecondaryObjectiveType.BallCount:
            case SecondaryObjectiveType.ComboCount:
                return current >= threshold;

            case SecondaryObjectiveType.MaxCount:
            case SecondaryObjectiveType.MaxLost:
                return current <= threshold;

            default:
                return false;
        }
    }

    // ---------------------------------------------------------------------
    // Results
    // ---------------------------------------------------------------------

    public List<SecondaryObjectiveResult> BuildResults()
    {
        List<SecondaryObjectiveResult> results = new List<SecondaryObjectiveResult>(objectives.Count);

        for (int i = 0; i < objectives.Count; i++)
        {
            SecondaryObjectiveRuntime obj = objectives[i];
            int threshold = Mathf.Max(0, obj.Definition.Threshold);

            results.Add(new SecondaryObjectiveResult
            {
                Text = obj.Definition.UiText,
                Current = obj.CurrentValue,
                Required = threshold,
                Achieved = obj.Achieved,
                AwardedScore = obj.Achieved ? obj.Definition.RewardScore : 0
            });
        }

        return results;
    }

    public int GetTotalRewardScore()
    {
        int total = 0;
        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i].Achieved)
                total += objectives[i].Definition.RewardScore;
        }
        return total;
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private bool TryParseType(string typeString, out SecondaryObjectiveType type)
    {
        switch (typeString)
        {
            case "BallCount": type = SecondaryObjectiveType.BallCount; return true;
            case "ComboCount": type = SecondaryObjectiveType.ComboCount; return true;
            case "MaxCount": type = SecondaryObjectiveType.MaxCount; return true;
            case "MaxLost": type = SecondaryObjectiveType.MaxLost; return true;
            default: type = default; return false;
        }
    }

    private static void AddToDict(Dictionary<string, int> dict, string key, int add)
    {
        if (dict == null || string.IsNullOrWhiteSpace(key))
            return;

        if (!dict.TryGetValue(key, out int v))
            dict[key] = add;
        else
            dict[key] = v + add;
    }

    private static int GetCount(Dictionary<string, int> dict, string key)
    {
        if (dict == null || string.IsNullOrWhiteSpace(key))
            return 0;

        return dict.TryGetValue(key, out int v) ? v : 0;
    }

    private static int SumDict(Dictionary<string, int> dict)
    {
        if (dict == null)
            return 0;

        int sum = 0;
        foreach (var kv in dict)
            sum += kv.Value;

        return sum;
    }

    private static Dictionary<string, int> GetOrCreatePhaseDict(
        Dictionary<int, Dictionary<string, int>> perPhase,
        int phaseIndex1Based)
    {
        if (!perPhase.TryGetValue(phaseIndex1Based, out Dictionary<string, int> dict) || dict == null)
        {
            dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            perPhase[phaseIndex1Based] = dict;
        }
        return dict;
    }

    private static Dictionary<string, int> GetPhaseDictOrNull(
        Dictionary<int, Dictionary<string, int>> perPhase,
        int phaseIndex1Based)
    {
        if (perPhase == null)
            return null;

        return perPhase.TryGetValue(phaseIndex1Based, out Dictionary<string, int> dict) ? dict : null;
    }

    private static bool IsBlackType(string typeKey)
    {
        return string.Equals(typeKey, "black", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPhaseIndex1Based(SecondaryObjectiveData def)
    {
        if (def == null)
            return 0;

        return Mathf.Max(0, def.PhaseIndex);
    }
}
