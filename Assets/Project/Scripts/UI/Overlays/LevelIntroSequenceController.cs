using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gère la séquence d'intro de niveau :
/// - lock des controles
/// - dialogues d'intro (si disponibles)
/// - compte a rebours "3-2-1"
/// - unlock des controles, puis callback onComplete (LevelManager.StartLevel)
/// </summary>
public class LevelIntroSequenceController : MonoBehaviour
{
    [Header("Core refs")]
    [SerializeField] private LevelControlsController controlsController;
    [SerializeField] private CountdownUI countdownUI;

    [Header("Narration")]
    [SerializeField] private CrewDatabase crewDatabase;
    [SerializeField] private IntroDialogUI introDialogUI;

    [Header("Dialog config")]
    [SerializeField] private int worldIndex = 1;
    [SerializeField] private int levelIndex = 1;
    [SerializeField] private float initialIntroDelay = 0.5f;
    [SerializeField] private float delayBetweenLines = 0.3f;
    [SerializeField] private float introDialogEndHold = 0.8f;

    /// <summary>
    /// Lance la sequence d'intro complete, puis appelle onComplete.
    /// </summary>
    public void Play(Action onComplete)
    {
        StartCoroutine(PlayRoutine(onComplete));
    }

    private IEnumerator PlayRoutine(Action onComplete)
    {
        // 1) Lock des controles pendant l'intro
        if (controlsController != null)
            controlsController.DisableGameplayControls();

        // 2) Dialogues d'intro (si defini)
        yield return StartCoroutine(PlayIntroDialogSequence());

        // 3) Compte a rebours "3-2-1" si disponible
        if (countdownUI != null)
        {
            yield return StartCoroutine(countdownUI.PlayCountdown(null));
        }

        // 4) Unlock des controles et signal "go"
        if (controlsController != null)
            controlsController.EnableGameplayControls();

        onComplete?.Invoke();
    }

    /// <summary>
    /// Lit la sequence de dialogues d'intro (si presente).
    /// </summary>
    private IEnumerator PlayIntroDialogSequence()
    {
        // 1) Recuperer le DialogManager
        DialogManager dialogManager = FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            yield break;

        // 2) Attendre que la base soit prete
        while (!dialogManager.IsReady)
            yield return null;

        // 3) Recuperer la sequence d'intro pour ce world/level
        DialogSequence sequence = dialogManager.GetIntroSequence(worldIndex, levelIndex);
        if (sequence == null)
            yield break;

        DialogLine[] lines = dialogManager.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            yield break;

        // 4) Petite pause avant la premiere ligne
        if (initialIntroDelay > 0f)
            yield return new WaitForSeconds(initialIntroDelay);

        // 5) Boucle sur les lignes
        for (int i = 0; i < lines.Length; i++)
        {
            DialogLine line = lines[i];

            CrewCharacter character = null;
            if (crewDatabase != null && !string.IsNullOrEmpty(line.speakerId))
            {
                character = crewDatabase.GetCharacter(line.speakerId);
            }

            if (introDialogUI != null)
            {
                // Effet typewriter + pauses internes
                yield return StartCoroutine(introDialogUI.PlayLine(character, line.text));

                // Pause globale entre les lignes (sauf apres la derniere)
                if (i < lines.Length - 1 && delayBetweenLines > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenLines);
                }
            }
            else
            {
                // Fallback console si l'UI n'est pas branchee
                string speakerLabel = character != null ? character.displayName : line.speakerId;
                Debug.Log("[IntroDialog] [" + speakerLabel + "] " + line.text);

                if (i < lines.Length - 1 && delayBetweenLines > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenLines);
                }
            }
        }

        // 6) Hold final
        if (introDialogEndHold > 0f)
        {
            yield return new WaitForSeconds(introDialogEndHold);
        }

        // 7) Fin de sequence : cacher l'UI si presente
        if (introDialogUI != null)
        {
            introDialogUI.Hide();
        }
    }
}
