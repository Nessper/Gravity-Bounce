using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gère la phase d'évacuation de fin de niveau :
/// - laisse le joueur évacuer les billes pendant une durée définie,
/// - active l'auto-flush des bins,
/// - force un flush final,
/// - attend la fin des animations UI utiles,
/// - coupe les contrôles,
/// - joue l'outro du board,
/// - masque le HUD,
/// - puis appelle le callback de fin normale.
///
/// IMPORTANT :
/// Si le Hull tombe à 0 pendant l'évacuation / final flush,
/// la cérémonie normale NE DOIT PAS se lancer.
/// Le flow GameOver Hull doit garder la priorité.
/// </summary>
public class EndSequenceController : MonoBehaviour
{
    [Header("Références gameplay")]
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private PauseController pauseController;

    [Header("Run / Persistance")]
    [SerializeField] private LevelRunStateController runStateController;
    [SerializeField] private RunSessionState runSessionState;

    [Header("Evacuation")]
    [Tooltip("Durée de la phase d'évacuation en secondes.")]
    [SerializeField] private float evacDurationSec = 10f;

    [Tooltip("Intervalle entre deux ticks de callback UI (compteur).")]
    [SerializeField] private float tickIntervalSec = 1f;

    [Header("Progression (UI)")]
    [Tooltip("ProgressBar utilisée pendant le niveau. Sert à attendre la fin de l'animation step-by-step.")]
    [SerializeField] private ProgressBarUI progressBarUI;

    [Tooltip("Temps max d'attente pour laisser la barre finir l'animation step-by-step.")]
    [SerializeField] private float progressAnimTimeoutSec = 2f;

    [Header("Board / Outro")]
    [Tooltip("Racine du board. Doit porter un BoardOutroAssembler.")]
    [SerializeField] private Transform boardRoot;

    [Tooltip("Délai avant de lancer le rangement du board après le dernier flush.")]
    [SerializeField] private float outroStartDelaySec = 0.15f;

    [Header("HUD")]
    [Tooltip("Racine du HUD gameplay (Canvas HUD principal).")]
    [SerializeField] private GameObject gameplayHudRoot;

    [Header("Contrôles")]
    [Tooltip("Contrôleur centralisé des contrôles de gameplay (player + CloseBin + UI mobile).")]
    [SerializeField] private LevelControlsController levelControls;

    [Tooltip("Délai entre le masquage du HUD et l'appel du callback de fin normale.")]
    [SerializeField] private float hudToCeremonyDelaySec = 0.25f;

    private BoardOutroAssembler boardOutro;
    private Coroutine co;

    // Callbacks optionnels pour l'UI d'évacuation.
    private Action onEvacStart;
    private Action<float> onEvacTick;

    // Event public pour prévenir le reste du jeu du début d'évacuation.
    public event Action OnEvacuationStarted;

    // Event public qui prévient que le gameplay est définitivement scellé.
    public event Action OnGameplaySealed;

    private void Awake()
    {
        // Récupère le composant d'outro du board si possible.
        if (boardRoot != null)
        {
            boardOutro = boardRoot.GetComponent<BoardOutroAssembler>();

            if (boardOutro == null)
                Debug.LogWarning("[EndSequenceController] Aucun BoardOutroAssembler trouvé sur boardRoot.");
        }
        else
        {
            Debug.LogWarning("[EndSequenceController] boardRoot non assigné.");
        }
    }

    /// <summary>
    /// Configure dynamiquement les références et paramètres de la phase d'évacuation.
    /// </summary>
    public void Configure(
        BinCollector c,
        PlayerController p,
        CloseBinController cb,
        PauseController pc,
        float evacDuration = -1f,
        float tickInterval = -1f,
        Action onEvacStartCb = null,
        Action<float> onEvacTickCb = null,
        ProgressBarUI progressBar = null)
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

