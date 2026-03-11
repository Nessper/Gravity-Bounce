using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 40f;
    [SerializeField] private float holdAfterFinish = 0.5f;

    private bool isPlaying;

    public IEnumerator PlayLine(CrewCharacter character, string text)
    {
        EnsureVisible();

        // Sécurité : si une ligne précédente a été interrompue
        AudioManager.Instance?.StopDialogTypingLoop();

        isPlaying = true;
        yield return PlayRoutine(character, text);
        isPlaying = false;
    }

    public void StopImmediate()
    {
        // On ne stoppe pas de coroutine ici (DialogUI n’en lance plus).
        // On coupe juste le son + on cache.
        AudioManager.Instance?.StopDialogTypingLoop();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        isPlaying = false;
    }

    public void Hide()
    {
        StopImmediate();
    }

    private void EnsureVisible()
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private IEnumerator PlayRoutine(CrewCharacter character, string text)
    {
        if (character != null)
        {
            if (nameText != null) nameText.text = character.displayName;

            if (portraitImage != null)
            {
                portraitImage.sprite = character.portrait;
                portraitImage.color = character.uiColor;
            }
        }
        else
        {
            if (nameText != null) nameText.text = string.Empty;
        }

        if (dialogText == null)
            yield break;

        dialogText.text = text ?? string.Empty;
        dialogText.ForceMeshUpdate();

        int totalChars = dialogText.textInfo.characterCount;
        dialogText.maxVisibleCharacters = 0;

        AudioManager.Instance?.PlayUi(SfxId.DialogGlitch);


        // Typing loop par personnage
        if (character != null && character.dialogClip != null)
        {
            AudioManager.Instance?.StartDialogTypingLoop(character.dialogClip, character.pitch);
        }

        float visible = 0f;
        while (dialogText.maxVisibleCharacters < totalChars)
        {
            visible += Time.deltaTime * Mathf.Max(1f, charsPerSecond);
            dialogText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(visible));
            yield return null;
        }

        dialogText.maxVisibleCharacters = totalChars;

        AudioManager.Instance?.StopDialogTypingLoop();

        if (holdAfterFinish > 0f)
            yield return new WaitForSeconds(holdAfterFinish);
    }
}
