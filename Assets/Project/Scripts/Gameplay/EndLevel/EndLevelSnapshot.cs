using System;

[Serializable]
public class EndLevelSnapshot
{
    // Identité anti-dup / anti-replay (runId, nodeIndex, etc.)
    public EndLevelToken Token;

    // Redondant mais pratique pour reload LevelData / meta si besoin
    public string LevelId;

    // Données nécessaires à la cérémonie (raw score + combos + stats)
    public EndLevelStats Stats;

    // Objectif principal (UI + outcome)
    public MainObjectiveResult MainObjective;

    // Objectifs secondaires (UI + bonus goals)
    // Array = plus sûr que List pour JsonUtility dans les saves.
    public SecondaryObjectiveResult[] Secondary;

    // Indique si les rewards/progression ont été appliquées une seule fois.
    // False = on peut encore commit
    // True = commit déjà fait, ne pas redonner
    public bool RewardsCommitted;

    // Timestamp utile pour debug / audit
    public long EvaluatedTimestampUtc;
}
