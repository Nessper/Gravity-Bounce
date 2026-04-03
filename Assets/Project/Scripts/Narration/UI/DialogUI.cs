using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI de dialogue reutilisable pour tout le jeu.
///
/// Deux modes sont supportes :
/// - Auto : la ligne se tape toute seule, attend un court delai, puis rend la main.
/// - Interactive :
///     1 clic pendant le typing -> affiche instantanement tout le texte et coupe le son,
///     1 clic quand le texte est deja visible -> passe a la ligne suivante.
///
/// Responsabilites :
/// - Afficher le portrait, le nom et le texte du personnage.
/// - Gerer l'effet typewriter.
/// - Gerer le son de typing.
/// - Afficher un petit indicateur visuel quand la ligne est terminee,
///   mais uniquement en mode interactif.
/// - Fournir deux APIs claires : auto et interactif.
///
/// Ne choisit PAS quelle sequence jouer :
/// c'est le role de DialogSequenceRunner.
/// </summary>
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
    [Tooltip("Petit indicateur visuel qui apparait quand la ligne est completement affichee.")]
    [SerializeField] private CanvasGroup continueHintCanvasGroup;

    [Tooltip("Vitesse du clignotement de l'indicateur.")]
    [SerializeField] private float continueHintBlinkSpeed = 2.5f;

    [Tooltip("Alpha minimal du clignotement.")]
    [SerializeField] private float continueHintMinAlpha = 0.25f;

    // Coroutine de typing en cours
    private Coroutine typingRoutine;

    // Coroutine de clignotement du hint "continuer"
    private Coroutine continueHintRoutine;

    // Etat courant de la ligne
    private bool isTyping;
    private bool isLineFullyVisible;
    private bool advanceRequested;

    // Texte complet de la ligne courante
    private string currentFullText = string.Empty;

    // Indique si l'on doit afficher le hint "continuer" pour la ligne courante.
    // True uniquement pour les lignes interactives.
    private bool showContinueHintForCurrentLine;

    /// <summary>
    /// Joue une ligne en mode interactif.
    ///
    /// Comportement :
    /// - clic pendant le typing : reveal instantane
    /// - clic apres reveal complet : on passe a la ligne suivante
    /// </summary>
    public IEnumerator PlayLineInteractive(CrewCharacter character, string text)
    {
        EnsureVisible();
        ResetLineState();
        showContinueHintForCurrentLine = true;
        SetupSpeaker(character);

        currentFullText = text ?? string.Empty;

        if (dialogText == null)
            yield break;

        PrepareTextForTyping(currentFullText);
        StartTypingAudio(character);

        typingRoutine = StartCoroutine(TypeLineRoutine());

        // Important :
        // on attend que tout clic / touche encore maintenu soit relache
        // avant d'autoriser la nouvelle ligne a consommer un input.
        // Cela evite qu'un meme clic serve a finir une ligne puis
        // a reveal instantanement la suivante.
        yield return WaitForAdvanceInputRelease();

        while (true)
        {
            if (WasAdvancePressed())
            {
                if (isTyping)
                {
                    // Premier clic pendant le typing :
                    // on revele instantanement la ligne courante.
                    RevealInstant();
                }
                else if (isLineFullyVisible)
                {
                    // Clic suivant :
                    // on valide la ligne et on passe a la suivante.
                    advanceRequested = true;
                }
            }

            if (advanceRequested)
                break;

            yield return null;
        }

        CleanupAfterLine();
    }

    /// <summary>
    /// Joue une ligne en mode automatique.
    /// Le texte se tape tout seul puis attend un petit delai avant de terminer.
    /// </summary>
    public IEnumerator PlayLineAuto(CrewCharacter character, string text)
    {
        EnsureVisible();
        ResetLineState();
        showContinueHintForCurrentLine = false;
        SetupSpeaker(character);

        currentFullText = text ?? string.Empty;

        if (dialogText == null)
            yield break;

        PrepareTextForTyping(currentFullText);
        StartTypingAudio(character);

        typingRoutine = StartCoroutine(TypeLineRoutine());

        // On attend la fin complete du typing
        while (typingRoutine != null)
            yield return null;

        // Petite pause de lisibilite avant de rendre la main
        if (holdAfterFinish > 0f)
            yield return new WaitForSeconds(holdAfterFinish);

        CleanupAfterLine();
    }

    /// <summary>
    /// Stoppe immediatement tout ce qui est en cours et masque la UI.
    /// A utiliser lors d'un changement brutal d'etat.
    /// </summary>
    public void StopImmediate()
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

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Cache la boite de dialogue.
    /// </summary>
    public void Hide()
    {
        StopImmediate();
    }

    /// <summary>
    /// Rend la UI visible.
    /// </summary>
    private void EnsureVisible()
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Reinitialise proprement l'etat de la ligne avant d'en jouer une nouvelle.
    /// </summary>
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

    /// <summary>
    /// Remplit les elements UI du speaker.
    /// </summary>
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

    /// <summary>
    /// Prepare le texte pour l'effet typewriter.
    /// </summary>
    private void PrepareTextForTyping(string fullText)
    {
        if (dialogText == null)
            return;

        dialogText.text = fullText;
        dialogText.ForceMeshUpdate();
        dialogText.maxVisibleCharacters = 0;
    }

    /// <summary>
    /// Demarre les sons lies a l'apparition de la ligne.
    /// </summary>
    private void StartTypingAudio(CrewCharacter character)
    {
        AudioManager.Instance?.StopDialogTypingLoop();
        AudioManager.Instance?.PlayUi(SfxId.DialogGlitch);

        if (character != null && character.dialogClip != null)
        {
            AudioManager.Instance?.StartDialogTypingLoop(character.dialogClip, character.pitch);
        }
    }

    /// <summary>
    /// Coroutine principale du typewriter.
    /// Revele progressivement les caracteres.
    /// </summary>
    private IEnumerator TypeLineRoutine()
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
            visible += Time.deltaTime * Mathf.Max(1f, charsPerSecond);
            dialogText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(visible));
            yield return null;
        }

        dialogText.maxVisibleCharacters = totalChars;

        isTyping = false;
        isLineFullyVisible = true;
        typingRoutine = null;

        AudioManager.Instance?.StopDialogTypingLoop();

        // La ligne est finie :
        // on affiche l'indicateur "continuer" uniquement pour les lignes interactives.
        if (showContinueHintForCurrentLine)
            ShowContinueHint();
    }

    /// <summary>
    /// Affiche instantanement tout le texte de la ligne courante.
    /// Coupe aussi le son de typing.
    /// </summary>
    private void RevealInstant()
    {
        if (dialogText == null)
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

        // Reveal instant = la ligne est consideree terminee.
        // Le hint n'apparait que si la ligne courante est interactive.
        if (showContinueHintForCurrentLine)
            ShowContinueHint();
    }

    /// <summary>
    /// Nettoyage de fin de ligne.
    /// </summary>
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

    /// <summary>
    /// Retourne vrai si le joueur a demande d'avancer.
    /// </summary>
    private bool WasAdvancePressed()
    {
        if (allowMouseClick && Input.GetMouseButtonDown(0))
            return true;

        if (allowSpaceKey && Input.GetKeyDown(KeyCode.Space))
            return true;

        return false;
    }

    /// <summary>
    /// Attend que les inputs d'avance soient relaches avant d'autoriser
    /// la nouvelle ligne interactive a ecouter de nouveaux clics.
    /// Evite qu'un meme clic serve a la fois a passer a la ligne suivante
    /// et a reveal instant la ligne suivante.
    /// </summary>
    private IEnumerator WaitForAdvanceInputRelease()
    {
        while (IsAdvanceHeld())
            yield return null;
    }

    /// <summary>
    /// Retourne vrai si un input d'avance est actuellement maintenu.
    /// </summary>
    private bool IsAdvanceHeld()
    {
        if (allowMouseClick && Input.GetMouseButton(0))
            return true;

        if (allowSpaceKey && Input.GetKey(KeyCode.Space))
            return true;

        return false;
    }

    /// <summary>
    /// Cache immediatement l'indicateur "continuer".
    /// </summary>
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

    /// <summary>
    /// Affiche et fait clignoter l'indicateur "continuer".
    /// </summary>
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

    /// <summary>
    /// Coroutine de clignotement doux pour l'indicateur "continuer".
    /// </summary>
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