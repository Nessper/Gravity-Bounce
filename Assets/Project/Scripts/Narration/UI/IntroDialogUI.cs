using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI simple pour afficher une ligne de dialogue avec portrait + nom + texte.
/// Utilisée pour l'intro du niveau (dialogues avant le countdown).
/// Version TextMeshPro avec effet "machine a ecrire" et slide-in du panel.
/// </summary>
public class IntroDialogUI : MonoBehaviour
{
    [Header("References UI")]
    [Tooltip("Racine visuelle du dialogue (panel, group, etc.). Sera activée/désactivée.")]
    [SerializeField] private GameObject root;

    [Tooltip("RectTransform du panel racine (pour le slide).")]
    [SerializeField] private RectTransform rootRect;

    [Tooltip("CanvasGroup du panel (pour le fade-in). Optionnel.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Image du portrait du personnage.")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Nom du personnage.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Texte du dialogue.")]
    [SerializeField] private TMP_Text dialogText;

    [Header("Audio")]
    [SerializeField] private AudioSource dialogAudioSource;

    [Header("Typewriter")]
    [Tooltip("Délai avant de commencer à écrire le texte (après affichage du portrait/nom).")]
    [SerializeField] private float preTypeDelay = 0.25f;

    [Tooltip("Vitesse d'affichage du texte (caractères par seconde).")]
    [SerializeField] private float charsPerSecond = 40f;

    [Tooltip("Temps de pause après la fin de la ligne (en secondes).")]
    [SerializeField] private float holdAfterFinish = 0.6f;

    [Header("Slide-In")]
    [Tooltip("Activer l'animation de slide-in du panel à la première apparition.")]
    [SerializeField] private bool useSlideIn = true;

    [Tooltip("Distance verticale du slide (en pixels UI). Positive = le panel vient du bas.")]
    [SerializeField] private float slideDistance = 40f;

    [Tooltip("Durée du slide-in (en secondes).")]
    [SerializeField] private float slideDuration = 0.25f;

    [Header("Speaker Glitch")]
    [SerializeField] private bool useSpeakerGlitch = true;

    [Tooltip("Durée du glitch en secondes.")]
    [SerializeField] private float glitchDuration = 0.18f;

    [Tooltip("Amplitude max du jitter en pixels.")]
    [SerializeField] private float glitchPositionJitter = 4f;

    [Tooltip("Opacité minimale pendant le flicker.")]
    [SerializeField] private float glitchMinAlpha = 0.35f;

    [Tooltip("RectTransform du portrait (image).")]
    [SerializeField] private RectTransform portraitRect;

    [Tooltip("RectTransform du bloc de nom.")]
    [SerializeField] private RectTransform nameRect;

    [Header("Glitch Audio")]
    [SerializeField] private AudioSource glitchSource;
    [SerializeField] private AudioClip glitchClip;


    private Coroutine glitchRoutine;


    private bool hasShownOnce = false;
    private Vector2 initialAnchoredPos;
    private Coroutine slideCoroutine;

    private void Awake()
    {
        if (rootRect == null && root != null)
        {
            rootRect = root.GetComponent<RectTransform>();
        }

        if (canvasGroup == null && root != null)
        {
            canvasGroup = root.GetComponent<CanvasGroup>();
        }

        if (rootRect != null)
        {
            initialAnchoredPos = rootRect.anchoredPosition;
        }

        // On laisse le GameObject actif mais invisible
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }


    /// <summary>
    /// Affiche une ligne de dialogue instantanément (sans effet).
    /// Peut servir ailleurs si besoin.
    /// </summary>
    public void ShowLine(CrewCharacter character, string text)
    {
        EnsureRootVisibleWithSlideIfNeeded();

        if (dialogText != null)
        {
            dialogText.text = text ?? string.Empty;
            dialogText.maxVisibleCharacters = dialogText.text.Length;
        }

        if (character != null)
        {
            if (nameText != null)
                nameText.text = character.displayName;

            if (portraitImage != null)
                portraitImage.sprite = character.portrait;

            if (portraitImage != null)
                portraitImage.color = character.uiColor;
        }
        else
        {
            if (nameText != null)
                nameText.text = string.Empty;
        }
    }

    /// <summary>
    /// Joue une ligne avec effet machine a ecrire, puis une courte pause.
    /// LevelManager peut faire "yield return PlayLine(...)" pour synchroniser.
    /// </summary>
    public IEnumerator PlayLine(CrewCharacter character, string text)
    {
        EnsureRootVisibleWithSlideIfNeeded();

        // 1) Mettre à jour portrait + nom
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
        }

        if (dialogText == null)
            yield break;

        // 2) Glitch visuel (SANS son)
        if (useSpeakerGlitch)
        {
            if (glitchRoutine != null)
                StopCoroutine(glitchRoutine);

            glitchRoutine = StartCoroutine(PlaySpeakerGlitch());
        }

        // 3) Effacer le texte précédent
        dialogText.text = string.Empty;
        dialogText.maxVisibleCharacters = 0;

        // 4) Pause avant de commencer à écrire (silencieuse)
        if (preTypeDelay > 0f)
            yield return new WaitForSeconds(preTypeDelay);

        // 5) Préparer la nouvelle ligne
        string fullText = text ?? string.Empty;
        dialogText.text = fullText;
        dialogText.ForceMeshUpdate();

        int totalChars = dialogText.textInfo.characterCount;
        dialogText.maxVisibleCharacters = 0;

        if (totalChars == 0)
            yield break;

        float visibleCountFloat = 0f;

        // 6) Démarrer le son de "typing" en boucle pendant l'affichage
        if (dialogAudioSource != null && character != null && character.dialogClip != null)
        {
            // Au cas où un ancien son serait encore en cours
            if (dialogAudioSource.isPlaying)
                dialogAudioSource.Stop();

            dialogAudioSource.clip = character.dialogClip;
            dialogAudioSource.pitch = character.pitch;
            dialogAudioSource.volume = character.volume;
            dialogAudioSource.loop = true;
            dialogAudioSource.Play();
        }

        // 7) Typewriter
        while (dialogText.maxVisibleCharacters < totalChars)
        {
            visibleCountFloat += Time.deltaTime * Mathf.Max(1f, charsPerSecond);
            int newVisible = Mathf.FloorToInt(visibleCountFloat);

            if (newVisible != dialogText.maxVisibleCharacters)
            {
                dialogText.maxVisibleCharacters = Mathf.Clamp(newVisible, 0, totalChars);
            }

            yield return null;
        }

        dialogText.maxVisibleCharacters = totalChars;

        // 8) Stopper le son de typing une fois la ligne entièrement affichée
        if (dialogAudioSource != null && dialogAudioSource.isPlaying)
        {
            dialogAudioSource.Stop();
        }

        // 9) Pause finale post-ligne (silencieuse)
        if (holdAfterFinish > 0f)
            yield return new WaitForSeconds(holdAfterFinish);
    }




