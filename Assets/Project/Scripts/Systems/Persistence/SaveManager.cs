using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Responsable du chargement et de la sauvegarde des données persistantes du jeu.
/// Utilise PlayerPrefs avec un JSON unique.
/// </summary>
public class SaveManager : MonoBehaviour
{
    /// <summary>
    /// Clé PlayerPrefs utilisée pour stocker le JSON.
    /// Si tu changes radicalement la structure de GameSaveData,
    /// incrémente la version (par exemple "GameSave_v2").
    /// </summary>
    private const string PlayerPrefsKey = "GameSave_v1";

    /// <summary>
    /// Instance unique accessible globalement.
    /// </summary>
    public static SaveManager Instance { get; private set; }

    /// <summary>
    /// Données actuellement chargées en mémoire.
    /// </summary>
    public GameSaveData Current { get; private set; }

    private void Awake()
    {
        // Pattern simple de singleton pour ce manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);

        Load();
    }

    /// <summary>
    /// Charge les données depuis PlayerPrefs, ou crée des données par défaut.
    /// </summary>
    public void Load()
    {
        GameSaveData loaded = null;

        if (PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            string json = PlayerPrefs.GetString(PlayerPrefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    loaded = JsonUtility.FromJson<GameSaveData>(json);
                }
                catch
                {
                    Debug.LogWarning("SaveManager: erreur lors du parsing du JSON, création d'une nouvelle sauvegarde.");
                }
            }
        }

        // Si rien en prefs ou erreur de parsing, on crée un profil par défaut.
        if (loaded == null)
        {
            loaded = CreateDefaultSave();

            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        Current = loaded;

        EnsureBaseShipUnlocked();

        Debug.Log("SaveManager: sauvegarde chargée. selectedShipId = " + Current.selectedShipId);
    }

    /// <summary>
    /// Sauvegarde les données courantes dans PlayerPrefs.
    /// </summary>
    public void Save()
    {
        if (Current == null)
        {
            Debug.LogWarning("SaveManager.Save appelé mais Current est null.");
            return;
        }

        string json = JsonUtility.ToJson(Current);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Réinitialise complètement les données sauvegardées
    /// en recréant un profil par défaut, puis en sauvegardant.
    /// </summary>
    public void ResetSave()
    {
        Current = CreateDefaultSave();
        Save();
    }

    /// <summary>
    /// Réinitialise la partie
    /// </summary>
    public void ResetRunState()
    {
        if (Current == null)
            Current = CreateDefaultSave();

        if (Current.runState == null)
            Current.runState = new RunStateData();

        var run = Current.runState;

        // On remet juste l'état de la campagne, pas les best scores.
        run.hasOngoingRun = false;
        run.currentShipId = Current.selectedShipId; // ou "CORE_SCOUT"
        run.currentWorld = 1;
        run.currentLevelIndex = 0;
        run.currentLevelId = "W1-L1";

        run.remainingHullInRun = 0;
        run.currentRunScore = 0;
        run.levelsClearedInRun = 0;

        // Contrat : on repart toujours à 3 vies au début d'une nouvelle run.
        run.remainingContractLives = 3;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        Save();
    }

    /// <summary>
    /// Crée un profil par défaut (vaisseau de base débloqué, aucune progression).
    /// </summary>
    private GameSaveData CreateDefaultSave()
    {
        var data = new GameSaveData();

        data.profileId = "DefaultProfile";
        data.selectedShipId = "CORE_SCOUT";

        data.unlockedShips.Add("CORE_SCOUT");

        data.runState = new RunStateData();
        data.runState.hasOngoingRun = false;
        data.runState.levelInProgress = false;
        data.runState.abortPenaltyArmed = false;

        // Nouveau système : contrat à 3 vies par défaut.
        data.runState.remainingContractLives = 3;

        // Best score par défaut
        data.bestRunScore = 0;

        return data;
    }

    /// <summary>
    /// S'assure que le vaisseau de base est bien débloqué,
    /// même si la sauvegarde est ancienne ou partielle.
    /// </summary>
    private void EnsureBaseShipUnlocked()
    {
        if (Current == null)
        {
            return;
        }

        if (!Current.unlockedShips.Contains("CORE_SCOUT"))
        {
            Current.unlockedShips.Add("CORE_SCOUT");
        }

        if (string.IsNullOrEmpty(Current.selectedShipId))
        {
            Current.selectedShipId = "CORE_SCOUT";
        }
    }

    /// <summary>
    /// Retourne le meilleur score de run global (tous niveaux confondus),
    /// ou 0 si aucun n'a encore été enregistré.
    /// </summary>
    public int GetBestRunScore()
    {
        if (Current == null)
            return 0;

        return Mathf.Max(0, Current.bestRunScore);
    }

    /// <summary>
    /// Tente de mettre à jour le meilleur score de run global.
    /// Retourne true si un nouveau record est établi.
    /// </summary>
    public bool TryUpdateBestRunScore(int runScore)
    {
        if (Current == null)
            return false;

        int clamped = Mathf.Max(0, runScore);
        if (clamped > Current.bestRunScore)
        {
            Current.bestRunScore = clamped;
            Save();
            return true;
        }

        return false;
    }

}
