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
    [SerializeField] private int totalBillesPrevues;  // Nombre de billes prevues sur le niveau (plan theorique)
    public int TotalBillesPrevues => totalBillesPrevues;

    // =====================================================
    //  MAIN OBJECTIVE (SEUIL PRINCIPAL DU NIVEAU)
    // =====================================================

    [Header("Main Objective")]
    [SerializeField] private int objectiveThreshold;  // Nombre de billes a collecter pour atteindre l'objectif principal (ThresholdCount)
    public int ObjectiveThreshold => objectiveThreshold;

    // Flag interne pour eviter de declencher plusieurs fois l'evenement
    private bool goalReached = false;

    [Serializable]
    public class SimpleEvent : UnityEvent { }

    // Evenement declenche une seule fois lorsque le seuil est atteint ou depasse
    public SimpleEvent onGoalReached = new SimpleEvent();

    // =====================================================
    //  RUNTIME STATE (ETAT EN COURS DE PARTIE)
    // =====================================================

    private int totalBilles;     // Nombre total de billes collectees (toutes types confondus)
    private int totalPertes;     // Nombre total de billes perdues (Void)
    private int currentScore;    // Score courant (points)
    private int realSpawned;     // Nombre de billes reellement spawnees

    public int TotalBilles => totalBilles;
    public int TotalPertes => totalPertes;
    public int CurrentScore => currentScore;
    public int GetRealSpawned() => realSpawned;

    // Details agreges par type de bille (collectees et perdues)
    private readonly Dictionary<string, int> totauxParType = new Dictionary<string, int>();
    private readonly Dictionary<string, int> pertesParType = new Dictionary<string, int>();

    // Historique des flushs (snapshots de bacs)
    private readonly List<BinSnapshot> historique = new List<BinSnapshot>();

    // =====================================================
    //  EVENTS PUBLICS
    // =====================================================

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    // Notifie toute evolution du score courant
    [HideInInspector] public IntEvent onScoreChanged = new IntEvent();

    // Notifie une perte de bille avec son type (pour feedbacks, HUD, etc.)
    public event Action<string> OnBallLost;

    // Notifie l'enregistrement d'un flush complet (BinSnapshot) après mise à jour du score.
    // Permet à d'autres systèmes (objectifs secondaires, replays, etc.) de réagir.
    public event Action<BinSnapshot> OnFlushSnapshotRegistered;

    // Acces lecture seule aux aggregats
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
    /// Définit le nombre total de billes prévues sur le niveau.
    /// </summary>
    public void SetPlannedBalls(int count)
    {
        totalBillesPrevues = Mathf.Max(0, count);
    }


    /// <summary>
    /// Definit le seuil d'objectif principal (ThresholdCount) et reinitialise le flag de goal.
    /// Appelle une fois au debut du niveau, apres lecture du JSON.
    /// </summary>
    public void SetObjectiveThreshold(int threshold)
    {
        objectiveThreshold = Mathf.Max(0, threshold);
        goalReached = false;
    }

    /// <summary>
    /// Reinitialise completement le score et les compteurs runtime pour une nouvelle partie/niveau.
    /// </summary>
    public void ResetScore(int start = 0)
    {
        currentScore = start;
        totalBilles = 0;
        totalPertes = 0;
        realSpawned = 0;
        goalReached = false;

        totauxParType.Clear();
        pertesParType.Clear();
        historique.Clear();

        onScoreChanged?.Invoke(currentScore);
    }

    // =====================================================
    //  SCORE & EVENTS (COLLECTE, POINTS, OBJECTIF)
    // =====================================================

    /// <summary>
    /// Enregistre qu'une bille a ete reellement spawnee.
    /// Permet de comparer le plan theorique au runtime reel.
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
    /// - Incremente le total de billes collectees
    /// - Met a jour les totaux par type
    /// - Ajoute les points du lot au score
    /// - Verifie si l'objectif principal est atteint
    /// </summary>
    public void GetSnapshot(BinSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nombreDeBilles <= 0)
            return;

        // Sauvegarde de l'historique
        historique.Add(snapshot);

        // Incremente le total de billes collectees
        totalBilles += snapshot.nombreDeBilles;

        // Mise a jour des totaux par type de bille
        if (snapshot.parType != null)
        {
            foreach (var kv in snapshot.parType)
            {
                if (!totauxParType.ContainsKey(kv.Key))
                    totauxParType[kv.Key] = 0;

                totauxParType[kv.Key] += kv.Value;
            }
        }

        // Ajout des points du flush au score courant
        AddPoints(snapshot.totalPointsDuLot);

        // Verifie si l'objectif principal est atteint ou depasse
        CheckGoalReached();

        // Notifie l'enregistrement de ce flush a tous les listeners interessés
        OnFlushSnapshotRegistered?.Invoke(snapshot);
    }


    /// <summary>
    /// Verifie si le seuil d'objectif est atteint et, si oui,
    /// declenche l'evenement onGoalReached une seule fois.
    /// </summary>
    private void CheckGoalReached()
    {  
        if (goalReached)
            return;

        if (objectiveThreshold <= 0)
            return;
       
        if (totalBilles >= objectiveThreshold)
        {
            goalReached = true;

            // Pour debug simple pour le moment
            Debug.Log("Goal reached !");

            onGoalReached?.Invoke();
        }
    }

    // =====================================================
    //  PERTE DE BILLE (VOID TRIGGER)
    // =====================================================

    /// <summary>
    /// Enregistre la perte d'une bille (passage par le Void).
    /// Met a jour les pertes par type et le total global.
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
    /// Note: FinalScore est pour l'instant egal au RawScore,
    /// la logique de multiplicateur/cere monie finale peut le modifier plus tard.
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
}
