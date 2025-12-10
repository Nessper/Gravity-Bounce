using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur runtime des objectifs secondaires pour un niveau.
/// - Configure SecondaryObjectivesManager a partir du LevelData.
/// - Ecoute les flush (BinSnapshot) pour suivre les billes collecteess.
/// - Recoit les evenements de combos.
/// - Stocke les resultats secondaires calcules en fin de niveau.
/// 
/// Ce composant isole toute la logique d objectifs secondaires hors du LevelManager.
/// </summary>
public class LevelSecondaryObjectivesController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ScoreManager scoreManager;

    // Manager logique des objectifs secondaires.
    private SecondaryObjectivesManager secondaryManager = new SecondaryObjectivesManager();

    // Indique si le niveau courant possede des objectifs secondaires configures.
    private bool hasSecondaryObjectives;

    // Resultats calculees lors de l evaluation de fin de niveau.
    private List<SecondaryObjectiveResult> lastResults;

    /// <summary>
    /// Acces au manager logique utilise par LevelResultEvaluator.
    /// Peut etre null si aucun objectif secondaire n est configure.
    /// </summary>
    public SecondaryObjectivesManager Manager => hasSecondaryObjectives ? secondaryManager : null;

    /// <summary>
    /// Retourne les derniers resultats secondaires enregistres
    /// (utilises par EndLevelUI via LevelManager).
    /// </summary>
    public List<SecondaryObjectiveResult> GetLastResults()
    {
        return lastResults;
    }

    /// <summary>
    /// Enregistre les resultats secondaires calcules en fin de niveau.
    /// Appelle par le LevelManager apres LevelResultEvaluator.
    /// </summary>
    public void SetResults(List<SecondaryObjectiveResult> results)
    {
        lastResults = results;
    }

    // ============================================================
    // CYCLE UNITY
    // ============================================================

    private void OnEnable()
    {
        if (scoreManager != null)
        {
            scoreManager.OnFlushSnapshotRegistered += HandleFlushSnapshotRegistered;
        }
    }

    private void OnDisable()
    {
        if (scoreManager != null)
        {
            scoreManager.OnFlushSnapshotRegistered -= HandleFlushSnapshotRegistered;
        }
    }

    // ============================================================
    // SETUP
    // ============================================================

    /// <summary>
    /// Configure les objectifs secondaires a partir du LevelData.
    /// Appelle par le LevelManager pendant le setup du niveau.
    /// </summary>
    public void SetupFromLevel(LevelData data)
    {
        lastResults = null;
        hasSecondaryObjectives = false;

        if (data == null || data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
            return;

        hasSecondaryObjectives = true;
        secondaryManager = new SecondaryObjectivesManager();
        secondaryManager.Setup(data.SecondaryObjectives);
    }

    // ============================================================
    // EVENTS RUNTIME
    // ============================================================

    /// <summary>
    /// Appelle par le ScoreManager a chaque flush de bin.
    /// Traduit les snapshots en evenements pour SecondaryObjectivesManager.
    /// </summary>
    private void HandleFlushSnapshotRegistered(BinSnapshot snapshot)
    {
        if (!hasSecondaryObjectives)
            return;

        if (snapshot == null || snapshot.parType == null)
            return;

        foreach (var kv in snapshot.parType)
        {
            string ballType = kv.Key;
            int count = kv.Value;

            for (int i = 0; i < count; i++)
            {
                secondaryManager.OnBallCollected(ballType);
            }
        }
    }

    /// <summary>
    /// Appelle par le LevelManager (ou autre) quand un combo est declenche.
    /// </summary>
    public void NotifyComboTriggered(string comboId)
    {
        if (!hasSecondaryObjectives)
            return;

        if (string.IsNullOrEmpty(comboId))
            return;

        secondaryManager.OnComboTriggered(comboId);
    }
}
