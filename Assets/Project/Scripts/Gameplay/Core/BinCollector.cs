using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gere la collecte (flush) des bacs gauche/droite :
/// - detection des conditions de flush (seuil, force, delai),
/// - construction d un BinSnapshot pour le ScoreManager / ComboEngine,
/// - penalite de coque (Hull) en fonction des billes noires du lot,
/// - declenchement des FX de flush,
/// - declenchement des SFX de flush (normal / black),
/// - recyclage des billes via le BallSpawner.
/// </summary>
public class BinCollector : MonoBehaviour
{
    [SerializeField] private RunSessionState runSession;

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ComboEngine comboEngine;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;
    [SerializeField] private BallSpawner spawner;

    [Header("Hull / Coque")]
    [Tooltip("Systeme de gestion de la coque du vaisseau (Hull).")]
    [SerializeField] private HullSystem hullSystem;

    [Header("FX de flush")]
    [Tooltip("Effet visuel de flush pour le bin gauche.")]
    [SerializeField] private BinFlushFX leftFlushFx;

    [Tooltip("Effet visuel de flush pour le bin droit.")]
    [SerializeField] private BinFlushFX rightFlushFx;

    [Header("Audio (Flush)")]
    [Tooltip("Petit lead time pour lancer le SFX juste avant le pic visuel du FX.")]
    [SerializeField] private float flushSfxLeadTime = 0.03f;

    [Tooltip("Anti doublon: delai minimal entre deux sons de flush (utile quand left+right flush en meme temps).")]
    [SerializeField] private float flushSfxCooldownSec = 0.05f;

    [Header("Options de flush")]
    [Tooltip("Delai avant le flush en run normal (hors fin de niveau).")]
    [SerializeField] private float delayBeforeFlush = 1.2f;

    // Etat de flush par cote (evite les debuts en double)
    private bool flushingLeft;
    private bool flushingRight;

    // Anti doublon SFX (temps unscaled pour etre robuste aux timescales)
    private float lastFlushSfxTimeUnscaled = -999f;

    /// <summary>
    /// Indique si un flush (gauche ou droite) est actuellement en cours.
    /// </summary>
    public bool IsAnyFlushActive => flushingLeft || flushingRight;

    public bool IsLeftFlushing() => flushingLeft;
    public bool IsRightFlushing() => flushingRight;

    /// <summary>
    /// Active/desactive l auto-flush sur les deux bacs (utilise par la fin de niveau).
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
    /// Demande un flush sur un cote specifique.
    /// </summary>
    public void CollectFromBin(Side side, bool force = false, bool skipDelay = false, bool isFinalFlush = false)
    {
        if (side == Side.Left)
            CollectLeft(force, skipDelay, isFinalFlush);

        if (side == Side.Right)
            CollectRight(force, skipDelay, isFinalFlush);
    }

    /// <summary>
    /// Demande un flush simultane des deux bacs (gauche et droit).
    /// </summary>
    public void CollectAll(bool force = false, bool skipDelay = false, bool isFinalFlush = false)
    {
        CollectLeft(force, skipDelay, isFinalFlush);
        CollectRight(force, skipDelay, isFinalFlush);
    }

    // ------------------------------------------------------------------------
    // Pipelines internes
    // ------------------------------------------------------------------------

    private void CollectLeft(bool force, bool skipDelay, bool isFinalFlush)
    {
        if (leftBin == null || flushingLeft)
            return;

        flushingLeft = true;
        StartCoroutine(CollectWithOptions(Side.Left, force, skipDelay, isFinalFlush));
    }

    private void CollectRight(bool force, bool skipDelay, bool isFinalFlush)
    {
        if (rightBin == null || flushingRight)
            return;

        flushingRight = true;
        StartCoroutine(CollectWithOptions(Side.Right, force, skipDelay, isFinalFlush));
    }

