using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire audio global.
/// Doit etre enfant de BootRoot (BootRoot est le DontDestroyOnLoad).
/// Objectif :
/// - Tous les SFX (gameplay + UI) passent ici.
/// - Musique globale (crossfade A/B) avec transitions propres (sans "trou").
/// - Support d'un ducking (multiplicateur global) pour mettre la musique en sourdine pendant certaines phases (intro, etc).
///
/// Remarques importantes :
/// - Les fades musique utilisent Time.unscaledDeltaTime (independant du timeScale).
/// - La musique est PAUSEE quand on appelle SetPaused(true) (choix actuel).
/// - Aucune scene ne doit auto-jouer de musique. Tout est pilote via AudioManager.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // --------------------------------------------------------------------
    // Types
    // --------------------------------------------------------------------

    [Serializable]
    public class SfxEntry
    {
        [Header("Identification")]
        public SfxId id = SfxId.None;

        [Header("Audio")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0.5f, 2f)]
        public float pitch = 1f;

        [Tooltip("Si vrai, applique une variation aleatoire de pitch autour de la valeur pitch.")]
        public bool usePitchJitter = false;

        [Range(0f, 0.25f)]
        public float pitchJitter = 0.05f;
    }

    [Serializable]
    public class MusicEntry
    {
        [Header("Identification")]
        public MusicId id = MusicId.None;

        [Header("Audio")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 0.5f;

        [Tooltip("Si vrai, la musique boucle.")]
        public bool loop = true;
    }

    // --------------------------------------------------------------------
    // Singleton
    // --------------------------------------------------------------------

    private static AudioManager instance;
    public static AudioManager Instance => instance;

    // --------------------------------------------------------------------
    // Inspector
    // --------------------------------------------------------------------

    [Header("Sources - SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;

    [Header("Sources - Music (GLOBAL)")]
    [Tooltip("AudioSource musique A (crossfade).")]
    [SerializeField] private AudioSource musicSourceA;

    [Tooltip("AudioSource musique B (crossfade).")]
    [SerializeField] private AudioSource musicSourceB;

    [Header("Dialogue - Typing Loop")]
    [Tooltip("AudioSource dediee au son de typing des dialogues (en boucle).")]
    [SerializeField] private AudioSource dialogTypingSource;

    [Tooltip("Si true, le typing dialogue est considere UI (pause UI le coupe). Sinon, gameplay.")]
    [SerializeField] private bool dialogTypingUsesUiBus = false;

    [Header("Table SFX")]
    [SerializeField] private List<SfxEntry> entries = new List<SfxEntry>();

    [Header("Table Music")]
    [SerializeField] private List<MusicEntry> musicEntries = new List<MusicEntry>();

    [Header("Music - Ducking")]
    [Tooltip("Multiplicateur global applique au volume de la musique. 1 = normal, 0.25 = sourdine.")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolumeMultiplier = 1f;

    // --------------------------------------------------------------------
    // Runtime maps
    // --------------------------------------------------------------------

    private readonly Dictionary<SfxId, SfxEntry> map = new Dictionary<SfxId, SfxEntry>();
    private readonly Dictionary<MusicId, MusicEntry> musicMap = new Dictionary<MusicId, MusicEntry>();

    // --------------------------------------------------------------------
    // Music runtime state
    // --------------------------------------------------------------------

    private Coroutine musicFadeCo;
    private Coroutine musicMultiplierCo;

    private MusicId currentMusicId = MusicId.None;

    // Source active/inactive pour crossfade
    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;

    // Volumes "base" (sans multiplicateur) pilotes par les coroutines.
    // Le volume reel applique a l'AudioSource est : base * musicVolumeMultiplier.
    private float activeBaseVolume;
    private float inactiveBaseVolume;

    // --------------------------------------------------------------------
    // Unity
    // --------------------------------------------------------------------

    private void Awake()
    {
        // Garantit un seul AudioManager (au cas ou une scene en aurait un par erreur).
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        BootRoot.RegisterAudio(this);

        RebuildMap();
        RebuildMusicMap();
        ValidateSources();

        activeMusicSource = musicSourceA;
        inactiveMusicSource = musicSourceB;

        ForceStopMusicSources();
    }

    // --------------------------------------------------------------------
    // Build maps
    // --------------------------------------------------------------------

    public void RebuildMap()
    {
        map.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            SfxEntry e = entries[i];
            if (e == null)
                continue;

            if (e.id == SfxId.None)
                continue;

            if (map.ContainsKey(e.id))
            {
                Debug.LogWarning("[AudioManager] Doublon detecte pour id: " + e.id + ". Le premier est conserve.");
                continue;
            }

            map.Add(e.id, e);
        }
    }

    public void RebuildMusicMap()
    {
        musicMap.Clear();

        for (int i = 0; i < musicEntries.Count; i++)
        {
            MusicEntry e = musicEntries[i];
            if (e == null)
                continue;

            if (e.id == MusicId.None)
                continue;

            if (musicMap.ContainsKey(e.id))
            {
                Debug.LogWarning("[AudioManager] Doublon detecte pour MusicId: " + e.id + ". Le premier est conserve.");
                continue;
            }

            musicMap.Add(e.id, e);
        }
    }

    // --------------------------------------------------------------------
    // Public API - Gameplay SFX (NE PAS CHANGER)
    // --------------------------------------------------------------------

    public void PlaySfx(SfxId id)
    {
        PlayInternal(id, isUi: false, pitchOverride: null, volumeMult: 1f);
    }

    public void PlaySfx(SfxId id, float pitchOverride)
    {
        PlayInternal(id, isUi: false, pitchOverride: pitchOverride, volumeMult: 1f);
    }

    public void PlaySfx(SfxId id, float pitchOverride, float volumeMult)
    {
        PlayInternal(id, isUi: false, pitchOverride: pitchOverride, volumeMult: volumeMult);
    }

    // --------------------------------------------------------------------
    // Public API - UI SFX (NE PAS CHANGER)
    // --------------------------------------------------------------------

    public void PlayUi(SfxId id)
    {
        PlayInternal(id, isUi: true, pitchOverride: null, volumeMult: 1f);
    }

    public void PlayUi(SfxId id, float pitchOverride)
    {
        PlayInternal(id, isUi: true, pitchOverride: pitchOverride, volumeMult: 1f);
    }

    public void PlayUi(SfxId id, float pitchOverride, float volumeMult)
    {
        PlayInternal(id, isUi: true, pitchOverride: pitchOverride, volumeMult: volumeMult);
    }

    // --------------------------------------------------------------------
    // Music API
    // --------------------------------------------------------------------

    /// <summary>
    /// Joue une musique globale (loop) selon MusicId.
    /// - Si la meme musique est deja en cours, ne fait rien.
    /// - Si une transition est en cours, elle est stoppee et remplacee.
    /// - Crossfade A/B (pas de trou).
    /// - Fades en unscaled time.
    /// </summary>
    public void PlayMusic(MusicId id, float fadeOutSec = 0.8f, float fadeInSec = 0.8f)
    {
        if (musicSourceA == null || musicSourceB == null)
        {
            Debug.LogWarning("[AudioManager] musicSourceA/B non assignees.");
            return;
        }

        if (id == MusicId.None)
        {
            StopMusic(fadeOutSec);
            return;
        }

        // Anti-relance : continuité Title -> ShipSelect, etc.
        if (id == currentMusicId && activeMusicSource != null && activeMusicSource.isPlaying)
            return;

        if (!musicMap.TryGetValue(id, out MusicEntry entry) || entry == null || entry.clip == null)
        {
            Debug.LogWarning("[AudioManager] MusicId non configure ou clip manquant: " + id);
            return;
        }

        StartMusicTransition(entry, id, fadeOutSec, fadeInSec);
    }

    /// <summary>
    /// Stoppe la musique globale avec fade out.
    /// On fade out les 2 sources (cas ou une transition etait en cours).
    /// </summary>
    public void StopMusic(float fadeOutSec = 0.8f)
    {
        if (musicSourceA == null || musicSourceB == null)
            return;

        if (musicFadeCo != null)
        {
            StopCoroutine(musicFadeCo);
            musicFadeCo = null;
        }

        bool anyPlaying = (musicSourceA.isPlaying || musicSourceB.isPlaying);
        if (!anyPlaying)
        {
            currentMusicId = MusicId.None;
            ForceStopMusicSources();
            return;
        }

        musicFadeCo = StartCoroutine(FadeOutAndStopMusicCo(fadeOutSec));
    }

    /// <summary>
    /// Fixe un multiplicateur global de volume musique (ducking).
    /// Exemple : 0.25 pendant l intro, puis 1.0 au debut gameplay.
    /// Fade en unscaled time.
    ///
    /// IMPORTANT : dans cette implementation, les coroutines musique pilotent des "base volumes".
    /// Le volume reel applique aux AudioSources est base * musicVolumeMultiplier.
    /// Donc changer le multiplier affecte immediatement le rendu, sans casser le crossfade.
    /// </summary>
    public void SetMusicVolumeMultiplier(float targetMult, float fadeSec = 0.3f)
    {
        float clamped = Mathf.Clamp01(targetMult);

        if (musicMultiplierCo != null)
        {
            StopCoroutine(musicMultiplierCo);
            musicMultiplierCo = null;
        }

        if (fadeSec <= 0f)
        {
            musicVolumeMultiplier = clamped;
            ApplyMusicVolumes();
            return;
        }

        musicMultiplierCo = StartCoroutine(MusicVolumeMultiplierCo(clamped, fadeSec));
    }

    private IEnumerator MusicVolumeMultiplierCo(float target, float fadeSec)
    {
        float start = musicVolumeMultiplier;
        float dur = Mathf.Max(0.0001f, fadeSec);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);

            musicVolumeMultiplier = Mathf.Lerp(start, target, a);
            ApplyMusicVolumes();

            yield return null;
        }

        musicVolumeMultiplier = target;
        ApplyMusicVolumes();

        musicMultiplierCo = null;
    }

    /// <summary>
    /// Snap volume musique (utile debug / cas d'urgence).
    /// Applique sur la source active (volume reel).
    /// </summary>
    public void SnapMusicToTargetVolume(float targetVolume)
    {
        if (activeMusicSource == null)
            return;

        if (musicFadeCo != null)
        {
            StopCoroutine(musicFadeCo);
            musicFadeCo = null;
        }

        // On fixe le base volume actif pour que ApplyMusicVolumes reste coherent.
        activeBaseVolume = Mathf.Clamp01(targetVolume);
        ApplyMusicVolumes();
    }

    /// <summary>
    /// Met en pause / reprend la musique (choix actuel : on pause la musique).
    /// On pause les 2 sources (au cas ou).
    /// </summary>
    public void SetMusicPaused(bool paused)
    {
        if (musicSourceA == null || musicSourceB == null)
            return;

        if (paused)
        {
            musicSourceA.Pause();
            musicSourceB.Pause();
        }
        else
        {
            musicSourceA.UnPause();
            musicSourceB.UnPause();
        }
    }

    // =======================
    // MUSIC (Clip API - NEW)
    // =======================

    private AudioClip currentMusicClip;

    /// <summary>
    /// Joue une musique à partir d'un AudioClip (crossfade A/B), sans passer par MusicId.
    /// Utile pour la musique de gameplay random (LevelMusicDirector).
    /// </summary>
    public void PlayMusicClip(AudioClip clip, float baseVolume = 0.6f, bool loop = true, float fadeOutSec = 0.8f, float fadeInSec = 0.8f)
    {
        if (clip == null)
        {
            StopMusic(fadeOutSec);
            return;
        }

        if (musicSourceA == null || musicSourceB == null)
        {
            Debug.LogWarning("[AudioManager] musicSourceA/B non assignees.");
            return;
        }

        // Anti-relance : si le même clip joue déjà sur la source active.
        if (activeMusicSource != null && activeMusicSource.isPlaying && activeMusicSource.clip == clip)
            return;

        // On considère que ce n’est plus une musique “ID”.
        currentMusicId = MusicId.None;
        currentMusicClip = clip;

        // On réutilise le crossfade existant en fabriquant une entrée “inline”.
        MusicEntry temp = new MusicEntry
        {
            id = MusicId.None,
            clip = clip,
            volume = Mathf.Clamp01(baseVolume),
            loop = loop
        };

        StartMusicTransition(temp, MusicId.None, fadeOutSec, fadeInSec);
    }

    /// <summary>
    /// Retourne le clip musique en cours (utile debug).
    /// </summary>
    public AudioClip GetCurrentMusicClip()
    {
        if (activeMusicSource != null && activeMusicSource.isPlaying)
            return activeMusicSource.clip;

        return null;
    }

    // --------------------------------------------------------------------
    // Dialogue - Typing Loop
    // --------------------------------------------------------------------

    public void StartDialogTypingLoop(AudioClip clip, float pitch)
    {
        if (dialogTypingSource == null)
        {
            Debug.LogWarning("[AudioManager] dialogTypingSource non assigne (typing dialogue).");
            return;
        }

        if (clip == null)
            return;

        float p = Mathf.Max(0.01f, pitch);

        dialogTypingSource.Stop();
        dialogTypingSource.clip = clip;
        dialogTypingSource.pitch = p;

        // IMPORTANT: volume = inspector, on n'y touche pas.
        dialogTypingSource.loop = true;
        dialogTypingSource.Play();
    }

    public void StopDialogTypingLoop()
    {
        if (dialogTypingSource == null)
            return;

        if (dialogTypingSource.isPlaying)
            dialogTypingSource.Stop();

        dialogTypingSource.clip = null;
    }

    // --------------------------------------------------------------------
    // Pause control
    // --------------------------------------------------------------------

    public void SetGameplayPaused(bool paused)
    {
        if (sfxSource != null)
        {
            if (paused) sfxSource.Pause();
            else sfxSource.UnPause();
        }

        if (!dialogTypingUsesUiBus && dialogTypingSource != null)
        {
            if (paused) dialogTypingSource.Pause();
            else dialogTypingSource.UnPause();
        }
    }

    public void SetUiPaused(bool paused)
    {
        if (uiSource != null)
        {
            if (paused) uiSource.Pause();
            else uiSource.UnPause();
        }

        if (dialogTypingUsesUiBus && dialogTypingSource != null)
        {
            if (paused) dialogTypingSource.Pause();
            else dialogTypingSource.UnPause();
        }
    }

    public void SetPaused(bool paused)
    {
        SetGameplayPaused(paused);
        SetUiPaused(paused);
        SetMusicPaused(paused);
    }

    public void StopAll()
    {
        if (sfxSource != null) sfxSource.Stop();
        if (uiSource != null) uiSource.Stop();
        if (dialogTypingSource != null) dialogTypingSource.Stop();

        ForceStopMusicSources();

        if (dialogTypingSource != null)
            dialogTypingSource.clip = null;
    }

    // --------------------------------------------------------------------
    // Core SFX
    // --------------------------------------------------------------------

    private void PlayInternal(SfxId id, bool isUi, float? pitchOverride, float volumeMult)
    {
        if (id == SfxId.None)
            return;

        AudioSource source = isUi ? uiSource : sfxSource;
        if (source == null)
        {
            Debug.LogWarning("[AudioManager] AudioSource manquante. isUi=" + isUi);
            return;
        }

        if (!map.TryGetValue(id, out SfxEntry entry) || entry == null)
        {
            Debug.LogWarning("[AudioManager] SfxId non configure: " + id);
            return;
        }

        if (entry.clip == null)
        {
            Debug.LogWarning("[AudioManager] Clip manquant pour: " + id);
            return;
        }

        float basePitch = pitchOverride.HasValue ? pitchOverride.Value : entry.pitch;

        float finalPitch = basePitch;
        if (entry.usePitchJitter && entry.pitchJitter > 0f)
        {
            finalPitch = basePitch + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter);
            if (finalPitch < 0.01f)
                finalPitch = 0.01f;
        }

        float finalVolume = Mathf.Clamp01(entry.volume * Mathf.Max(0f, volumeMult));

        source.pitch = finalPitch;
        source.PlayOneShot(entry.clip, finalVolume);
    }

    // --------------------------------------------------------------------
    // Core Music internals (CROSSFADE + DUCKING)
    // --------------------------------------------------------------------

    private void StartMusicTransition(MusicEntry next, MusicId nextId, float fadeOutSec, float fadeInSec)
    {
        if (musicFadeCo != null)
        {
            StopCoroutine(musicFadeCo);
            musicFadeCo = null;
        }

        musicFadeCo = StartCoroutine(MusicCrossfadeCo(next, nextId, fadeOutSec, fadeInSec));
    }

    private IEnumerator MusicCrossfadeCo(MusicEntry next, MusicId nextId, float fadeOutSec, float fadeInSec)
    {
        if (activeMusicSource == null || inactiveMusicSource == null)
        {
            Debug.LogWarning("[AudioManager] Sources musique non assignees (A/B).");
            yield break;
        }

        float outDur = Mathf.Max(0f, fadeOutSec);
        float inDur = Mathf.Max(0.0001f, fadeInSec);

        // Prepare la source inactive avec le nouveau clip.
        inactiveMusicSource.Stop();
        inactiveMusicSource.clip = next.clip;
        inactiveMusicSource.loop = next.loop;

        // Base volumes : on part de 0 sur la nouvelle source.
        inactiveBaseVolume = 0f;

        inactiveMusicSource.Play();

        // On considere que "la musique courante" a change des qu'on lance la transition.
        currentMusicId = nextId;

        // Base volume depart de l'ancienne.
        float startBaseA = activeBaseVolume;

        // Base volume cible de la nouvelle (sans multiplicateur).
        float targetBaseB = Mathf.Clamp01(next.volume);

        // Duree globale du crossfade : on prend le max pour que les 2 courbes aient le temps.
        float dur = Mathf.Max(outDur, inDur);

        // Cas rare : dur=0 => switch instant
        if (dur <= 0f)
        {
            // Ancienne coupe
            activeMusicSource.Stop();
            activeMusicSource.clip = null;
            activeBaseVolume = 0f;

            // Nouvelle full
            inactiveBaseVolume = targetBaseB;

            SwapMusicSources();
            ApplyMusicVolumes();

            musicFadeCo = null;
            yield break;
        }

        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;

            // A descend sur outDur.
            if (outDur <= 0f)
                activeBaseVolume = 0f;
            else
                activeBaseVolume = Mathf.Lerp(startBaseA, 0f, Mathf.Clamp01(t / outDur));

            // B monte sur inDur.
            inactiveBaseVolume = Mathf.Lerp(0f, targetBaseB, Mathf.Clamp01(t / inDur));

            // Applique base * multiplier sur les 2 sources.
            ApplyMusicVolumes();

            yield return null;
        }

        // Finalise.
        activeBaseVolume = 0f;
        inactiveBaseVolume = targetBaseB;

        // Stop l'ancienne source.
        activeMusicSource.Stop();
        activeMusicSource.clip = null;

        // La nouvelle devient active.
        SwapMusicSources();

        // Applique le volume final.
        ApplyMusicVolumes();

        musicFadeCo = null;
    }

    private IEnumerator FadeOutAndStopMusicCo(float fadeOutSec)
    {
        float outDur = Mathf.Max(0f, fadeOutSec);

        float startBaseA = activeBaseVolume;
        float startBaseB = inactiveBaseVolume;

        if (outDur > 0f)
        {
            float t = 0f;
            while (t < outDur)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / outDur);

                activeBaseVolume = Mathf.Lerp(startBaseA, 0f, a);
                inactiveBaseVolume = Mathf.Lerp(startBaseB, 0f, a);

                ApplyMusicVolumes();

                yield return null;
            }
        }

        ForceStopMusicSources();
    }

    private void ApplyMusicVolumes()
    {
        float m = Mathf.Clamp01(musicVolumeMultiplier);

        if (activeMusicSource != null)
            activeMusicSource.volume = Mathf.Clamp01(activeBaseVolume * m);

        if (inactiveMusicSource != null)
            inactiveMusicSource.volume = Mathf.Clamp01(inactiveBaseVolume * m);
    }

    private void SwapMusicSources()
    {
        AudioSource tmp = activeMusicSource;
        activeMusicSource = inactiveMusicSource;
        inactiveMusicSource = tmp;

        float tmpV = activeBaseVolume;
        activeBaseVolume = inactiveBaseVolume;
        inactiveBaseVolume = tmpV;
    }

    private void ForceStopMusicSources()
    {
        if (musicFadeCo != null)
        {
            StopCoroutine(musicFadeCo);
            musicFadeCo = null;
        }

        if (musicMultiplierCo != null)
        {
            StopCoroutine(musicMultiplierCo);
            musicMultiplierCo = null;
        }

        if (musicSourceA != null)
        {
            musicSourceA.Stop();
            musicSourceA.clip = null;
            musicSourceA.volume = 0f;
        }

        if (musicSourceB != null)
        {
            musicSourceB.Stop();
            musicSourceB.clip = null;
            musicSourceB.volume = 0f;
        }

        currentMusicId = MusicId.None;

        activeBaseVolume = 0f;
        inactiveBaseVolume = 0f;

        // On remet le ducking a la valeur de base "safe".
        musicVolumeMultiplier = 1f;
    }

    // --------------------------------------------------------------------
    // Validation
    // --------------------------------------------------------------------

    private void ValidateSources()
    {
        if (sfxSource == null)
            Debug.LogWarning("[AudioManager] sfxSource non assigne.");

        if (uiSource == null)
            Debug.LogWarning("[AudioManager] uiSource non assigne.");

        if (dialogTypingSource == null)
            Debug.LogWarning("[AudioManager] dialogTypingSource non assigne (typing dialogue).");

        if (musicSourceA == null)
            Debug.LogWarning("[AudioManager] musicSourceA non assignee.");

        if (musicSourceB == null)
            Debug.LogWarning("[AudioManager] musicSourceB non assignee.");
    }
}