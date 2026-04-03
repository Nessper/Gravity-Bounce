using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Overlay reutilisable de "hold to skip".
///
/// Regles de fonctionnement :
/// - un seul owner a la fois
/// - seul l owner courant peut le cacher via Hide(owner)
/// - Show(owner, ...) remplace explicitement l owner courant
/// - ForceHideImmediate() est reserve aux resets internes / globaux
///
/// Objectif :
/// permettre a plusieurs controllers d utiliser le meme overlay partage
/// a differents moments de la scene sans conflits.
/// </summary>
public class HoldToSkipOverlayUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Indicator")]
    [SerializeField] private RectTransform indicator;
    [SerializeField] private CanvasGroup indicatorCanvasGroup;
    [SerializeField] private Image radialFill;

    [Header("Label")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private string defaultLabelText = "HOLD TO SKIP";

    [Header("Timing")]
    [SerializeField] private float holdDuration = 0.65f;
    [SerializeField] private float resetSpeed = 3f;

    [Header("Text Feedback")]
    [SerializeField] private float textScaleActive = 1.10f;
    [SerializeField] private float textScaleLerpSpeed = 10f;
    [SerializeField] private float labelInactiveAlpha = 0.65f;
    [SerializeField] private float labelActiveAlpha = 1f;

    [Header("Text Progress Colors")]
    [SerializeField] private string inactiveHexColor = "#FFFFFF";
    [SerializeField] private string activeHexColor = "#59FFF3";

    private Action onCompleted;
    private UnityEngine.Object currentOwner;

    private bool isShown;
    private bool holdCompleted;
    private bool waitForReleaseAfterComplete;

    private float progress01;
    private Vector3 labelBaseScale;
    private string currentLabelText;

    /// <summary>
    /// Capture l echelle de base du label puis remet l overlay a zero.
    /// Ce reset ici est legitime : c est l objet lui-meme qui s initialise.
    /// </summary>
    private void Awake()
    {
        if (label != null)
            labelBaseScale = label.rectTransform.localScale;
        else
            labelBaseScale = Vector3.one;

        currentLabelText = defaultLabelText;
        ForceHideImmediate();
    }

    /// <summary>
    /// Met a jour la progression du hold et les visuels.
    /// Utilise Time.unscaledDeltaTime pour rester fonctionnel
    /// meme si le jeu est pause.
    /// </summary>
    private void Update()
    {
        if (!isShown)
            return;

        UpdatePointerPosition();

        if (waitForReleaseAfterComplete)
        {
            if (!IsHoldInputActive())
                waitForReleaseAfterComplete = false;

            return;
        }

        bool holding = IsHoldInputActive();

        if (holding)
        {
            float duration = Mathf.Max(0.01f, holdDuration);
            progress01 += Time.unscaledDeltaTime / duration;
            progress01 = Mathf.Clamp01(progress01);

            UpdateVisuals(true);

            if (!holdCompleted && progress01 >= 1f)
            {
                holdCompleted = true;
                waitForReleaseAfterComplete = true;
                onCompleted?.Invoke();
            }
        }
        else
        {
            if (progress01 > 0f)
            {
                progress01 -= Time.unscaledDeltaTime * Mathf.Max(0.01f, resetSpeed);
                progress01 = Mathf.Clamp01(progress01);

                UpdateVisuals(progress01 > 0f);
            }
            else
            {
                UpdateVisuals(false);
            }
        }
    }

    /// <summary>
    /// Affiche l overlay pour un owner donne.
    /// Si l overlay etait deja utilise par un autre owner, cet owner est remplace.
    /// C est un comportement volontaire : le dernier Show() gagne.
    /// </summary>
    public void Show(UnityEngine.Object owner, Action onComplete, string customLabelText = null)
    {
        if (owner == null)
        {
            Debug.LogWarning("[HoldToSkipOverlayUI] Show refused: owner is null.");
            return;
        }

        currentOwner = owner;
        onCompleted = onComplete;
        isShown = true;
        holdCompleted = false;
        waitForReleaseAfterComplete = false;
        progress01 = 0f;

        currentLabelText = string.IsNullOrEmpty(customLabelText)
            ? defaultLabelText
            : customLabelText;

        if (root != null)
            root.SetActive(true);

        if (label != null)
        {
            label.fontStyle = FontStyles.Normal;
            label.rectTransform.localScale = labelBaseScale;
            SetLabelAlpha(labelInactiveAlpha);
            UpdateLabelProgressText();
        }

        if (radialFill != null)
            radialFill.fillAmount = 0f;

        if (indicatorCanvasGroup != null)
        {
            indicatorCanvasGroup.alpha = 0f;
            indicatorCanvasGroup.interactable = false;
            indicatorCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Cache l overlay uniquement si le caller est bien l owner courant.
    /// Cela evite qu un autre controller vienne casser l usage courant.
    /// </summary>
    public void Hide(UnityEngine.Object owner)
    {
        if (owner == null)
            return;

        if (currentOwner != owner)
            return;

        ForceHideImmediate();
    }

    /// <summary>
    /// Reset brutal de l overlay, sans verification de proprietaire.
    /// A utiliser uniquement pour :
    /// - l initialisation interne
    /// - un reset global de scene
    /// - un cas exceptionnel de bootstrap
    /// Ne pas utiliser dans les controllers de sequence.
    /// </summary>
    public void ForceHideImmediate()
    {
        isShown = false;
        holdCompleted = false;
        waitForReleaseAfterComplete = false;
        progress01 = 0f;
        onCompleted = null;
        currentOwner = null;
        currentLabelText = defaultLabelText;

        if (radialFill != null)
            radialFill.fillAmount = 0f;

        if (indicatorCanvasGroup != null)
        {
            indicatorCanvasGroup.alpha = 0f;
            indicatorCanvasGroup.interactable = false;
            indicatorCanvasGroup.blocksRaycasts = false;
        }

        if (label != null)
        {
            label.fontStyle = FontStyles.Normal;
            label.rectTransform.localScale = labelBaseScale;
            SetLabelAlpha(labelInactiveAlpha);
            UpdateLabelProgressText();
        }

        if (root != null)
            root.SetActive(false);
    }

    /// <summary>
    /// Retourne true si l overlay est actuellement visible.
    /// </summary>
    public bool IsShown()
    {
        return isShown;
    }

    /// <summary>
    /// Retourne true si l owner fourni est le proprietaire courant.
    /// </summary>
    public bool IsOwnedBy(UnityEngine.Object owner)
    {
        return currentOwner == owner;
    }

    /// <summary>
    /// Positionne l indicateur sur le curseur souris ou le premier touch.
    /// </summary>
    private void UpdatePointerPosition()
    {
        if (indicator == null)
            return;

        Vector2 screenPos;

        if (Input.touchCount > 0)
            screenPos = Input.GetTouch(0).position;
        else
            screenPos = Input.mousePosition;

        indicator.position = screenPos;
    }

    /// <summary>
    /// Met a jour le radial, l opacite de l indicateur, le scale du texte,
    /// le style du texte et la progression coloree.
    /// </summary>
    private void UpdateVisuals(bool active)
    {
        if (radialFill != null)
            radialFill.fillAmount = progress01;

        if (indicatorCanvasGroup != null)
            indicatorCanvasGroup.alpha = active ? 1f : 0f;

        if (label != null)
        {
            float targetScale = active ? textScaleActive : 1f;
            Vector3 desiredScale = labelBaseScale * targetScale;

            label.rectTransform.localScale = Vector3.Lerp(
                label.rectTransform.localScale,
                desiredScale,
                Time.unscaledDeltaTime * Mathf.Max(0.01f, textScaleLerpSpeed)
            );

            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            SetLabelAlpha(active ? labelActiveAlpha : labelInactiveAlpha);
            UpdateLabelProgressText();
        }
    }

    /// <summary>
    /// Recompose le texte du label avec une progression lettre par lettre.
    /// Les espaces ne comptent pas dans la progression.
    /// </summary>
    private void UpdateLabelProgressText()
    {
        if (label == null)
            return;

        string source = string.IsNullOrEmpty(currentLabelText)
            ? defaultLabelText
            : currentLabelText;

        int totalColorableChars = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (!char.IsWhiteSpace(source[i]))
                totalColorableChars++;
        }

        if (totalColorableChars <= 0)
        {
            label.text = source;
            return;
        }

        int activeChars = Mathf.Clamp(
            Mathf.FloorToInt(progress01 * totalColorableChars),
            0,
            totalColorableChars
        );

        StringBuilder sb = new StringBuilder(source.Length * 24);
        int coloredCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (char.IsWhiteSpace(c))
            {
                sb.Append(c);
                continue;
            }

            bool isActiveChar = coloredCount < activeChars;
            string colorHex = isActiveChar ? activeHexColor : inactiveHexColor;

            sb.Append("<color=");
            sb.Append(colorHex);
            sb.Append(">");
            sb.Append(c);
            sb.Append("</color>");

            coloredCount++;
        }

        label.text = sb.ToString();
    }

    /// <summary>
    /// Modifie uniquement l alpha du label sans toucher a sa couleur RGB.
    /// </summary>
    private void SetLabelAlpha(float alpha)
    {
        if (label == null)
            return;

        Color c = label.color;
        c.a = Mathf.Clamp01(alpha);
        label.color = c;
    }

    /// <summary>
    /// Retourne true si le joueur maintient actuellement l input principal.
    /// Compatible souris et touch.
    /// </summary>
    private bool IsHoldInputActive()
    {
        if (Input.GetMouseButton(0))
            return true;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began ||
                t.phase == TouchPhase.Moved ||
                t.phase == TouchPhase.Stationary)
            {
                return true;
            }
        }

        return false;
    }
}