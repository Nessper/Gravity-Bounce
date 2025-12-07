using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la collecte (flush) des bacs gauche/droite :
/// - détection des conditions de flush (seuil, force, délai),
/// - construction d'un BinSnapshot pour le ScoreManager / ComboEngine,
/// - pénalité de coque (Hull) en fonction des billes noires du lot,
/// - déclenchement des FX de flush,
/// - recyclage des billes via le BallSpawner.
/// </summary>
public class BinCollector : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ComboEngine comboEngine;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;
    [SerializeField] private BallSpawner spawner;   // requis pour le recycle

    [Header("Hull / Coque")]
    [Tooltip("Système de gestion de la coque du vaisseau (Hull).")]
    [SerializeField] private HullSystem hullSystem;

    [Header("FX de flush")]
    [Tooltip("Effet visuel de flush pour le bin gauche.")]
    [SerializeField] private BinFlushFX leftFlushFx;

    [Tooltip("Effet visuel de flush pour le bin droit.")]
    [SerializeField] private BinFlushFX rightFlushFx;

    [Header("Options de flush")]
    [Tooltip("Délai avant le flush en run normal (hors fin de niveau).")]
    [SerializeField] private float delayBeforeFlush = 1.2f;

    // Etat de flush par côté (évite les débuts en double)
    private bool flushingLeft;
    private bool flushingRight;

    /// <summary>
    /// Indique si un flush (gauche ou droite) est actuellement en cours.
    /// </summary>
    public bool IsAnyFlushActive => flushingLeft || flushingRight;

    public bool IsLeftFlushing() => flushingLeft;
    public bool IsRightFlushing() => flushingRight;

    /// <summary>
    /// Active/désactive l'auto-flush sur les deux bacs (utilisé par la fin de niveau).
    /// </summary>
    public void SetAutoFlushEnabled(bool enabled)
    {
        if (leftBin != null) leftBin.SetAutoFlushEnabled(enabled);
        if (rightBin != null) rightBin.SetAutoFlushEnabled(enabled);
    }

    // ------------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------------

    /// <summary>
    /// Demande un flush sur un côté spécifique.
    /// </summary>
    /// <param name="side">Côté gauche ou droit.</param>
    /// <param name="force">
    /// Si true, ignore le seuil du bin et flush tout (utilisé en fin de niveau).
    /// </param>
    /// <param name="skipDelay">
    /// Si true, ignore le délai avant flush (utilisé pour les flush forcés).
    /// </param>
    public void CollectFromBin(Side side, bool force = false, bool skipDelay = false)
    {
        if (side == Side.Left)
            CollectLeft(force, skipDelay);

        if (side == Side.Right)
            CollectRight(force, skipDelay);
    }

    /// <summary>
    /// Demande un flush simultané des deux bacs (gauche et droit).
    /// </summary>
    public void CollectAll(bool force = false, bool skipDelay = false)
    {
        CollectLeft(force, skipDelay);
        CollectRight(force, skipDelay);
    }

    // ------------------------------------------------------------------------
    // Pipelines internes
    // ------------------------------------------------------------------------

    private void CollectLeft(bool force, bool skipDelay)
    {
        if (leftBin == null || flushingLeft)
            return;

        flushingLeft = true;
        StartCoroutine(CollectWithOptions(Side.Left, force, skipDelay));
    }

    private void CollectRight(bool force, bool skipDelay)
    {
        if (rightBin == null || flushingRight)
            return;

        flushingRight = true;
        StartCoroutine(CollectWithOptions(Side.Right, force, skipDelay));
    }

    /// <summary>
    /// Pipeline complet de flush pour un côté donné :
    /// - éventuel délai,
    /// - vérification du seuil (sauf force),
    /// - pré-calcul FX,
    /// - snapshot logique (score, combos, hull),
    /// - recyclage des billes.
    /// </summary>
    private IEnumerator CollectWithOptions(Side side, bool force, bool skipDelay)
    {
        try
        {
            // Délai avant flush en run normal
            if (!skipDelay)
                yield return new WaitForSecondsRealtime(delayBeforeFlush);

            var trigger = GetTrigger(side);
            if (trigger == null)
                yield break;

            // En mode normal, on valide le seuil; en force (fin de niveau), on prend tout.
            if (!force && trigger.Count < trigger.flushThreshold)
                yield break;

            // ----------------------------------------------------------------
            // 1) Pré-calcul pour le FX AVANT disparition des billes
            // ----------------------------------------------------------------
            int previewScore = trigger.PeekTotalPoints();
            bool hasBlack = trigger.ContainsBlack();
            TriggerFlushFx(side, previewScore, hasBlack);

            // ----------------------------------------------------------------
            // 2) Snapshot logique (score, combos, hull) + purge du bin
            // ----------------------------------------------------------------
            List<BallState> lot = trigger.TakeSnapshotAndClear();
            if (lot == null || lot.Count == 0)
                yield break;

            // Construction du snapshot et comptage des billes noires
            int blackCount;
            BinSnapshot snapshot = BuildSnapshot(lot, side, out blackCount);

            // Pénalité de coque: 1 point par bille noire dans ce flush
            if (hullSystem != null && blackCount > 0)
            {
                hullSystem.ApplyBlackPenalty(blackCount);
            }

            // Enregistrement score + combos
            if (scoreManager != null)
                scoreManager.GetSnapshot(snapshot);

            if (comboEngine != null)
                comboEngine.OnFlush(snapshot);

            // ----------------------------------------------------------------
            // 3) Recyclage: on désactive toutes les billes du lot
            // ----------------------------------------------------------------
            if (spawner == null)
            {
                Debug.LogError("[BinCollector] Spawner non assigné : impossible de recycler. (Fallback: Destroy)");

                for (int i = 0; i < lot.Count; i++)
                {
                    BallState st = lot[i];
                    if (st == null)
                        continue;

                    st.collected = true;
                    Object.Destroy(st.gameObject);
                }

                yield break;
            }

            for (int i = 0; i < lot.Count; i++)
            {
                BallState st = lot[i];
                if (st == null)
                    continue;

                st.collected = true;                 // anti double-compte ailleurs
                st.transform.SetParent(null, true);  // par sécurité
                spawner.Recycle(st.gameObject, collected: true);
            }
        }
        finally
        {
            SetFlushing(side, false);
        }
    }

    // ------------------------------------------------------------------------
    // Utilitaires internes
    // ------------------------------------------------------------------------

    /// <summary>
    /// Construit le BinSnapshot à partir du lot de billes collectées et
    /// retourne également le nombre de billes noires trouvées dans ce lot.
    /// </summary>
    private BinSnapshot BuildSnapshot(List<BallState> lot, Side side, out int blackCount)
    {
        var snapshot = new BinSnapshot
        {
            binSide = (side == Side.Right) ? BinSide.Right : BinSide.Left,
            timestamp = Time.time,
            parType = new Dictionary<string, int>(),
            pointsParType = new Dictionary<string, int>(),
            nombreDeBilles = 0,
            totalPointsDuLot = 0
        };

        blackCount = 0;
        int totalPoints = 0;

        for (int i = 0; i < lot.Count; i++)
        {
            BallState st = lot[i];
            if (st == null)
                continue;

            totalPoints += st.points;
            snapshot.nombreDeBilles++;

            // Comptage par type (pour combos, stats, etc.)
            string typeName = st.type.ToString();

            int count;
            if (!snapshot.parType.TryGetValue(typeName, out count))
                snapshot.parType[typeName] = 1;
            else
                snapshot.parType[typeName] = count + 1;

            int pts;
            if (!snapshot.pointsParType.TryGetValue(typeName, out pts))
                snapshot.pointsParType[typeName] = st.points;
            else
                snapshot.pointsParType[typeName] = pts + st.points;

            // Comptage des billes noires pour la coque
            if (st.type == BallType.Black)
                blackCount++;
        }

        snapshot.totalPointsDuLot = totalPoints;
        return snapshot;
    }

    /// <summary>
    /// Déclenche le FX de flush pour le côté concerné, en fonction des infos
    /// pré-calculées dans le bin (score du flush et présence d'une bille noire).
    /// </summary>
    private void TriggerFlushFx(Side side, int flushScore, bool hasBlack)
    {
        BinFlushFX fx = null;

        if (side == Side.Left)
            fx = leftFlushFx;
        else if (side == Side.Right)
            fx = rightFlushFx;

        if (fx != null)
            fx.PlayFlush(hasBlack, flushScore);
    }

    private BinTrigger GetTrigger(Side side)
    {
        if (side == Side.Left)
            return leftBin;

        if (side == Side.Right)
            return rightBin;

        return null;
    }

    private void SetFlushing(Side side, bool value)
    {
        if (side == Side.Left)
            flushingLeft = value;

        if (side == Side.Right)
            flushingRight = value;
    }
}
