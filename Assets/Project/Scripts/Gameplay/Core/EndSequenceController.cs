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

    // Event public pour prévenir le reste du jeu
    public event Action OnEvacuationStarted;

    // ------------------------------
    //   CONFIGURATION
    // ------------------------------
    public void Configure(
        BinCollector c,
        PlayerController p,
        CloseBinController cb,
        PauseController pc,
        float evacDuration = -1f,
        float tickInterval = -1f,
        Action onEvacStartCb = null,
        Action<float> onEvacTickCb = null)
    {
        collector = c;
        player = p;
        closeBinController = cb;
        pauseController = pc;

        if (evacDuration > 0f)
            evacDurationSec = evacDuration;

        if (tickInterval > 0f)
            tickIntervalSec = tickInterval;

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

        // Notifie le monde que l'évac commence
        OnEvacuationStarted?.Invoke();

        // Callback UI (ex: lancement du countdown)
        onEvacStart?.Invoke();

        // 2) Compte à rebours en TEMPS SCALÉ (respecte Time.timeScale)
        float remaining = duration;
        float tickTimer = 0f;

        while (remaining > 0f)
        {
            float dt = Time.deltaTime;

            // Si le jeu est complètement figé par safety (Time.timeScale = 0),
            // dt sera 0, donc la boucle ne progresse pas.
            remaining -= dt;
            tickTimer += dt;

            if (remaining < 0f)
                remaining = 0f;

            // Tick logique toutes les tickIntervalSec
            if (tickIntervalSec > 0f && tickTimer >= tickIntervalSec)
            {
                tickTimer -= tickIntervalSec;
                onEvacTick?.Invoke(remaining);
            }

            yield return null;
        }

        // Tick final à 0 pour terminer proprement
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

        // 7) Callback de fin
        done?.Invoke();
        co = null;
    }
}
