using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gère la phase d'évacuation de fin de niveau :
/// - Laisse le joueur évacuer les billes pendant une durée définie.
/// - Active l'auto-flush des bins.
/// - Force un flush final, attend la fin des flushs.
/// - Coupe les contrôles, lance l'outro du board, cache le HUD.
/// - Puis, après un léger délai, appelle le callback de fin (cérémonie, etc.).
/// </summary>
public class EndSequenceController : MonoBehaviour
{
    [Header("Références gameplay")]
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;               // encore là pour compat, mais idéalement géré via LevelControlsController
    [SerializeField] private CloseBinController closeBinController; // idem
    [SerializeField] private PauseController pauseController;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Evacuation")]
    [Tooltip("Durée de la phase d'évacuation en secondes.")]
    [SerializeField] private float evacDurationSec = 10f;

    [Tooltip("Intervalle entre deux ticks de callback UI (compteur).")]
    [SerializeField] private float tickIntervalSec = 1f;

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

    [Tooltip("Délai entre le masquage du HUD et l'appel du callback de fin (cérémonie).")]
    [SerializeField] private float hudToCeremonyDelaySec = 0.25f;

    private BoardOutroAssembler boardOutro;
    private Coroutine co;

    // Callbacks optionnels pour l’UI d’évacuation
    private Action onEvacStart;
    private Action<float> onEvacTick;

    // Event public pour prévenir le reste du jeu (début de l'évacuation)
    public event Action OnEvacuationStarted;

    private void Awake()
    {
        if (boardRoot != null)
        {
            boardOutro = boardRoot.GetComponent<BoardOutroAssembler>();
            if (boardOutro == null)
            {
                Debug.LogWarning("[EndSequenceController] Aucun BoardOutroAssembler trouvé sur boardRoot.");
            }
        }
        else
        {
            Debug.LogWarning("[EndSequenceController] boardRoot non assigné.");
        }
    }

    // ------------------------------
    //   CONFIGURATION
    // ------------------------------

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

    /// <summary>
    /// Réinitialise l'état interne (arrêt de la coroutine en cours).
    /// </summary>
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

    /// <summary>
    /// Lance la phase d'évacuation.
    /// onCompleted sera appelé une fois que tout est terminé :
    /// évac + flush final + outro + hide HUD + délai, puis cérémonie.
    /// </summary>
    public void BeginEvacuationPhase(Action onCompleted, float? overrideDurationSec = null)
    {
        if (co == null)
            co = StartCoroutine(RunEvac(onCompleted, overrideDurationSec));
    }

    /// <summary>
    /// Coroutine principale de la phase d'évacuation et transition vers la fin de niveau.
    /// </summary>
    private IEnumerator RunEvac(Action done, float? overrideDurationSec)
    {
        float duration = overrideDurationSec.HasValue
            ? Mathf.Max(0f, overrideDurationSec.Value)
            : evacDurationSec;

        // 1) Début évacuation : on autorise la pause, on active les contrôles, on active l'auto-flush
        pauseController?.EnablePause(true);

        if (levelControls != null)
        {
            levelControls.EnableGameplayControls();
        }
        else
        {
            // Fallback si LevelControlsController n'est pas assigné
            player?.SetActiveControl(true);
            closeBinController?.SetActiveControl(true);
        }

        collector?.SetAutoFlushEnabled(true);

        OnEvacuationStarted?.Invoke();
        onEvacStart?.Invoke();

        // 2) Compte à rebours en temps scalé
        float remaining = duration;
        float tickTimer = 0f;

        while (remaining > 0f)
        {
            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;

            if (remaining < 0f)
                remaining = 0f;

            if (tickIntervalSec > 0f && tickTimer >= tickIntervalSec)
            {
                tickTimer -= tickIntervalSec;
                onEvacTick?.Invoke(remaining);
            }

            yield return null;
        }

        // Tick final à 0
        onEvacTick?.Invoke(0f);

        // 3) Stop auto-flush
        collector?.SetAutoFlushEnabled(false);

        // 4) Attente fin d’un flush éventuel
        if (collector != null && collector.IsAnyFlushActive)
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);

        // 5) Flush final forcé
        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: true, isFinalFlush: true);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        // 6) Couper les contrôles de gameplay (player + close bin + UI mobile)
        if (levelControls != null)
        {
            levelControls.DisableGameplayControls();
        }
        else
        {
            player?.SetActiveControl(false);
            closeBinController?.SetActiveControl(false);
        }

        // 7) Rangement visuel du board
        if (boardOutro != null)
        {
            if (outroStartDelaySec > 0f)
                yield return new WaitForSeconds(outroStartDelaySec);

            yield return StartCoroutine(boardOutro.PlayOutro());
        }

        // 8) Cacher le HUD gameplay
        if (gameplayHudRoot != null)
            gameplayHudRoot.SetActive(false);

        // 9) Léger délai avant la cérémonie de fin (EndLevelUI, etc.)
        if (hudToCeremonyDelaySec > 0f)
            yield return new WaitForSeconds(hudToCeremonyDelaySec);

        // 10) Callback de fin : le reste du jeu peut enchaîner (cérémonie, end UI, changement de scène)
        done?.Invoke();
        co = null;
    }
}
