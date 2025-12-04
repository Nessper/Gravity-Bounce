using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de donnees des personnages de l'equipage / interlocuteurs.
/// Permet de retrouver un CrewCharacter a partir de son id logique
/// (utilise dans le JSON des dialogues comme speakerId).
/// </summary>
[CreateAssetMenu(menuName = "Narration/Crew Database", fileName = "CrewDatabase")]
public class CrewDatabase : ScriptableObject
{
    [Tooltip("Liste des personnages disponibles (Captain, Mike, Leigh, Rye, Operator, etc.).")]
    public CrewCharacter[] characters;

    // Dictionnaire interne pour les lookups rapides par id.
    private Dictionary<string, CrewCharacter> lookup;

    /// <summary>
    /// Reconstruit le dictionnaire a chaque activation de l'asset
    /// (en mode play, recompile, etc.).
    /// </summary>
    private void OnEnable()
    {
        BuildLookup();
    }

    /// <summary>
    /// Construit le dictionnaire id -> CrewCharacter a partir du tableau characters.
    /// Ignore les entrees nulles ou sans id.
    /// </summary>
    private void BuildLookup()
    {
        lookup = new Dictionary<string, CrewCharacter>();

        if (characters == null)
            return;

        for (int i = 0; i < characters.Length; i++)
        {
            CrewCharacter c = characters[i];
            if (c == null)
                continue;

            if (string.IsNullOrEmpty(c.id))
                continue;

            if (lookup.ContainsKey(c.id))
            {
                // On evite de crasher, mais on log pour t'aider en cas de doublon.
                Debug.LogWarning("CrewDatabase: id duplique detecte : " + c.id);
                continue;
            }

            lookup.Add(c.id, c);
        }
    }

    /// <summary>
    /// Retourne le CrewCharacter correspondant a l'id, ou null si introuvable.
    /// </summary>
    public CrewCharacter GetCharacter(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (lookup == null)
        {
            BuildLookup();
        }

        CrewCharacter result;
        if (lookup.TryGetValue(id, out result))
        {
            return result;
        }

        return null;
    }
}
