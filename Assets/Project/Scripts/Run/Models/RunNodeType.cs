/// <summary>
/// Types de nodes possibles dans une run.
/// Pour l'instant, seul LEVEL est utilisé.
/// Les autres servent de contrat d'extension (shop, event, boss, etc.).
/// </summary>
public enum RunNodeType
{
    Level = 0,
    Shop = 1,
    Event = 2,
    Boss = 3,
    Ending = 4
}
