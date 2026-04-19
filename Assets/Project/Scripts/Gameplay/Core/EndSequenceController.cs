using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gere la phase d evacuation de fin de niveau :
/// - laisse le joueur evacuer les billes pendant une duree definie,
/// - active l auto-flush des bins,
/// - force un flush final,
/// - attend la fin des animations UI utiles,
/// - peut jouer un callback intermediaire avant fermeture du board,
/// - coupe les controles,
/// - joue l outro du board,
/// - masque le HUD,
/// - puis appelle le callback de fin normale.
///
/// IMPORTANT :
/// Si le Hull tombe a 0 pendant l evacuation / final flush,
/// la ceremonie normale NE DOIT PAS se lancer.
/// Le flow GameOver Hull doit garder la priorite.
///
/// Musique :
/// - on baisse la musique des le debut du countdown d evacuation,
/// - on la laisse basse pendant toute la sequence de fin,
/// - on ne la remonte pas ici.
/// </summary>
public class EndSequenceController : MonoBehaviour
{
    [Header("References gameplay")]
    [SerializeField] private BinCollector collector;
    [SerializeField] private PlayerController player;
    [SerializeField] private CloseBinController closeBinController;
    [SerializeField] private PauseOverlayController pauseOverlayController;

    [Header("Run / Persistance")]
    [SerializeField] private LevelRunStateController runStateController;
    [SerializeField] private RunSessionState runSessionState;

    [Header("Evacuation")]
    [Tooltip("Duree de la phase d evacuation en secondes.")]
    [SerializeField] private float evacDurationSec = 10f;

    [Tooltip("Intervalle entre deux ticks de callback UI (compteur).")]
    [SerializeField] private float tickIntervalSec = 1f;

    [Header("Progression (UI)")]
    [Tooltip("ProgressBar utilisee pendant le niveau. Sert a attendre la fin de l animation step-by-step.")]
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

    [Tooltip("Delai entre le masquage du HUD et l appel du callback de fin normale.")]
    [SerializeField] private float hudToCeremonyDelaySec = 0.25f;

    [Header("Music Ducking")]
    [Tooltip("Si vrai, baisse la musique des le debut de l evacuation.")]
    [SerializeField] private bool duckMusicDuringEvacuation = true;

    [Tooltip("Multiplicateur de volume musique applique pendant toute la sequence de fin.")]
    [Range(0f, 1f)]
    [SerializeField] private float evacuationMusicVolumeMult = 0.6f;

    [Tooltip("Fade du ducking musique au debut de l evacuation.")]
    [SerializeField] private float evacuationMusicDuckFadeSec = 1.6f;

    private BoardOutroAssembler boardOutro;
    private Coroutine co;

    // Callbacks optionnels pour l UI d evacuation.
    private Action onEvacStart;
    private Action<float> onEvacTick;

    // Callback optionnel joue apres l evac / final flush / progress bar,
    // mais avant fermeture du board et masquage du HUD.
    private Func<IEnumerator> onBeforeBoardOutro;

    // Event public pour prevenir le reste du jeu du debut d evacuation.
    public event Action OnEvacuationStarted;

    // Event public qui previent que le gameplay est definitivement scelle.
    public event Action OnGameplaySealed;

    private void Awake()
    {
        if (boardRoot != null)
        {
            boardOutro = boardRoot.GetComponent<BoardOutroAssembler>();

            if (boardOutro == null)
                Debug.LogWarning("[EndSequenceController] Aucun BoardOutroAssembler trouve sur boardRoot.");
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
        PauseOverlayController pc,
        float evacDuration = -1f,
        float tickInterval = -1f,
        Action onEvacStartCb = null,
        Action<float> onEvacTickCb = null,
        ProgressBarUI progressBar = null,
        Func<IEnumerator> onBeforeBoardOutroCb = null)
    {
        collector = c;
        player = p;
        closeBinController = cb;
        pauseOverlayController = pc;

        if (evacDuration > 0f)
            evacDurationSec = evacDuration;

        if (tickInterval > 0f)
            tickIntervalSec = tickInterval;

        onEvacStart = onEvacStartCb;
        onEvacTick = onEvacTickCb;
        onBeforeBoardOutro = onBeforeBoardOutroCb;

        if (progressBar != null)
            progressBarUI = progressBar;
    }

    /// <summary>
    /// Reinitialise l etat interne du controleur.
    /// Coupe la coroutine en cours si necessaire.
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
    /// Coupe immediatement toute la sequence d evacuation / fin normale.
    /// A utiliser quand un GameOver Hull prend la priorite absolue.
    /// </summary>
    public void AbortSequence()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        collector?.SetAutoFlushEnabled(false);

        if (levelControls != null)
        {
            levelControls.DisableGameplayControls();
        }
        else
        {
            player?.SetActiveControl(false);
            closeBinController?.SetActiveControl(false);
        }

        pauseOverlayController?.EnablePause(false);
        pauseOverlayController?.ForceResume();
    }

