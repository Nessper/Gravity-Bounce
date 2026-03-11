using System;

/// <summary>
/// Résultat de l'objectif principal calculé en fin de niveau.
/// </summary>
[Serializable]
public struct MainObjectiveResult
{
    public string Text;
    public int ThresholdPct;
    public int Required;
    public int Collected;
    public bool Achieved;
    public int BonusApplied;
}
