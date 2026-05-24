using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la collecte (flush) des bacs gauche/droite :
/// - détection des conditions de flush (seuil, force, délai),
/// - construction d'un BinSnapshot pour le ScoreManager / ComboEngine,
/// - pénalité de coque (Hull) en fonction des billes noires du lot,
/// - déclenchement des FX de flush,
/// - déclenchement des SFX de flush (normal / black),
/// - recyclage des billes via le BallSpawner.
///
/// Important :
/// - Le bonus de seuil de flush (modules GREED) vient désormais de ModuleRuntimeStats.
/// - En mode tutorial flush :
///   * Score / Combo / Hull / FX / SFX restent actifs comme en vrai gameplay,
///   * l'etat runtime est simplement reset a la fin du tuto.
/// </summary>
public class BinCollector : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FlushResolutionEngine flushResolutionEngine;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;
    [SerializeField] private BallSpawner spawner;

    [Header("Hull / Coque")]
    [Tooltip("Système de gestion de la coque du vaisseau (Hull).")]
    [SerializeField] private HullSystem hullSystem;

    [Header("FX de flush")]
    [Tooltip("Effet visuel de flush pour le bin gauche.")]
    [SerializeField] private BinFlushFX leftFlushFx;

    [Tooltip("Effet visuel de flush pour le bin droit.")]
    [SerializeField] private BinFlushFX rightFlushFx;

    [Header("Audio (Flush)")]
    [Tooltip("Petit lead time pour lancer le SFX juste avant le pic visuel du FX.")]
    [SerializeField] private float flushSfxLeadTime = 0.03f;

    [Tooltip("Anti-doublon : délai minimal entre deux sons de flush (utile quand left+right flush en même temps).")]
    [SerializeField] private float flushSfxCooldownSec = 0.05f;

    [Header("Options de flush")]
    [Tooltip("Délai avant le flush en run normal (hors fin de niveau).")]
    [SerializeField] private float delayBeforeFlush = 1.2f;

    public event Action<Side, BinSnapshot, int> OnBinFlushed;

    private bool flushingLeft;
    private bool flushingRight;

    private float lastFlushSfxTimeUnscaled = -999f;

    /// <summary>
    /// Indique si un flush (gauche ou droite) est actuellement en cours.
    /// </summary>
    public bool IsAnyFlushActive => flushingLeft || flushingRight;

    public bool IsLeftFlushing() => flushingLeft;
    public bool IsRightFlushing() => flushingRight;

    /// <summary>
    /// Active / désactive l'auto-flush sur les deux bacs
    /// (utilisé par la fin de niveau ou le tuto).
    /// </summary>
    public void SetAutoFlushEnabled(bool enabled)
    {
        if (leftBin != null)
            leftBin.SetAutoFlushEnabled(enabled);

        if (rightBin != null)
            rightBin.SetAutoFlushEnabled(enabled);
    }

    // ------------------------------------------------------------
    // API publique
    // ------------------------------------------------------------

    /// <summary>
    /// Demande un flush sur un côté spécifique.
    /// </summary>
    public void CollectFromBin(
        Side side,
        bool force = false,
        bool skipDelay = false,
        bool isFinalFlush = false,
        bool isTutorialFlush = false)
    {
        if (side == Side.Left)
            CollectLeft(force, skipDelay, isFinalFlush, isTutorialFlush);

        if (side == Side.Right)
            CollectRight(force, skipDelay, isFinalFlush, isTutorialFlush);
    }

    /// <summary>
    /// Demande un flush simultané des deux bacs (gauche et droit).
    /// </summary>
    public void CollectAll(
        bool force = false,
        bool skipDelay = false,
        bool isFinalFlush = false,
        bool isTutorialFlush = false)
    {
        CollectLeft(force, skipDelay, isFinalFlush, isTutorialFlush);
        CollectRight(force, skipDelay, isFinalFlush, isTutorialFlush);
    }

    // ------------------------------------------------------------
    // Pipelines internes
    // ------------------------------------------------------------

    private void CollectLeft(bool force, bool skipDelay, bool isFinalFlush, bool isTutorialFlush)
    {
        if (leftBin == null || flushingLeft)
            return;

        flushingLeft = true;
        StartCoroutine(CollectWithOptions(Side.Left, force, skipDelay, isFinalFlush, isTutorialFlush));
    }

    private void CollectRight(bool force, bool skipDelay, bool isFinalFlush, bool isTutorialFlush)
    {
        if (rightBin == null || flushingRight)
            return;

        flushingRight = true;
        StartCoroutine(CollectWithOptions(Side.Right, force, skipDelay, isFinalFlush, isTutorialFlush));
    }

    /// <summary>
    /// Pipeline complet de flush pour un côté donné :
    /// - éventuel délai,
    /// - vérification du seuil (sauf force),
    /// - pré-calcul FX + SFX avant disparition des billes,
    /// - snapshot logique + purge du bin,
    /// - éventuel score/combo,
    /// - éventuelle pénalité hull,
    /// - recyclage des billes.
    /// </summary>
    private IEnumerator CollectWithOptions(
        Side side,
        bool force,
        bool skipDelay,
        bool isFinalFlush,
        bool isTutorialFlush)
    {
        try
        {
            if (!skipDelay)
                yield return new WaitForSecondsRealtime(delayBeforeFlush);

            BinTrigger trigger = GetTrigger(side);
            if (trigger == null)
                yield break;

            if (!force)
            {
                int effectiveThreshold = GetEffectiveFlushThresholdFor(trigger);
                if (trigger.Count < effectiveThreshold)
                    yield break;
            }

            // --------------------------------------------------------
            // 1) Pré-calcul pour le feedback AVANT disparition des billes
            // --------------------------------------------------------
            int previewScore = trigger.PeekTotalPoints();
            bool hasBlack = trigger.ContainsBlack();

            TriggerFlushSfx(hasBlack);

            if (flushSfxLeadTime > 0f)
                yield return new WaitForSecondsRealtime(flushSfxLeadTime);

            TriggerFlushFx(side, previewScore, hasBlack);

            // --------------------------------------------------------
            // 2) Snapshot logique + purge du bin
            // --------------------------------------------------------
            List<BallState> lot = trigger.TakeSnapshotAndClear();
            if (lot == null || lot.Count == 0)
                yield break;

            int blackCount;
            BinSnapshot snapshot = BuildSnapshot(lot, side, out blackCount);

            if (spawner != null)
                snapshot.phaseIndex1Based = spawner.CurrentPhaseIndex + 1;

            snapshot.isFinalFlush = isFinalFlush;

            OnBinFlushed?.Invoke(side, snapshot, blackCount);

            // Hull reste actif même en tuto
            if (hullSystem != null && blackCount > 0)
                hullSystem.ApplyBlackPenalty(blackCount);

            // Score / combos actifs aussi pendant le tuto.
            // L'etat sera reset proprement a la fin du tuto.
            if (scoreManager != null)
                scoreManager.GetSnapshot(snapshot);

            if (flushResolutionEngine != null)
                flushResolutionEngine.OnFlush(snapshot);

            // --------------------------------------------------------
            // 3) Recyclage
            // --------------------------------------------------------
            if (spawner == null)
            {
                Debug.LogError("[BinCollector] Spawner non assigné : impossible de recycler. (Fallback : Destroy)");

                for (int i = 0; i < lot.Count; i++)
                {
                    BallState st = lot[i];
                    if (st == null)
                        continue;

                    st.collected = true;
                    Destroy(st.gameObject);
                }

                yield break;
            }

            for (int i = 0; i < lot.Count; i++)
            {
                BallState st = lot[i];
                if (st == null)
                    continue;

                st.collected = true;

                if (isTutorialFlush || st.isTutorialBall)
                {
                    Destroy(st.gameObject);
                }
                else
                {
                    spawner.Recycle(st.gameObject, st.type, collected: true);
                }
            }
        }
        finally
        {
            SetFlushing(side, false);
        }
    }

    // ------------------------------------------------------------
    // Utilitaires internes
    // ------------------------------------------------------------

    /// <summary>
    /// Construit le snapshot logique du flush.
    ///
    /// IMPORTANT :
    /// - Le snapshot représente le résultat FINAL utilisé
    ///   pour le scoring et les combos.
    /// - Certains modules peuvent modifier le type logique
    ///   d'une bille au flush.
    /// - Le BallType réel des BallState n'est PAS modifié.
    /// - Les transformations sont purement logiques/runtime.
    ///
    /// Ordre des modules :
    /// 1) Famille A : Black Filter
    ///    - une noire réservée devient White.
    /// 2) Famille B : White Upgrade
    ///    - une White devient Red.
    ///    - puis une White devient Blue.
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

        int whiteToRedLeft = 0;
        int whiteToBlueLeft = 0;

        if (ModuleRuntimeStats.Instance != null)
        {
            whiteToRedLeft = Mathf.Max(0, ModuleRuntimeStats.Instance.FlushWhiteToRedCount);
            whiteToBlueLeft = Mathf.Max(0, ModuleRuntimeStats.Instance.FlushWhiteToBlueCount);
        }

        for (int i = 0; i < lot.Count; i++)
        {
            BallState st = lot[i];

            if (st == null)
                continue;

            BallType resolvedType = ResolveTypeForFlushFamilyA(st);

            if (resolvedType == BallType.White && whiteToRedLeft > 0)
            {
                resolvedType = BallType.Red;
                whiteToRedLeft--;
            }
            else if (resolvedType == BallType.White && whiteToBlueLeft > 0)
            {
                resolvedType = BallType.Blue;
                whiteToBlueLeft--;
            }

            if (resolvedType == BallType.Black)
                blackCount++;

            int resolvedPoints =
                GetPointsForResolvedType(st, resolvedType);

            totalPoints += resolvedPoints;
            snapshot.nombreDeBilles++;

            string typeName = resolvedType.ToString();

            int count;
            if (!snapshot.parType.TryGetValue(typeName, out count))
                snapshot.parType[typeName] = 1;
            else
                snapshot.parType[typeName] = count + 1;

            int pts;
            if (!snapshot.pointsParType.TryGetValue(typeName, out pts))
                snapshot.pointsParType[typeName] = resolvedPoints;
            else
                snapshot.pointsParType[typeName] = pts + resolvedPoints;
        }

        snapshot.totalPointsDuLot = totalPoints;

        return snapshot;
    }

    /// <summary>
    /// Applique uniquement la famille A au type logique d'une bille.
    ///
    /// La famille B est appliquée dans BuildSnapshot car elle dépend
    /// de compteurs par flush.
    /// </summary>
    private BallType ResolveTypeForFlushFamilyA(BallState st)
    {
        if (st == null)
            return BallType.White;

        if (BlackFilterRuntimeController.Instance != null)
        {
            bool consumed =
                BlackFilterRuntimeController.Instance.ConsumeReservation(st);

            if (consumed)
                return BallType.White;
        }

        return st.type;
    }


    /// <summary>
    /// Retourne les points associés au type logique final.
    /// </summary>
    private int GetPointsForResolvedType(
        BallState source,
        BallType resolvedType)
    {
        switch (resolvedType)
        {
            case BallType.White:
                return 100;

            case BallType.Blue:
                return 150;

            case BallType.Red:
                return 200;

            case BallType.Black:
                return source != null
                    ? source.points
                    : -120;

            default:
                return source != null
                    ? source.points
                    : 0;
        }
    }

    private void TriggerFlushFx(Side side, int flushScore, bool hasBlack)
    {
        BinFlushFX fx = (side == Side.Left) ? leftFlushFx : rightFlushFx;
        if (fx != null)
            fx.PlayFlush(hasBlack, flushScore);
    }

    /// <summary>
    /// SFX flush simplifié :
    /// - FlushBlack si présence de bille noire,
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

    /// <summary>
    /// Retourne le seuil de flush effectif pour un bin donné :
    /// seuil de base + bonus GREED agrégé.
    /// </summary>
    public int GetEffectiveFlushThresholdFor(BinTrigger trigger)
    {
        if (trigger == null)
            return 1;

        int baseThreshold = Mathf.Max(1, trigger.flushThreshold);

        int bonus = 0;
        if (ModuleRuntimeStats.Instance != null)
            bonus = Mathf.Max(0, ModuleRuntimeStats.Instance.FlushMinBallsAdd);

        return Mathf.Max(1, baseThreshold + bonus);
    }
}