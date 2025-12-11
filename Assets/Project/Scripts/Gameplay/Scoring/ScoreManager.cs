using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    // =====================================================
    //  PLANNED TARGET (DONNEES PREVUES DU NIVEAU)
    // =====================================================

    [Header("Planned Target")]
    [SerializeField]
    private int totalBillesPrevues;
    // Nombre de billes PREVUES pour le niveau (plan théorique),
    // en excluant les billes noires.
    // Cette valeur doit être alimentée par le plan du niveau
    // (ex: BallSpawner.PlannedNonBlackSpawnCount).
    public int TotalBillesPrevues => totalBillesPrevues;

    // =====================================================
    //  MAIN OBJECTIVE (SEUIL PRINCIPAL DU NIVEAU)
    // =====================================================

    [Header("Main Objective")]
    [SerializeField]
    private int objectiveThreshold;
    // Nombre de billes NON NOIRES à collecter pour atteindre l'objectif principal (ThresholdCount).
    public int ObjectiveThreshold => objectiveThreshold;

    // Flag interne pour éviter de déclencher plusieurs fois l'évènement.
    private bool goalReached = false;

    [Serializable]
    public class SimpleEvent : UnityEvent { }

    // Évènement déclenché une seule fois lorsque le seuil est atteint ou dépassé.
    public SimpleEvent onGoalReached = new SimpleEvent();

    // =====================================================
    //  RUNTIME STATE (ETAT EN COURS DE PARTIE)
    // =====================================================

    // Nombre total de billes collectées (tous types confondus).
    private int totalBilles;

    // Nombre total de billes collectées HORS NOIRES.
    private int totalBillesNonNoires;

    // Nombre total de billes perdues (Void).
    private int totalPertes;

    // Score courant (points).
    private int currentScore;

    // Nombre de billes réellement spawnees (runtime).
    private int realSpawned;

    // Instant (en secondes depuis le debut du niveau) auquel
    // l'objectif principal a ete atteint. -1 si jamais atteint.
    private int mainGoalReachedTimeSec = -1;
    public int MainGoalReachedTimeSec => mainGoalReachedTimeSec;
    // Indique si l'objectif principal a ete atteint au moins une fois.
    public bool MainGoalAchieved => goalReached;


    public int TotalBilles => totalBilles;
    public int TotalNonBlackBilles => totalBillesNonNoires;
    public int TotalPertes => totalPertes;
    public int CurrentScore => currentScore;
    public int GetRealSpawned() => realSpawned;

    // Détails agrégés par type de bille (collectées et perdues).
    // Clés = noms de type de bille (ex: "White", "Black", "Blue", ...).
    private readonly Dictionary<string, int> totauxParType = new Dictionary<string, int>();
    private readonly Dictionary<string, int> pertesParType = new Dictionary<string, int>();

    // Historique des flushs (snapshots de bacs).
    private readonly List<BinSnapshot> historique = new List<BinSnapshot>();

    // Historique des ids de combos declenches pendant le niveau.
    private readonly HashSet<string> combosTriggered = new HashSet<string>();

    // =====================================================
    //  EVENTS PUBLICS
    // =====================================================

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    // Notifie toute évolution du score courant.
    [HideInInspector]
    public IntEvent onScoreChanged = new IntEvent();

    // Notifie une perte de bille avec son type (pour feedbacks, HUD, etc.).
    public event Action<string> OnBallLost;

    // Notifie l'enregistrement d'un flush complet (BinSnapshot) après mise à jour du score.
    // Permet à d'autres systèmes (objectifs secondaires, replays, etc.) de réagir.
    public event Action<BinSnapshot> OnFlushSnapshotRegistered;

    // Accès lecture seule aux agrégats.
    public IReadOnlyDictionary<string, int> GetTotalsByTypeSnapshot()
        => new Dictionary<string, int>(totauxParType);

    public IReadOnlyDictionary<string, int> GetLossesByTypeSnapshot()
        => new Dictionary<string, int>(pertesParType);

    public List<BinSnapshot> GetHistoriqueSnapshot()
        => new List<BinSnapshot>(historique);

    // =====================================================
    //  INIT / RESET
    // =====================================================

    /// <summary>
    /// Définit le nombre total de billes prévues sur le niveau
    /// (hors noires, côté design).
    /// </summary>
    public void SetPlannedBalls(int count)
    {
        totalBillesPrevues = Mathf.Max(0, count);
    }

    /// <summary>
    /// Définit le seuil d'objectif principal (ThresholdCount) en nombre
    /// de billes NON NOIRES à collecter et réinitialise le flag de goal.
    /// Appelée une fois au début du niveau, après lecture du JSON / plan.
    /// </summary>
    public void SetObjectiveThreshold(int threshold)
    {
        objectiveThreshold = Mathf.Max(0, threshold);
        goalReached = false;
    }

    /// <summary>
    /// Réinitialise complètement le score et les compteurs runtime
    /// pour une nouvelle partie/niveau.
    /// </summary>
    public void ResetScore(int start = 0)
    {
        currentScore = start;
        totalBilles = 0;
        totalBillesNonNoires = 0;
        totalPertes = 0;
        realSpawned = 0;
        goalReached = false;
        mainGoalReachedTimeSec = -1;

        totauxParType.Clear();
        pertesParType.Clear();
        historique.Clear();
        combosTriggered.Clear();

        onScoreChanged?.Invoke(currentScore);
    }

    // =====================================================
    //  SCORE & EVENTS (COLLECTE, POINTS, OBJECTIF)
    // =====================================================

    /// <summary>
    /// Enregistre qu'une bille a été réellement spawnee.
    /// Permet de comparer le plan théorique au runtime réel.
    /// </summary>
    public void RegisterRealSpawn()
    {
        realSpawned++;
    }

    /// <summary>
    /// Ajoute des points au score courant.
    /// Note: ne modifie pas le nombre de billes, seulement les points.
    /// </summary>
    public void AddPoints(int amount, string _ = null)
    {
        currentScore += amount;
        onScoreChanged?.Invoke(currentScore);
    }

    /// <summary>
    /// Enregistre un snapshot de bac (flush) :
    /// - Incrémente le total de billes collectées (tous types)
    /// - Incrémente le total de billes collectées HORS NOIRES
    /// - Met à jour les totaux par type
    /// - Ajoute les points du lot au score
    /// - Vérifie si l'objectif principal est atteint
    /// </summary>
    public void GetSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0)
            return;

        historique.Add(snapshot);

        totalBilles += snapshot.nombreDeBilles;

        int nonBlackThisFlush = 0;

        if (snapshot.parType != null)
        {
            foreach (var kv in snapshot.parType)
            {
                string typeKey = kv.Key;
                int count = kv.Value;

                if (!totauxParType.ContainsKey(typeKey))
                    totauxParType[typeKey] = 0;

                totauxParType[typeKey] += count;

                if (!IsBlackType(typeKey))
                    nonBlackThisFlush += count;
            }
        }

        totalBillesNonNoires += nonBlackThisFlush;

        AddPoints(snapshot.totalPointsDuLot);
        CheckGoalReached();
        OnFlushSnapshotRegistered?.Invoke(snapshot);
    }


    /// <summary>
    /// Détermine si une clé de type correspond à une bille noire.
    /// On utilise une comparaison insensible à la casse.
    /// </summary>
    private bool IsBlackType(string typeKey)
    {
        if (string.IsNullOrEmpty(typeKey))
            return false;

        return string.Equals(typeKey, "Black", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vérifie si le seuil d'objectif est atteint et, si oui,
    /// déclenche l'évènement onGoalReached une seule fois.
    /// L'objectif est basé sur les billes NON NOIRES collectées.
    /// </summary>
    private void CheckGoalReached()
    {
        if (goalReached)
            return;

        if (objectiveThreshold <= 0)
            return;

        // On compare le nombre de billes NON NOIRES collectées au seuil.
        if (totalBillesNonNoires >= objectiveThreshold)
        {
            goalReached = true;

            Debug.Log("Goal reached (non-black threshold) !");

            onGoalReached?.Invoke();
        }
    }

    // Enregistre un id de combo declenche pendant le niveau.
    public void RegisterComboId(string comboId)
    {
        if (string.IsNullOrEmpty(comboId))
            return;

        combosTriggered.Add(comboId);
    }

    // Retourne un snapshot des ids de combos declenches.
    public IReadOnlyCollection<string> GetCombosTriggeredSnapshot()
    {
        return new List<string>(combosTriggered);
    }


    // =====================================================
    //  PERTE DE BILLE (VOID TRIGGER)
    // =====================================================

    /// <summary>
    /// Enregistre la perte d'une bille (passage par le Void).
    /// Met à jour les pertes par type et le total global.
    /// </summary>
    public void RegisterLost(string ballType)
    {
        if (string.IsNullOrEmpty(ballType))
            ballType = "Unknown";

        if (!pertesParType.ContainsKey(ballType))
            pertesParType[ballType] = 0;

        pertesParType[ballType]++;

        totalPertes++;

        OnBallLost?.Invoke(ballType);
    }

    // =====================================================
    //  END LEVEL STATS
    // =====================================================

    /// <summary>
    /// Construit un objet EndLevelStats pour la fin de niveau.
    /// Note: FinalScore est pour l'instant égal au RawScore,
    /// la logique de multiplicateur / cérémonie finale peut le modifier plus tard.
    /// </summary>
    public EndLevelStats BuildEndLevelStats(int timeElapsedSec)
    {
        return new EndLevelStats
        {
            TimeElapsedSec = Mathf.Max(0, timeElapsedSec),
            BallsCollected = totalBilles,
            BallsLost = totalPertes,
            RawScore = currentScore,
            FinalScore = currentScore
        };
    }


    /// <summary>
    /// Permet d'enregistrer le temps (en secondes) auquel
    /// l'objectif principal a ete atteint sur le niveau.
    /// La valeur n'est definie qu'une seule fois (premiere atteinte).
    /// </summary>
    public void SetMainGoalReachedTime(int elapsedTimeSec)
    {
        if (elapsedTimeSec < 0)
            elapsedTimeSec = 0;

        if (mainGoalReachedTimeSec < 0)
            mainGoalReachedTimeSec = elapsedTimeSec;
    }




    // Debug only: permet au testeur de forcer le nombre de billes non noires prevues.
    public void Debug_SetPlannedNonBlack(int count)
    {
        totalBillesPrevues = count;
    }

}
