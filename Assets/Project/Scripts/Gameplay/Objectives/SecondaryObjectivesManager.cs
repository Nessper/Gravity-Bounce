using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Types d'objectifs secondaires supportés.
/// BallCount : basé sur le nombre de billes collectées.
/// ComboCount : basé sur le nombre de combos spécifiques déclenchés.
/// </summary>
public enum SecondaryObjectiveType
{
    BallCount,
    ComboCount
    // Plus tard : NoBlackLost, MaxLost, TimeUnder, etc.
}

/// <summary>
/// Etat runtime d'un objectif secondaire (progression pendant le niveau).
/// </summary>
public struct SecondaryObjectiveRuntime
{
    public SecondaryObjectiveData Definition;  // Données de base venant du JSON / LevelData
    public SecondaryObjectiveType Type;        // Type interprété à partir de Definition.Type
    public int CurrentValue;                   // Progression actuelle
    public bool Achieved;                      // True si le seuil est atteint ou dépassé
}

/// <summary>
/// Résultat final d'un objectif secondaire, utilisé pour l'UI de fin de niveau.
/// </summary>
public struct SecondaryObjectiveResult
{
    public string Text;         // Texte affiché dans l'UI
    public int Current;         // Valeur atteinte par le joueur
    public int Required;        // Valeur requise (Threshold)
    public bool Achieved;       // Objectif réussi ou non
    public int AwardedScore;    // Score attribué pour cet objectif (0 si raté)
}

/// <summary>
/// Manager logique des objectifs secondaires.
/// - Pas de MonoBehaviour : géré par LevelManager ou un autre orchestrateur.
/// - Ne modifie pas le gameplay en temps réel, ne fait que suivre la progression.
/// - Produit des résultats en fin de niveau pour l'UI et le calcul de score.
/// </summary>
public class SecondaryObjectivesManager
{
    // Liste interne des objectifs suivis pendant le niveau.
    private readonly List<SecondaryObjectiveRuntime> _objectives =
        new List<SecondaryObjectiveRuntime>();

    /// <summary>
    /// Initialise le manager à partir d'un tableau d'objectifs secondaires.
    /// Appelé en début de niveau (par exemple depuis LevelManager).
    /// </summary>
    public void Setup(SecondaryObjectiveData[] definitions)
    {
        _objectives.Clear();

        if (definitions == null || definitions.Length == 0)
            return;

        foreach (var def in definitions)
        {
            SecondaryObjectiveType parsedType;
            if (!TryParseType(def.Type, out parsedType))
            {
                Debug.LogWarning("[SecondaryObjectivesManager] Type inconnu pour l'objectif '" +
                                 def.Id + "' : '" + def.Type + "'. Objectif ignoré.");
                continue;
            }

            var runtime = new SecondaryObjectiveRuntime
            {
                Definition = def,
                Type = parsedType,
                CurrentValue = 0,
                Achieved = false
            };

            _objectives.Add(runtime);
        }
    }

    /// <summary>
    /// Doit être appelé lorsqu'une bille est collectée.
    /// ballTypeId : identifiant du type de bille (ex: "White", "Blue", "Black").
    /// </summary>
    public void OnBallCollected(string ballTypeId)
    {
        if (_objectives.Count == 0)
            return;

        for (int i = 0; i < _objectives.Count; i++)
        {
            var obj = _objectives[i];

            if (obj.Type != SecondaryObjectiveType.BallCount)
                continue;

            // TargetId :
            // - "Any" : n'importe quel type de bille
            // - sinon : doit matcher le type de bille (ex: "White")
            var target = obj.Definition.TargetId;
            if (target != "Any" && target != ballTypeId)
                continue;

            obj.CurrentValue++;

            if (!obj.Achieved && obj.CurrentValue >= obj.Definition.Threshold)
                obj.Achieved = true;

            _objectives[i] = obj;
        }
    }

    /// <summary>
    /// Doit être appelé lorsqu'un combo est déclenché.
    /// comboId : identifiant du combo (ex: "WhiteStreak", "SuperFlush").
    /// </summary>
    public void OnComboTriggered(string comboId)
    {
        if (_objectives.Count == 0)
            return;

        for (int i = 0; i < _objectives.Count; i++)
        {
            var obj = _objectives[i];

            if (obj.Type != SecondaryObjectiveType.ComboCount)
                continue;

            // On ne compte que les combos correspondant exactement au TargetId
            if (obj.Definition.TargetId != comboId)
                continue;

            obj.CurrentValue++;

            if (!obj.Achieved && obj.CurrentValue >= obj.Definition.Threshold)
                obj.Achieved = true;

            _objectives[i] = obj;
        }
    }

    /// <summary>
    /// Construit la liste des résultats finaux des objectifs secondaires.
    /// A appeler en fin de niveau, avant d'afficher l'EndLevelUI.
    /// </summary>
    public List<SecondaryObjectiveResult> BuildResults()
    {
        var results = new List<SecondaryObjectiveResult>(_objectives.Count);

        foreach (var obj in _objectives)
        {
            var res = new SecondaryObjectiveResult
            {
                Text = obj.Definition.UiText,
                Current = obj.CurrentValue,
                Required = obj.Definition.Threshold,
                Achieved = obj.Achieved,
                AwardedScore = obj.Achieved ? obj.Definition.RewardScore : 0
            };

            results.Add(res);
        }

        return results;
    }

    /// <summary>
    /// Retourne la somme des scores de récompense pour tous les objectifs atteints.
    /// Utile si tu veux ajouter cette valeur au score final pendant la cérémonie.
    /// </summary>
    public int GetTotalRewardScore()
    {
        int total = 0;

        foreach (var obj in _objectives)
        {
            if (obj.Achieved)
                total += obj.Definition.RewardScore;
        }

        return total;
    }

    /// <summary>
    /// Helper interne : convertit une string JSON ("BallCount", "ComboCount")
    /// en enum SecondaryObjectiveType.
    /// Renvoie false si le type est inconnu.
    /// </summary>
    private bool TryParseType(string typeString, out SecondaryObjectiveType type)
    {
        switch (typeString)
        {
            case "BallCount":
                type = SecondaryObjectiveType.BallCount;
                return true;
            case "ComboCount":
                type = SecondaryObjectiveType.ComboCount;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
