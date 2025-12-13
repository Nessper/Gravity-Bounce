using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Responsable du chargement et de la sauvegarde des donnees persistantes du jeu.
/// Utilise PlayerPrefs avec un JSON unique.
/// </summary>
public class SaveManager : MonoBehaviour
{
    private const string PlayerPrefsKey = "GameSave_v1";

    public static SaveManager Instance { get; private set; }

    /// <summary>
    /// Donnees actuellement chargees en memoire.
    /// </summary>
    public GameSaveData Current { get; private set; }

    private void Awake()
    {
        // Singleton simple
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // IMPORTANT : ce manager doit survivre aux changements de scene,
        // sinon la persistance devient aleatoire selon le flow.
        DontDestroyOnLoad(gameObject);

        Load();
    }

    /// <summary>
    /// Charge les donnees depuis PlayerPrefs, ou cree des donnees par defaut.
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
                    Debug.LogWarning("[SaveManager] Erreur lors du parsing du JSON, creation d'une nouvelle sauvegarde.");
                }
            }
        }

        if (loaded == null)
        {
            loaded = CreateDefaultSave();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        Current = loaded;

        EnsureBaseShipUnlocked();
        EnsureRunStateReady();
        ApplyAbortPenaltyIfNeeded();

        Debug.Log("[SaveManager] Sauvegarde chargee. selectedShipId = " + Current.selectedShipId);
    }

    /// <summary>
    /// Sauvegarde les donnees courantes dans PlayerPrefs.
    /// </summary>
    public void Save()
    {
        if (Current == null)
        {
            Debug.LogWarning("[SaveManager] Save appele mais Current est null.");
            return;
        }

        string json = JsonUtility.ToJson(Current);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Reinitialise completement la sauvegarde (profil par defaut).
    /// </summary>
    public void ResetSave()
    {
        Current = CreateDefaultSave();
        EnsureBaseShipUnlocked();
        EnsureRunStateReady();
        Save();
    }

    /// <summary>
    /// Reinitialise uniquement l'etat de run (campagne).
    /// </summary>
    public void ResetRunState()
    {
        EnsureRunStateReady();

        RunStateData run = Current.runState;

        run.hasOngoingRun = false;
        run.currentShipId = Current.selectedShipId;
        run.currentWorld = 1;
        run.currentLevelIndex = 0;
        run.currentLevelId = "W1-L1";

        run.remainingHullInRun = 0;
        run.currentRunScore = 0;
        run.levelsClearedInRun = 0;

        run.remainingContractLives = 3;

        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        Save();
    }

    // --------------------------------------------------------------------
    // RUN STATE HELPERS (Hull / ContractLives)
    // --------------------------------------------------------------------

    public int GetRemainingHullInRun()
    {
        EnsureRunStateReady();
        return Mathf.Max(0, Current.runState.remainingHullInRun);
    }

    public void SetRemainingHullInRun(int hull)
    {
        EnsureRunStateReady();
        Current.runState.remainingHullInRun = Mathf.Max(0, hull);
        Save();
    }

    public int GetRemainingContractLives()
    {
        EnsureRunStateReady();
        return Mathf.Max(0, Current.runState.remainingContractLives);
    }

    public void SetRemainingContractLives(int lives)
    {
        EnsureRunStateReady();
        Current.runState.remainingContractLives = Mathf.Max(0, lives);
        Save();
    }

    public RunStateData GetRunState()
    {
        EnsureRunStateReady();
        return Current.runState;
    }

    private void EnsureRunStateReady()
    {
        if (Current == null)
            Current = CreateDefaultSave();

        if (Current.runState == null)
            Current.runState = new RunStateData();
    }

    // --------------------------------------------------------------------
    // BEST SCORE
    // --------------------------------------------------------------------

    public int GetBestRunScore()
    {
        if (Current == null)
            return 0;

        return Mathf.Max(0, Current.bestRunScore);
    }

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

    // --------------------------------------------------------------------
    // DEFAULT + MIGRATION LIGHT
    // --------------------------------------------------------------------

    private GameSaveData CreateDefaultSave()
    {
        var data = new GameSaveData();

        data.profileId = "DefaultProfile";
        data.selectedShipId = "CORE_SCOUT";

        data.unlockedShips = new List<string>();
        data.unlockedShips.Add("CORE_SCOUT");

        data.runState = new RunStateData();
        data.runState.hasOngoingRun = false;
        data.runState.levelInProgress = false;
        data.runState.abortPenaltyArmed = false;

        data.runState.remainingContractLives = 3;
        data.bestRunScore = 0;

        return data;
    }

    private void EnsureBaseShipUnlocked()
    {
        if (Current == null)
            return;

        if (Current.unlockedShips == null)
            Current.unlockedShips = new List<string>();

        if (!Current.unlockedShips.Contains("CORE_SCOUT"))
            Current.unlockedShips.Add("CORE_SCOUT");

        if (string.IsNullOrEmpty(Current.selectedShipId))
            Current.selectedShipId = "CORE_SCOUT";
    }

    private void ApplyAbortPenaltyIfNeeded()
    {
        if (Current == null || Current.runState == null)
            return;

        var run = Current.runState;

        if (!run.hasOngoingRun)
            return;

        if (!run.levelInProgress)
            return;

        if (!run.abortPenaltyArmed)
            return;

        // Quit en plein gameplay => Hull -1
        run.remainingHullInRun = Mathf.Max(0, run.remainingHullInRun - 1);

        // Reset des flags pour éviter double pénalité
        run.levelInProgress = false;
        run.abortPenaltyArmed = false;

        Save();
    }

}
