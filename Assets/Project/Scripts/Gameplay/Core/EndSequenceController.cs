using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gere la phase d evacuation de fin de niveau :
/// - Laisse le joueur evacuer les billes pendant une duree definie.
/// - Active l auto-flush des bins.
/// - Force un flush final, attend la fin des flushs.
/// - Attend la fin de l animation de la ProgressBar (step-by-step) avant de lancer l overlay de fin.
/// - Coupe les controles, lance l outro du board, cache le HUD.
/// - Puis, apres un leger delai, appelle le callback de fin (ceremonie, etc.).
/// </summary>
public class EndSequenceController : MonoBehaviour
{
    [Header("References gameplay")]
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private PauseController pauseController;

    [Header("Run / Persistance")]
    [SerializeField] private LevelRunStateController runStateController;

    [Header("Evacuation")]
    [Tooltip("Duree de la phase d evacuation en secondes.")]
    [SerializeField] private float evacDurationSec = 10f;

    [Tooltip("Intervalle entre deux ticks de callback UI (compteur).")]
    [SerializeField] private float tickIntervalSec = 1f;

    [Header("Progression (UI)")]
    [Tooltip("ProgressBar (wrapper) utilisee pendant le niveau. Sert a attendre la fin de l animation step-by-step.")]
    [SerializeField] private ProgressBarUI progressBarUI;

    [Tooltip("Temps max d attente pour laisser la barre finir l animation step-by-step.")]
    [SerializeField] private float progressAnimTimeoutSec = 2f;

    [Header("Board / Outro")]
    [Tooltip("Racine du board. Doit porter un BoardOutroAssembler.")]
    [SerializeField] private Transform boardRoot;

    [Tooltip("Delai avant de lancer le rangement du board apres le dernier flush.")]
    [SerializeField] private float outroStartDelaySec = 0.15f;

    [Header("HUD")]
    [Tooltip("Racine du HUD gameplay (Canvas HUD principal).")]
    [SerializeField] private GameObject gameplayHudRoot;

    [Header("Controles")]
    [Tooltip("Controleur centralise des controles de gameplay (player + CloseBin + UI mobile).")]
    [SerializeField] private LevelControlsController levelControls;

    [Tooltip("Delai entre le masquage du HUD et l appel du callback de fin (ceremonie).")]
    [SerializeField] private float hudToCeremonyDelaySec = 0.25f;

    private BoardOutroAssembler boardOutro;
    private Coroutine co;

    // Callbacks optionnels pour l UI d evacuation (affichage, etc.)
    private Action onEvacStart;
    private Action<float> onEvacTick;

    // Event public pour prevenir le reste du jeu (debut de l evacuation)
    public event Action OnEvacuationStarted;

    // Event public qui previent que le gameplay est scellé
    public event Action OnGameplaySealed;


    private void Awake()
    {
        if (boardRoot != null)
        {
            boardOutro = boardRoot.GetComponent<BoardOutroAssembler>();
            if (boardOutro == null)
            {
                Debug.LogWarning("[EndSequenceController] Aucun BoardOutroAssembler trouve sur boardRoot.");
            }
        }
        else
        {
            Debug.LogWarning("[EndSequenceController] boardRoot non assigne.");
        }
    }

    /// <summary>
    /// Configure dynamiquement les references et parametres de la phase d evacuation.
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

        // Optionnel : si fourni, on ecrase la ref Inspector
        if (progressBar != null)
            progressBarUI = progressBar;
    }

    /// <summary>
    /// Reinitialise l etat interne (arret de la coroutine en cours).
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
    /// Lance la phase d evacuation.
    /// onCompleted sera appele une fois que tout est termine :
    /// evac + flush final + attente UI + outro + hide HUD + delai, puis ceremonie.
    /// </summary>
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

        // 1) Debut evacuation
        pauseController?.EnablePause(true);

        if (levelControls != null)
            levelControls.EnableGameplayControls();
        else
        {
            player?.SetActiveControl(true);
            closeBinController?.SetActiveControl(true);
        }

        collector?.SetAutoFlushEnabled(true);

        OnEvacuationStarted?.Invoke();
        onEvacStart?.Invoke();

        // 2) Compte a rebours (temps scale ici volontairement : si timescale bouge, l evac suit)
        float remaining = duration;
        float tickTimer = 0f;

        while (remaining > 0f)
        {
            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;

            if (remaining < 0f)
                remaining = 0f;

            // Tick UI robuste : si une frame lag, on rattrape les ticks manques
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

        // Dernier tick force a 0 (utile pour afficher "0" une fois)
        onEvacTick?.Invoke(0f);

        // 3) Stop auto-flush
        collector?.SetAutoFlushEnabled(false);

        // 4) Attente fin d un flush eventuel
        if (collector != null && collector.IsAnyFlushActive)
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);

        // 5) Flush final force
        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: true, isFinalFlush: true);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        // 5a) IMPORTANT : le gameplay est considere termine apres le flush final.
        // On desarme la penalite "quit = Hull -1" pour eviter une penalite fantome au prochain Load.
        runStateController?.MarkLevelEnded();
        OnGameplaySealed?.Invoke();

        // 5bis) IMPORTANT : laisser la ProgressBar finir son step-by-step
        // On force un Refresh au cas ou le dernier flush n aurait pas declenche l event UI.
        if (progressBarUI != null)
        {
            progressBarUI.Refresh();
            yield return progressBarUI.WaitForProgressAnimationComplete(progressAnimTimeoutSec);
        }

        // 6) Couper les controles de gameplay
        if (levelControls != null)
            levelControls.DisableGameplayControls();
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

        // 9) Delai avant ceremonie
        if (hudToCeremonyDelaySec > 0f)
            yield return new WaitForSeconds(hudToCeremonyDelaySec);

        // 10) Callback de fin
        done?.Invoke();
        co = null;
    }
}
