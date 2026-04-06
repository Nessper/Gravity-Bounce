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

    [Header("Input")]
    [SerializeField] private bool allowMouseClick = true;
    [SerializeField] private bool allowSpaceKey = true;

    [Header("Continue Hint")]
    [SerializeField] private CanvasGroup continueHintCanvasGroup;
    [SerializeField] private float continueHintBlinkSpeed = 2.5f;
    [SerializeField] private float continueHintMinAlpha = 0.25f;

    private Coroutine typingRoutine;
    private Coroutine continueHintRoutine;

    private bool isTyping;
    private bool isLineFullyVisible;
    private bool advanceRequested;

    private string currentFullText = string.Empty;
    private bool showContinueHintForCurrentLine;

    private int lineToken = 0;

    public IEnumerator PlayLineInteractive(CrewCharacter character, string text)
    {
        int myToken = ++lineToken;

        EnsureVisible();
        ResetLineState();
        showContinueHintForCurrentLine = true;
        SetupSpeaker(character);

        currentFullText = text ?? string.Empty;

        if (dialogText == null)
            yield break;

        PrepareTextForTyping(currentFullText);
        StartTypingAudio(character);

        typingRoutine = StartCoroutine(TypeLineRoutine(myToken));

        yield return WaitForAdvanceInputRelease();

        int unlockFrame = Time.frameCount + 1;

        while (true)
        {
            if (myToken != lineToken)
                yield break;

            if (Time.frameCount >= unlockFrame && WasAdvancePressed())
            {
                if (isTyping)
                {
                    RevealInstant(myToken);
                }
                else if (isLineFullyVisible)
                {
                    advanceRequested = true;
                }
            }

            if (advanceRequested)
                break;

            yield return null;
        }

        if (myToken != lineToken)
            yield break;

        CleanupAfterLine();
    }

    public IEnumerator PlayLineAuto(CrewCharacter character, string text)
    {
        int myToken = ++lineToken;

        EnsureVisible();
        ResetLineState();
        showContinueHintForCurrentLine = false;
        SetupSpeaker(character);

        currentFullText = text ?? string.Empty;

        if (dialogText == null)
            yield break;

        PrepareTextForTyping(currentFullText);
        StartTypingAudio(character);

        typingRoutine = StartCoroutine(TypeLineRoutine(myToken));

        while (typingRoutine != null)
        {
            if (myToken != lineToken)
                yield break;

            yield return null;
        }

        if (holdAfterFinish > 0f)
        {
            float t = 0f;
            while (t < holdAfterFinish)
            {
                if (myToken != lineToken)
                    yield break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (myToken != lineToken)
            yield break;

        CleanupAfterLine();
    }

    public void StopImmediate()
    {
        lineToken++;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        HideContinueHintInstant();

        AudioManager.Instance?.StopDialogTypingLoop();

        isTyping = false;
        isLineFullyVisible = false;
        advanceRequested = false;
        currentFullText = string.Empty;
        showContinueHintForCurrentLine = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
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

    private void ResetLineState()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        HideContinueHintInstant();

        AudioManager.Instance?.StopDialogTypingLoop();

        isTyping = false;
        isLineFullyVisible = false;
        advanceRequested = false;
        currentFullText = string.Empty;
        showContinueHintForCurrentLine = false;
    }

    private void SetupSpeaker(CrewCharacter character)
    {
        if (character != null)
        {
            if (nameText != null)
                nameText.text = character.displayName;

            if (portraitImage != null)
            {
                portraitImage.sprite = character.portrait;
                portraitImage.color = character.uiColor;
            }
        }
        else
        {
            if (nameText != null)
                nameText.text = string.Empty;

            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.color = Color.white;
            }
        }
    }

    private void PrepareTextForTyping(string fullText)
    {
        if (dialogText == null)
            return;

        dialogText.text = fullText;
        dialogText.ForceMeshUpdate();
        dialogText.maxVisibleCharacters = 0;
    }

    private void StartTypingAudio(CrewCharacter character)
    {
        AudioManager.Instance?.StopDialogTypingLoop();
        AudioManager.Instance?.PlayUi(SfxId.DialogGlitch);

        if (character != null && character.dialogClip != null)
            AudioManager.Instance?.StartDialogTypingLoop(character.dialogClip, character.pitch);
    }

    private IEnumerator TypeLineRoutine(int myToken)
    {
        if (dialogText == null)
            yield break;

        dialogText.ForceMeshUpdate();
        int totalChars = dialogText.textInfo.characterCount;

        isTyping = true;
        isLineFullyVisible = false;

        float visible = 0f;

        while (dialogText.maxVisibleCharacters < totalChars)
        {
            if (myToken != lineToken)
                yield break;

            visible += Time.unscaledDeltaTime * Mathf.Max(1f, charsPerSecond);
            dialogText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(visible));
            yield return null;
        }

        if (myToken != lineToken)
            yield break;

        dialogText.maxVisibleCharacters = totalChars;

        isTyping = false;
        isLineFullyVisible = true;
        typingRoutine = null;

        AudioManager.Instance?.StopDialogTypingLoop();

        if (showContinueHintForCurrentLine)
            ShowContinueHint();
    }

    private void RevealInstant(int myToken)
    {
        if (myToken != lineToken || dialogText == null)
            return;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        dialogText.text = currentFullText;
        dialogText.ForceMeshUpdate();
        dialogText.maxVisibleCharacters = dialogText.textInfo.characterCount;

        isTyping = false;
        isLineFullyVisible = true;

        AudioManager.Instance?.StopDialogTypingLoop();

        if (showContinueHintForCurrentLine)
            ShowContinueHint();
    }

    private void CleanupAfterLine()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        HideContinueHintInstant();

        AudioManager.Instance?.StopDialogTypingLoop();

        isTyping = false;
        isLineFullyVisible = false;
        advanceRequested = false;
        currentFullText = string.Empty;
        showContinueHintForCurrentLine = false;
    }

    private bool WasAdvancePressed()
    {
        if (allowMouseClick && Input.GetMouseButtonDown(0))
            return true;

        if (allowSpaceKey && Input.GetKeyDown(KeyCode.Space))
            return true;

        return false;
    }

    private IEnumerator WaitForAdvanceInputRelease()
    {
        while (IsAdvanceHeld())
            yield return null;
    }

    private bool IsAdvanceHeld()
    {
        if (allowMouseClick && Input.GetMouseButton(0))
            return true;

        if (allowSpaceKey && Input.GetKey(KeyCode.Space))
            return true;

        return false;
    }

    private void HideContinueHintInstant()
    {
        if (continueHintRoutine != null)
        {
            StopCoroutine(continueHintRoutine);
            continueHintRoutine = null;
        }

        if (continueHintCanvasGroup == null)
            return;

        continueHintCanvasGroup.alpha = 0f;
        continueHintCanvasGroup.interactable = false;
        continueHintCanvasGroup.blocksRaycasts = false;
    }

    private void ShowContinueHint()
    {
        if (continueHintCanvasGroup == null)
            return;

        if (continueHintRoutine != null)
        {
            StopCoroutine(continueHintRoutine);
            continueHintRoutine = null;
        }

        continueHintCanvasGroup.interactable = false;
        continueHintCanvasGroup.blocksRaycasts = false;
        continueHintRoutine = StartCoroutine(CoBlinkContinueHint());
    }

    private IEnumerator CoBlinkContinueHint()
    {
        if (continueHintCanvasGroup == null)
            yield break;

        float speed = Mathf.Max(0.1f, continueHintBlinkSpeed);
        float minAlpha = Mathf.Clamp01(continueHintMinAlpha);

        while (true)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f);
            continueHintCanvasGroup.alpha = Mathf.Lerp(minAlpha, 1f, wave);
            yield return null;
        }
    }
}