        // Si une ProgressBar est fournie ici, elle remplace celle de l'Inspector.
        if (progressBar != null)
            progressBarUI = progressBar;
    }

    /// <summary>
    /// Réinitialise l'état interne du contrôleur.
    /// Coupe la coroutine en cours si nécessaire.
    /// </summary>
    public void ResetState()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }
    }

    /// <summary>
    /// Coupe immediatement toute la sequence d'evacuation / fin normale.
    /// A utiliser quand un GameOver Hull prend la priorite absolue.
    /// </summary>
    public void AbortSequence()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        // Stop auto-flush evac si actif
        collector?.SetAutoFlushEnabled(false);

        // Coupe les controles gameplay
        if (levelControls != null)
        {
            levelControls.DisableGameplayControls();
        }
        else
        {
            player?.SetActiveControl(false);
            closeBinController?.SetActiveControl(false);
        }

        // La pause n'a plus lieu d'etre pendant un GameOver force
        pauseController?.EnablePause(false);
    }

    /// <summary>
    /// Lance la phase d'évacuation.
    /// Le callback onCompleted correspond à la fin normale :
    /// cérémonie, overlay de fin, etc.
    /// 
    /// IMPORTANT :
    /// si le Hull est détruit avant la fin, ce callback ne sera pas appelé.
    /// </summary>
    public void BeginEvacuationPhase(Action onCompleted, float? overrideDurationSec = null)
    {
        // Évite de lancer deux fois la même séquence.
        if (co == null)
            co = StartCoroutine(RunEvac(onCompleted, overrideDurationSec));
    }

    /// <summary>
    /// Retourne true si le vaisseau est déjà détruit.
    /// Dans ce cas, la cérémonie normale ne doit pas se lancer.
    /// </summary>
    private bool IsHullDestroyed()
    {
        return runSessionState != null && runSessionState.Hull <= 0;
    }

    /// <summary>
    /// Coroutine principale de la phase d'évacuation.
    /// </summary>
    private IEnumerator RunEvac(Action done, float? overrideDurationSec)
    {
        float duration = overrideDurationSec.HasValue
            ? Mathf.Max(0f, overrideDurationSec.Value)
            : evacDurationSec;

        // --------------------------------------------------------------------
        // 1) Début d'évacuation
        // --------------------------------------------------------------------

        // On autorise la pause pendant l'évacuation.
        pauseController?.EnablePause(true);

        // On laisse les contrôles gameplay actifs pendant l'évacuation.
        if (levelControls != null)
        {
            levelControls.EnableGameplayControls();
        }
        else
        {
            player?.SetActiveControl(true);
            closeBinController?.SetActiveControl(true);
        }

        // Active l'auto-flush des bins pendant l'évacuation.
        collector?.SetAutoFlushEnabled(true);

        // Notifie le reste du jeu / UI.
        OnEvacuationStarted?.Invoke();
        onEvacStart?.Invoke();

        // --------------------------------------------------------------------
        // 2) Compte à rebours d'évacuation
        // --------------------------------------------------------------------

        float remaining = duration;
        float tickTimer = 0f;

        while (remaining > 0f)
        {
            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;

            if (remaining < 0f)
                remaining = 0f;

            // Si l'intervalle de tick est défini, on rattrape les ticks manqués
            // même en cas de frame longue.
            if (tickIntervalSec > 0f)
            {
                while (tickTimer >= tickIntervalSec)
                {
                    tickTimer -= tickIntervalSec;
                    onEvacTick?.Invoke(remaining);
                }
            }

            yield return null;
        }

        // Force un dernier tick à 0 pour l'UI du compteur.
        onEvacTick?.Invoke(0f);

        // --------------------------------------------------------------------
        // 3) Stop auto-flush
        // --------------------------------------------------------------------

        collector?.SetAutoFlushEnabled(false);

        // --------------------------------------------------------------------
        // 4) Attente de la fin d'un flush éventuellement encore en cours
        // --------------------------------------------------------------------

        if (collector != null && collector.IsAnyFlushActive)
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);

        // --------------------------------------------------------------------
        // 5) Flush final forcé
        // --------------------------------------------------------------------

        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: true, isFinalFlush: true);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        // --------------------------------------------------------------------
        // 5a) Le gameplay est maintenant considéré comme terminé
        // --------------------------------------------------------------------

        // On désarme les pénalités de quit/relaunch liées au niveau en cours.
        runStateController?.MarkLevelEnded();
        OnGameplaySealed?.Invoke();

        // --------------------------------------------------------------------
        // 5b) On laisse le temps à la ProgressBar de finir ses animations
        // --------------------------------------------------------------------

        if (progressBarUI != null)
        {
            // Refresh défensif au cas où le dernier flush n'aurait pas poussé l'update.
            progressBarUI.Refresh();
            yield return progressBarUI.WaitForProgressAnimationComplete(progressAnimTimeoutSec);
        }

        // --------------------------------------------------------------------
        // 6) On coupe les contrôles gameplay
        // --------------------------------------------------------------------

        if (levelControls != null)
        {
            levelControls.DisableGameplayControls();
        }
        else
        {
            player?.SetActiveControl(false);
            closeBinController?.SetActiveControl(false);
        }

        // --------------------------------------------------------------------
        // 7) Outro visuelle du board
        // --------------------------------------------------------------------

        if (boardOutro != null)
        {
            if (outroStartDelaySec > 0f)
                yield return new WaitForSeconds(outroStartDelaySec);

            yield return StartCoroutine(boardOutro.PlayOutro());
        }

        // --------------------------------------------------------------------
        // 8) Masquage du HUD gameplay
        // --------------------------------------------------------------------

        if (gameplayHudRoot != null)
            gameplayHudRoot.SetActive(false);

        // --------------------------------------------------------------------
        // 9) Petit délai avant la fin normale
        // --------------------------------------------------------------------

        if (hudToCeremonyDelaySec > 0f)
            yield return new WaitForSeconds(hudToCeremonyDelaySec);

        // --------------------------------------------------------------------
        // 10) VERROU CRITIQUE :
        // si le Hull est tombé à 0 pendant l'évacuation / final flush,
        // on NE LANCE PAS la cérémonie normale.
        // Le flow GameOver Hull doit rester seul maître à bord.
        // --------------------------------------------------------------------

        if (IsHullDestroyed())
        {
            Debug.Log("[EndSequenceController] Cérémonie normale annulée : Hull <= 0.");
            co = null;
            yield break;
        }

        // --------------------------------------------------------------------
        // 11) Callback de fin normale
        // --------------------------------------------------------------------

        done?.Invoke();
        co = null;
    }
}