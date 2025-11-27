using UnityEngine;

/// <summary>
/// Description d'un obstacle telle qu'elle vient du JSON.
/// Ne contient QUE des données, aucun comportement.
/// </summary>
[System.Serializable]
public class ObstaclePlacement
{
    // Id logique de l'obstacle (permet de choisir le prefab côté Unity).
    public string obstacleId;

    // Position locale par rapport au BoardRoot (en unités monde).
    public Vector3 localPosition;

    // Rotation locale en degrés (en pratique, surtout Z).
    public Vector3 localEulerAngles;

    // Optionnel : index de phase, si plus tard tu veux activer selon la phase.
    public int phaseIndex;
}