    /// <summary>
    /// Cache complètement l'UI.
    /// </summary>
    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        // On NE désactive pas root, pour éviter les problèmes de coroutine
    }


    /// <summary>
    /// S'assure que le root est actif, et lance un slide-in
    /// subtil à la première apparition.
    /// </summary>
    private void EnsureRootVisibleWithSlideIfNeeded()
    {
        if (root == null)
            return;

        // On s'assure que le GO est actif dans la hierarchie (a faire dans la scene)
        // Ici on ne fait PAS root.SetActive(false/true).

        if (useSlideIn && rootRect != null)
        {
            if (!hasShownOnce)
            {
                hasShownOnce = true;

                if (!isActiveAndEnabled)
                {
                    // Si le MonoBehaviour n'est pas actif, on abandonne l'anim
                    // pour éviter l'erreur StartCoroutine.
                    if (canvasGroup != null)
                        canvasGroup.alpha = 1f;

                    return;
                }

                if (slideCoroutine != null)
                    StopCoroutine(slideCoroutine);

                slideCoroutine = StartCoroutine(SlideInRoutine());
            }
            else
            {
                // Panel déjà montré au moins une fois : on s'assure qu'il est visible
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;
            }
        }
        else
        {
            // Pas de slide-in : on rend juste visible
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }
    }


    private IEnumerator SlideInRoutine()
    {
        if (rootRect == null)
            yield break;

        Vector2 startPos = initialAnchoredPos;
        startPos.y -= slideDistance;
        Vector2 endPos = initialAnchoredPos;

        float startAlpha = 0f;
        float endAlpha = 1f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = startAlpha;
        }

        rootRect.anchoredPosition = startPos;

        float t = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            rootRect.anchoredPosition = Vector2.Lerp(startPos, endPos, easedT);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, easedT);
            }

            yield return null;
        }

        rootRect.anchoredPosition = endPos;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = endAlpha;
        }
    }

    private IEnumerator PlaySpeakerGlitch()
    {
        if (!useSpeakerGlitch)
            yield break;

        // Auto-bind si non assigné dans l'inspector
        if (portraitRect == null && portraitImage != null)
            portraitRect = portraitImage.rectTransform;
        if (nameRect == null && nameText != null)
            nameRect = nameText.rectTransform;

        if (portraitRect == null || nameRect == null || portraitImage == null || nameText == null)
            yield break;

        Vector2 basePortraitPos = portraitRect.anchoredPosition;
        Vector2 baseNamePos = nameRect.anchoredPosition;

        Color basePortraitColor = portraitImage.color;
        Color baseNameColor = nameText.color;

        // Jouer le son de glitch une seule fois au lancement
        if (glitchSource != null && glitchClip != null)
        {
            glitchSource.pitch = 1f;    // tu pourras personnaliser par perso plus tard
            glitchSource.volume = 1f;   // ajuste dans l'inspector si besoin
            glitchSource.PlayOneShot(glitchClip);
        }

        float totalDuration = Mathf.Max(0.05f, glitchDuration);

        // Nombre de steps "tzzzt" pendant la durée
        int steps = 6;
        float stepDuration = totalDuration / steps;

        for (int i = 0; i < steps; i++)
        {
            // Jitter visible
            float dx = Random.Range(-glitchPositionJitter, glitchPositionJitter);
            float dy = Random.Range(-glitchPositionJitter, glitchPositionJitter);

            portraitRect.anchoredPosition = basePortraitPos + new Vector2(dx, dy);
            nameRect.anchoredPosition = baseNamePos + new Vector2(-dx * 0.4f, dy * 0.4f);

            // Flicker alpha: une frame sombre, une frame normale
            float alphaFactor = (i % 2 == 0) ? glitchMinAlpha : 1f;

            Color pc = basePortraitColor;
            Color nc = baseNameColor;
            pc.a = basePortraitColor.a * alphaFactor;
            nc.a = baseNameColor.a * alphaFactor;

            portraitImage.color = pc;
            nameText.color = nc;

            // Timing en temps reel pour respecter glitchDuration
            yield return new WaitForSecondsRealtime(stepDuration);
        }

        // Reset propre
        portraitRect.anchoredPosition = basePortraitPos;
        nameRect.anchoredPosition = baseNamePos;
        portraitImage.color = basePortraitColor;
        nameText.color = baseNameColor;
    }





}
