using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Execute une sequence de dialogues (DialogLine[]) avec la meme UI partout dans le jeu.
///
/// Cette classe ne choisit pas quelle sequence charger.
/// Elle recoit simplement des lignes deja preparees et les joue dans l'ordre.
///
/// Deux modes de lecture sont supportes :
/// - Auto : pour les dialogues pendant le gameplay (phases, evacuation, etc.)
/// - Interactive : pour intro, outro, tuto, ou tout autre moment ou le joueur doit cliquer
///
/// Responsabilites :
/// - Resoudre le speaker via CrewDatabase
/// - Jouer chaque ligne dans l'ordre
/// - Appliquer un delai initial avant la premiere ligne
/// - Notifier le code appelant a la fin
/// - Pouvoir stopper brutalement une sequence en cours
/// </summary>
public class DialogSequenceRunner : MonoBehaviour
{
    public enum PlaybackMode
    {
        Auto,
        Interactive
    }

    [Header("References")]
    [SerializeField] private CrewDatabase crewDatabase;
    [SerializeField] private DialogUI dialogUI;

    [Header("Timings")]
    [Tooltip("Delai avant d'afficher la premiere ligne.")]
    [SerializeField] private float initialDelay = 0.5f;

    // Coroutine de sequence actuellement en cours
    private Coroutine currentRoutine;

    /// <summary>
    /// API par defaut : lecture en mode Auto.
    /// Pratique pour les dialogues gameplay existants.
    /// </summary>
    public void Play(DialogLine[] lines, Action onComplete)
    {
        Play(lines, PlaybackMode.Auto, onComplete);
    }

    /// <summary>
    /// Lance la lecture d'une sequence complete avec le mode demande.
    /// </summary>
    public void Play(DialogLine[] lines, PlaybackMode mode, Action onComplete)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        if (lines == null || lines.Length == 0)
        {
            if (dialogUI != null)
                dialogUI.Hide();

            onComplete?.Invoke();
            return;
        }

        currentRoutine = StartCoroutine(PlayRoutine(lines, mode, onComplete));
    }

    /// <summary>
    /// Stoppe immediatement la sequence en cours et masque la UI.
    /// </summary>
    public void StopAndHide()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        if (dialogUI != null)
            dialogUI.Hide();
    }

    /// <summary>
    /// Coroutine interne de lecture de sequence.
    /// </summary>
    private IEnumerator PlayRoutine(DialogLine[] lines, PlaybackMode mode, Action onComplete)
    {
        if (initialDelay > 0f)
            yield return new WaitForSecondsRealtime(initialDelay);

        for (int i = 0; i < lines.Length; i++)
        {
            DialogLine line = lines[i];

            CrewCharacter character = null;
            if (crewDatabase != null && !string.IsNullOrEmpty(line.speakerId))
                character = crewDatabase.GetCharacter(line.speakerId);

            if (dialogUI != null)
            {
                if (mode == PlaybackMode.Interactive)
                    yield return StartCoroutine(dialogUI.PlayLineInteractive(character, line.text));
                else
                    yield return StartCoroutine(dialogUI.PlayLineAuto(character, line.text));
            }
            else
            {
                Debug.Log("[DialogSequenceRunner] [" + line.speakerId + "] " + line.text);
            }
        }

        if (dialogUI != null)
            dialogUI.Hide();

        currentRoutine = null;
        onComplete?.Invoke();
    }
}