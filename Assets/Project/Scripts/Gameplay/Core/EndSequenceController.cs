using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;


public class EndSequenceController : MonoBehaviour
{
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private PauseController pauseController;
    [SerializeField] private ScoreManager scoreManager;


    [Header("Evacuation")]
    [SerializeField] private float evacDurationSec = 10f;   // durée d'évacuation
    [SerializeField] private float tickIntervalSec = 1f;    // cadence du callback tick (pour UI compteur)

    private Coroutine co;

    // Callbacks optionnels pour l’UI
    private Action onEvacStart;
    private Action<float> onEvacTick;

    // ------------------------------
    //   CONFIGURATION
    // ------------------------------
    public void Configure(
        BinCollector c, PlayerController p, CloseBinController cb, PauseController pc,
        float evacDuration = -1f, float tickInterval = -1f,
        Action onEvacStartCb = null, Action<float> onEvacTickCb = null)
    {
        collector = c;
        player = p;
        closeBinController = cb;
        pauseController = pc;

        if (evacDuration > 0f) evacDurationSec = evacDuration;
        if (tickInterval > 0f) tickIntervalSec = tickInterval;

        onEvacStart = onEvacStartCb;
        onEvacTick = onEvacTickCb;
    }

    public void ResetState()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }
    }

    // ------------------------------
    //   PHASE D'ÉVACUATION
    // ------------------------------
    public void BeginEvacuationPhase(Action onCompleted, float? overrideDurationSec = null)
    {
        if (co == null)
            co = StartCoroutine(RunEvac(onCompleted, overrideDurationSec));
    }

    private IEnumerator RunEvac(Action done, float? overrideDurationSec)
    {
        float duration = overrideDurationSec.HasValue
            ? Mathf.Max(0f, overrideDurationSec.Value)
            : evacDurationSec;

        // 1) Début évacuation : joueur actif, auto-flush actif
        pauseController?.EnablePause(true);
        player?.SetActiveControl(true);
        closeBinController?.SetActiveControl(true);
        collector?.SetAutoFlushEnabled(true);

        onEvacStart?.Invoke();

        // 2) Compte à rebours en temps réel
        float remaining = duration;
        while (remaining > 0f)
        {
            onEvacTick?.Invoke(remaining);
            float step = Mathf.Min(tickIntervalSec, remaining);
            yield return new WaitForSecondsRealtime(step);
            remaining -= step;
        }
        onEvacTick?.Invoke(0f);

        // 3) Stop auto-flush pour figer les bins
        collector?.SetAutoFlushEnabled(false);

        // 4) Attendre la fin d’un flush normal éventuel
        if (collector != null && collector.IsAnyFlushActive)
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);

        // 5) Flush final forcé (immédiat)
        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: true);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        // 6) Fin : on coupe les contrôles pour l’écran de fin
        pauseController?.EnablePause(false);
        player?.SetActiveControl(false);
        closeBinController?.SetActiveControl(false);


        // Evaluation des combos finaux (pas d’UI ici)
        var finals = EvaluateFinalCombos();



        // 7) Callback de fin
        done?.Invoke();
        co = null;
    }

    private System.Collections.Generic.List<FinalComboResult> EvaluateFinalCombos()
    {
        if (scoreManager == null)
            return null;

        // Construire le contexte pour les combos de fin
        var ctx = new FinalComboContext
        {
            timeElapsedSec = Mathf.RoundToInt(Time.timeSinceLevelLoad), // ou ton timer de LevelManager si tu veux plus précis
            totalBilles = scoreManager.TotalBilles
        };

        // Évaluer les combos finaux
        var results = FinalComboEvaluator.Evaluate(scoreManager, ctx);

        // Appliquer les points au score global
        if (results != null)
        {
            foreach (var r in results)
                scoreManager.AddPoints(r.points, r.id);
        }

        return results;
    }



}
