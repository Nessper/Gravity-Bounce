using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Exécute une séquence de dialogues (DialogLine[]) avec la même UI et les mêmes timings
/// partout dans le jeu (intro, phases, post-level, events, etc.).
///
/// Responsabilités :
/// - Reçoit un tableau de DialogLine (préparé ailleurs, par un DialogManager par exemple).
/// - Résout les personnages via CrewDatabase.
/// - Affiche chaque ligne avec IntroDialogUI, dans l'ordre.
/// - Applique les timings (delay initial, entre lignes, après la dernière).
/// - Notifie le code appelant via callback quand la séquence est terminée.
/// - Peut être stoppé brutalement (skip, changement d'état) via StopAndHide().
///
/// Ne s'occupe PAS de :
/// - choisir QUELLE séquence charger (intro, phase, post-level) : c'est le rôle du contrôleur appelant.
/// - gérer les contrôles, le flash, le HUD, etc.
/// </summary>
public class DialogSequenceRunner : MonoBehaviour
{
    // ============================
    // REFS
    // ============================
    [Header("Références")]
    [SerializeField] private CrewDatabase crewDatabase;
    [SerializeField] private DialogUI dialogUI;

    // ============================
    // CONFIGURATION
    // ============================
    [Header("Timings")]
    [Tooltip("Délai avant d'afficher la première ligne.")]
    [SerializeField] private float initialDelay = 0.5f;

    [Tooltip("Délai entre chaque ligne.")]
    [SerializeField] private float delayBetweenLines = 0.3f;

    [Tooltip("Délai après la dernière ligne avant de cacher la boîte de dialogue.")]
    [SerializeField] private float endHoldDelay = 0.8f;

    // ============================
    // STATE
    // ============================
    private Coroutine currentRoutine;



    // ============================
    // PUBLIC API
    // ============================

    /// <summary>
    /// Lance l'exécution d'une séquence de dialogues.
    /// Le tableau de lignes doit être fourni par un autre système (DialogManager, etc.).
    /// </summary>
    /// <param name="lines">Lignes de dialogue à jouer dans l'ordre.</param>
    /// <param name="onComplete">Callback appelé à la fin de la séquence (ou immédiatement si rien à jouer).</param>
    public void Play(DialogLine[] lines, Action onComplete)
    {
        // Si une séquence est déjà en cours, on la stoppe proprement
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        // Si aucune ligne, on nettoie l'UI et on termine immédiatement
        if (lines == null || lines.Length == 0)
        {
            if (dialogUI != null)
                dialogUI.Hide();

            onComplete?.Invoke();
            return;
        }

        currentRoutine = StartCoroutine(PlayRoutine(lines, onComplete));
    }

    /// <summary>
    /// Stoppe immédiatement la séquence de dialogue en cours (si présente)
    /// et cache la boîte de dialogue.
    /// À utiliser pour les cas de Skip ou de changement brutal d'état.
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



    // ============================
    // INTERNAL COROUTINE
    // ============================
    private IEnumerator PlayRoutine(DialogLine[] lines, Action onComplete)
    {
        // Délai initial avant la première ligne
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        for (int i = 0; i < lines.Length; i++)
        {
            DialogLine line = lines[i];

            // Résolution du personnage à partir de l'ID
            CrewCharacter character = null;
            if (crewDatabase != null && !string.IsNullOrEmpty(line.speakerId))
            {
                character = crewDatabase.GetCharacter(line.speakerId);
            }

            if (dialogUI != null)
            {
                // IntroDialogUI gère l'affichage complet de la ligne (typewriter, glitch, etc.)
                yield return StartCoroutine(dialogUI.PlayLine(character, line.text));

                // Délai entre les lignes (sauf après la dernière)
                if (i < lines.Length - 1 && delayBetweenLines > 0f)
                    yield return new WaitForSeconds(delayBetweenLines);
            }
            else
            {
                // Fallback console si jamais l'UI n'est pas branchée
                Debug.Log("[DialogSequenceRunner] [" + line.speakerId + "] " + line.text);

                if (i < lines.Length - 1 && delayBetweenLines > 0f)
                    yield return new WaitForSeconds(delayBetweenLines);
            }
        }

        // Délai après la dernière ligne
        if (endHoldDelay > 0f)
            yield return new WaitForSeconds(endHoldDelay);

        // On cache la UI à la fin
        if (dialogUI != null)
            dialogUI.Hide();

        currentRoutine = null;
        onComplete?.Invoke();
    }
}
