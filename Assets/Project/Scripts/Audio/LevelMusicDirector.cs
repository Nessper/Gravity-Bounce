using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LevelMusicDirector (Main only)
/// - Choisit un couple (Briefing + Gameplay) dans Resources/Playlists/<playlistName>/
/// - Joue la musique de Briefing au moment où le briefing s’affiche
/// - Joue la musique Gameplay au moment où le gameplay démarre réellement
///
/// Convention simple (V1) :
/// - Le clip briefing contient "-Briefing"
/// - Le clip gameplay contient "-Gameplay"
/// Exemple :
///   StellarAscent-Briefing
///   StellarAscent-Gameplay
///   StellarAscentVariant-Briefing
///   StellarAscentVariant-Gameplay
/// </summary>
public class LevelMusicDirector : MonoBehaviour
{
    [Header("Playlist (Resources)")]
    [Tooltip("Nom du dossier dans Resources/Playlists/<name>/  (ex: 'Mike')")]
    [SerializeField] private string playlistName = "Mike";

    [Header("Clip Naming")]
    [SerializeField] private string briefingSuffix = "-Briefing";
    [SerializeField] private string gameplaySuffix = "-Gameplay";

    [Header("Playback")]
    [Range(0f, 1f)]
    [SerializeField] private float briefingBaseVolume = 0.6f;

    [Range(0f, 1f)]
    [SerializeField] private float gameplayBaseVolume = 0.7f;

    [SerializeField] private float fadeOutSec = 0.8f;
    [SerializeField] private float fadeInSec = 0.8f;

    public AudioClip SelectedBriefingClip { get; private set; }
    public AudioClip SelectedGameplayClip { get; private set; }

    /// <summary>
    /// Appelé par LevelManager au boot de Main.
    /// Choisit un couple briefing/gameplay une fois pour ce niveau.
    /// </summary>
    public void SelectRandomPair()
    {
        SelectedBriefingClip = null;
        SelectedGameplayClip = null;

        string resPath = "Playlists/" + playlistName;
        AudioClip[] clips = Resources.LoadAll<AudioClip>(resPath);

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("[LevelMusicDirector] Aucun clip trouvé dans Resources/" + resPath);
            return;
        }

        // Index par “baseName” (nom sans suffix)
        Dictionary<string, AudioClip> briefingByBase = new Dictionary<string, AudioClip>();
        Dictionary<string, AudioClip> gameplayByBase = new Dictionary<string, AudioClip>();

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip c = clips[i];
            if (c == null) continue;

            string n = c.name;

            if (n.EndsWith(briefingSuffix))
            {
                string baseName = n.Substring(0, n.Length - briefingSuffix.Length);
                if (!briefingByBase.ContainsKey(baseName))
                    briefingByBase.Add(baseName, c);
            }
            else if (n.EndsWith(gameplaySuffix))
            {
                string baseName = n.Substring(0, n.Length - gameplaySuffix.Length);
                if (!gameplayByBase.ContainsKey(baseName))
                    gameplayByBase.Add(baseName, c);
            }
        }

        // Construire la liste des paires valides
        List<string> validBases = new List<string>();
        foreach (var kv in briefingByBase)
        {
            if (gameplayByBase.ContainsKey(kv.Key))
                validBases.Add(kv.Key);
        }

        if (validBases.Count == 0)
        {
            Debug.LogWarning("[LevelMusicDirector] Aucune paire valide trouvée. (Vérifie les suffixes -Briefing/-Gameplay)");
            return;
        }

        string pick = validBases[Random.Range(0, validBases.Count)];
        SelectedBriefingClip = briefingByBase[pick];
        SelectedGameplayClip = gameplayByBase[pick];

        Debug.Log("[LevelMusicDirector] Pair selected: " + pick
            + " | Briefing=" + SelectedBriefingClip.name
            + " | Gameplay=" + SelectedGameplayClip.name);
    }

    public void PlayBriefingMusic()
    {
        if (SelectedBriefingClip == null)
        {
            Debug.LogWarning("[LevelMusicDirector] PlayBriefingMusic: no selected briefing clip.");
            return;
        }

        AudioManager.Instance?.PlayMusicClip(
            SelectedBriefingClip,
            baseVolume: briefingBaseVolume,
            loop: true,
            fadeOutSec: fadeOutSec,
            fadeInSec: fadeInSec
        );
    }

    public void PlayGameplayMusic()
    {
        if (SelectedGameplayClip == null)
        {
            Debug.LogWarning("[LevelMusicDirector] PlayGameplayMusic: no selected gameplay clip.");
            return;
        }

        AudioManager.Instance?.PlayMusicClip(
            SelectedGameplayClip,
            baseVolume: gameplayBaseVolume,
            loop: true,
            fadeOutSec: fadeOutSec,
            fadeInSec: fadeInSec
        );
    }
}