    /// <summary>
    /// Lance la phase d evacuation.
    /// Le callback onCompleted correspond a la fin normale :
    /// ceremonie, overlay de fin, etc.
    ///
    /// IMPORTANT :
    /// si le Hull est detruit avant la fin, ce callback ne sera pas appele.
    /// </summary>
    public void BeginEvacuationPhase(Action onCompleted, float? overrideDurationSec = null)
    {
        if (co == null)
            co = StartCoroutine(RunEvac(onCompleted, overrideDurationSec));
    }

    /// <summary>
    /// Retourne true si le vaisseau est deja detruit.
    /// Dans ce cas, la ceremonie normale ne doit pas se lancer.
    /// </summary>
    private bool IsHullDestroyed()
    {
        return runSessionState != null && runSessionState.Hull <= 0;
    }

    /// <summary>
    /// Coroutine principale de la phase d evacuation.
    /// </summary>
    private IEnumerator RunEvac(Action done, float? overrideDurationSec)
    {
        float duration = overrideDurationSec.HasValue
            ? Mathf.Max(0f, overrideDurationSec.Value)
            : evacDurationSec;

        // --------------------------------------------------------------------
        // 1) Debut d evacuation
        // --------------------------------------------------------------------

        if (duckMusicDuringEvacuation)
            AudioManager.Instance?.SetMusicVolumeMultiplier(evacuationMusicVolumeMult, evacuationMusicDuckFadeSec);

        pauseOverlayController?.EnablePause(true);

        if (levelControls != null)
        {
            levelControls.EnableGameplayControls();
        }
        else
        {
            player?.SetActiveControl(true);
            closeBinController?.SetActiveControl(true);
        }

        collector?.SetAutoFlushEnabled(true);

        OnEvacuationStarted?.Invoke();
        onEvacStart?.Invoke();

        // --------------------------------------------------------------------
        // 2) Compte a rebours d evacuation
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

        onEvacTick?.Invoke(0f);

        // --------------------------------------------------------------------
        // 3) Stop auto-flush
        // --------------------------------------------------------------------

        collector?.SetAutoFlushEnabled(false);

        // --------------------------------------------------------------------
        // 4) Attente de la fin d un flush eventuellement encore en cours
        // --------------------------------------------------------------------

        if (collector != null && collector.IsAnyFlushActive)
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);

        // --------------------------------------------------------------------
        // 5) Flush final force
        // --------------------------------------------------------------------

        if (collector != null)
        {
            collector.CollectAll(force: true, skipDelay: true, isFinalFlush: true);
            yield return new WaitUntil(() => !collector.IsAnyFlushActive);
        }

        // --------------------------------------------------------------------
        // 5a) Le gameplay est maintenant considere comme termine
        // --------------------------------------------------------------------

        runStateController?.MarkLevelEnded();
        OnGameplaySealed?.Invoke();

        // --------------------------------------------------------------------
        // 5b) On laisse le temps a la ProgressBar de finir ses animations
        // --------------------------------------------------------------------

        if (progressBarUI != null)
        {
            progressBarUI.Refresh();
            yield return progressBarUI.WaitForProgressAnimationComplete(progressAnimTimeoutSec);
        }

        // --------------------------------------------------------------------
        // 5c) Callback intermediaire avant fermeture du board
        // --------------------------------------------------------------------

        if (onBeforeBoardOutro != null)
            yield return StartCoroutine(onBeforeBoardOutro());

        // --------------------------------------------------------------------
        // 6) On coupe les controles gameplay
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

        pauseOverlayController?.EnablePause(false);
        pauseOverlayController?.ForceResume();

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
        // 9) Petit delai avant la fin normale
        // --------------------------------------------------------------------

        if (hudToCeremonyDelaySec > 0f)
            yield return new WaitForSeconds(hudToCeremonyDelaySec);

        // --------------------------------------------------------------------
        // 10) Verrou critique
        // --------------------------------------------------------------------

        if (IsHullDestroyed())
        {
            Debug.Log("[EndSequenceController] Ceremonie normale annulee : Hull <= 0.");
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