    /// <summary>
    /// Pipeline complet de flush pour un cote donne :
    /// - eventuel delai,
    /// - verification du seuil (sauf force),
    /// - pre-calcul FX + SFX avant disparition des billes,
    /// - snapshot logique (score, combos, hull) + purge du bin,
    /// - recyclage des billes.
    /// </summary>
    private IEnumerator CollectWithOptions(Side side, bool force, bool skipDelay, bool isFinalFlush)
    {
        try
        {
            // Delai avant flush en run normal
            if (!skipDelay)
                yield return new WaitForSecondsRealtime(delayBeforeFlush);

            BinTrigger trigger = GetTrigger(side);
            if (trigger == null)
                yield break;

            // En mode normal, on valide le seuil (avec Greed); en force (fin de niveau), on prend tout.
            if (!force)
            {
                int effectiveThreshold = GetEffectiveFlushThresholdFor(trigger);
                if (trigger.Count < effectiveThreshold)
                    yield break;
            }

            // ----------------------------------------------------------------
            // 1) Pre-calcul pour le feedback AVANT disparition des billes
            // ----------------------------------------------------------------
            int previewScore = trigger.PeekTotalPoints();
            bool hasBlack = trigger.ContainsBlack();

            TriggerFlushSfx(hasBlack);

            if (flushSfxLeadTime > 0f)
                yield return new WaitForSecondsRealtime(flushSfxLeadTime);

            TriggerFlushFx(side, previewScore, hasBlack);

            // ----------------------------------------------------------------
            // 2) Snapshot logique (score, combos, hull) + purge du bin
            // ----------------------------------------------------------------
            List<BallState> lot = trigger.TakeSnapshotAndClear();
            if (lot == null || lot.Count == 0)
                yield break;

            int blackCount;
            BinSnapshot snapshot = BuildSnapshot(lot, side, out blackCount);

            // NEW: phase courante (1-based) pour debug / objectifs secondaires par phase
            if (spawner != null)
                snapshot.phaseIndex1Based = spawner.CurrentPhaseIndex + 1;

            snapshot.isFinalFlush = isFinalFlush;

            // Penalite de coque : 1 point par bille noire dans ce flush
            if (hullSystem != null && blackCount > 0)
                hullSystem.ApplyBlackPenalty(blackCount);

            if (scoreManager != null)
                scoreManager.GetSnapshot(snapshot);

            if (comboEngine != null)
                comboEngine.OnFlush(snapshot);

            // ----------------------------------------------------------------
            // 3) Recyclage
            // ----------------------------------------------------------------
            if (spawner == null)
            {
                Debug.LogError("[BinCollector] Spawner non assigne : impossible de recycler. (Fallback: Destroy)");

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

                st.collected = true;
                // On ne détache jamais à la racine. Le spawner/pool gère la vie de l’objet.
                spawner.Recycle(st.gameObject, st.type, collected: true);
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

            if (st.type == BallType.Black)
                blackCount++;
        }

        snapshot.totalPointsDuLot = totalPoints;
        return snapshot;
    }

    private void TriggerFlushFx(Side side, int flushScore, bool hasBlack)
    {
        BinFlushFX fx = (side == Side.Left) ? leftFlushFx : rightFlushFx;
        if (fx != null)
            fx.PlayFlush(hasBlack, flushScore);
    }

    /// <summary>
    /// SFX flush simplifie :
    /// - FlushBlack si presence de bille noire,
    /// - sinon FlushNormal.
    /// </summary>
    private void TriggerFlushSfx(bool hasBlack)
    {
        if (BootRoot.Audio == null)
            return;

        float now = Time.unscaledTime;
        if (flushSfxCooldownSec > 0f && (now - lastFlushSfxTimeUnscaled) < flushSfxCooldownSec)
            return;

        lastFlushSfxTimeUnscaled = now;

        if (hasBlack)
            BootRoot.Audio.PlaySfx(SfxId.FlushBlack);
        else
            BootRoot.Audio.PlaySfx(SfxId.FlushNormal);
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

    public int GetEffectiveFlushThresholdFor(BinTrigger trigger)
    {
        if (trigger == null)
            return 1;

        int baseThreshold = Mathf.Max(1, trigger.flushThreshold);

        // Bonus Greed dérivé des modules équipés
        int bonus = 0;
        if (runSession != null)
            bonus = Mathf.Max(0, runSession.GetFlushMinBallsBonusFromModules());

        return Mathf.Max(1, baseThreshold + bonus);
    }


}
