using System;
using System.Collections.Generic;

/// <summary>
/// Contient toutes les données persistantes du jeu.
/// Cette classe est sérialisée en JSON et stockée dans PlayerPrefs.
/// </summary>
[Serializable]
public class GameSaveData
{
    /// <summary>
    /// Identifiant du profil du joueur.
    /// Utile si un jour tu ajoutes plusieurs profils.
    /// </summary>
    public string profileId = "DefaultProfile";

    /// <summary>
    /// Identifiant du dernier vaisseau sélectionné.
    /// </summary>
    public string selectedShipId = "CORE_SCOUT";

    /// <summary>
    /// Liste des vaisseaux débloqués.
    /// </summary>
    public List<string> unlockedShips = new List<string>();

    /// <summary>
    /// Etat de la campagne en cours (run).
    /// </summary>
    public RunStateData runState = new RunStateData();

    // Nouveau : meilleurs scores
    public List<LevelBestScoreEntry> levelBestScores = new List<LevelBestScoreEntry>();

    // Meilleur score total de run (somme des niveaux avant Game Over)
    public int bestRunScore = 0;

    // --------------------------------------------------------------------
    // BEST SCORES PAR NIVEAU
    // --------------------------------------------------------------------

    /// <summary>
    /// Retourne le best score connu pour ce niveau, ou 0 s'il n'existe pas.
    /// </summary>
    public int GetBestScoreForLevel(string levelId)
    {
        if (string.IsNullOrEmpty(levelId) || levelBestScores == null)
            return 0;

        for (int i = 0; i < levelBestScores.Count; i++)
        {
            var entry = levelBestScores[i];
            if (entry != null && entry.levelId == levelId)
                return entry.bestScore;
        }

        return 0;
    }

    /// <summary>
    /// Met à jour le best score pour un niveau donné.
    /// Ne remplace la valeur que si newBestScore est supérieur.
    /// </summary>
    public void SetBestScoreForLevel(string levelId, int newBestScore)
    {
        if (string.IsNullOrEmpty(levelId))
            return;

        if (newBestScore < 0)
            newBestScore = 0;

        if (levelBestScores == null)
            levelBestScores = new List<LevelBestScoreEntry>();

        for (int i = 0; i < levelBestScores.Count; i++)
        {
            var entry = levelBestScores[i];
            if (entry != null && entry.levelId == levelId)
            {
                if (newBestScore > entry.bestScore)
                {
                    entry.bestScore = newBestScore;
                }
                return;
            }
        }

        // Si on arrive ici : aucune entrée existante -> on en crée une.
        var newEntry = new LevelBestScoreEntry
        {
            levelId = levelId,
            bestScore = newBestScore
        };
        levelBestScores.Add(newEntry);
    }
}

/// <summary>
/// Etat de la campagne en cours (run).
/// C'est ce qui permet de savoir si un run est en cours,
/// sur quel monde/niveau, avec combien de vies, etc.
/// </summary>
[Serializable]
public class RunStateData
{
    /// <summary>
    /// Indique s'il y a un run en cours.
    /// </summary>
    public bool hasOngoingRun;

    /// <summary>
    /// Identifiant du vaisseau utilisé pour ce run.
    /// </summary>
    public string currentShipId;

    /// <summary>
    /// Monde actuel (index ou identifiant simple).
    /// Exemple: 1 pour le premier monde, 2 pour le second, etc.
    /// </summary>
    public int currentWorld;

    /// <summary>
    /// Index du niveau actuel dans le monde (0 pour le premier niveau du monde, etc.).
    /// </summary>
    public int currentLevelIndex;

    /// <summary>
    /// Identifiant du niveau courant, tel que défini dans le JSON (ex: "W1-L1").
    /// Permet de savoir exactement quel fichier de niveau charger.
    /// </summary>
    public string currentLevelId = "";

    /// <summary>
    /// Nombre de vies restantes dans la campagne.
    /// </summary>
    public int remainingLivesInRun;

    /// <summary>
    /// Score cumulé pour le monde en cours.
    /// </summary>
    public int currentWorldScore;

    /// <summary>
    /// Score cumulé pour la campagne en cours.
    /// </summary>
    public int currentRunScore;
 
    /// <summary>
    /// Nombre de niveaux terminés dans ce run.
    /// </summary>
    public int levelsClearedInRun;

    /// <summary>
    /// Indique si un level est en cours.
    /// Sert pour la règle "quit en plein level = défaite".
    /// </summary>
    /// <summary>
    /// Indique si un level est en cours.
    /// Sert pour la règle "quit en plein level = défaite".
    /// </summary>
    public bool levelInProgress;

    /// <summary>
    /// Indique si la pénalité d'abandon doit s'appliquer
    /// si le jeu se ferme alors que levelInProgress est encore true.
    /// On ne l'arme qu'une fois que la partie est vraiment commencée.
    /// </summary>
    public bool abortPenaltyArmed;
}

[System.Serializable]
public class LevelBestScoreEntry
{
    public string levelId;
    public int bestScore;
}


