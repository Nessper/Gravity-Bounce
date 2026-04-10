using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestre la transition visuelle du Ship Select lors d'un changement de vaisseau.
///
/// Sequence :
/// - lock des boutons
/// - fade out de l UI cible
/// - fermeture des portes
/// - swap du ship pendant que les portes sont fermees
/// - ouverture des portes
/// - reveal leger du ship apres le debut de l ouverture
/// - fade in de l UI cible
/// - unlock des boutons
///
/// Ce script ne contient pas la logique de donnees du Ship Select.
/// Il delegue le vrai changement de vaisseau au ShipSelectController.
/// </summary>
public class ShipSelectTransitionController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private ShipSelectController shipSelectController;

    [Header("UI Fade")]
    [SerializeField] private CanvasGroup contentCanvasGroup;

    [Header("Doors")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Ship Reveal")]
    // SpriteRenderer du vaisseau affiche dans le hangar.
    [SerializeField] private SpriteRenderer shipImageRenderer;

    [Header("Door Positions (local)")]
    [SerializeField] private Vector3 leftDoorOpenLocalPosition;
    [SerializeField] private Vector3 leftDoorClosedLocalPosition;
    [SerializeField] private Vector3 rightDoorOpenLocalPosition;
    [SerializeField] private Vector3 rightDoorClosedLocalPosition;

    [Header("Timings")]
    [SerializeField] private float uiFadeOutDuration = 0.12f;
    [SerializeField] private float doorCloseDuration = 0.22f;
    [SerializeField] private float closedHoldDuration = 0.06f;
    [SerializeField] private float doorOpenDuration = 0.22f;
    [SerializeField] private float uiFadeInDuration = 0.15f;

    [Header("Ship Reveal Timings")]
    // Delai applique APRES le debut de l ouverture des portes
    // pour rendre la revelation du ship plus lisible.
    [SerializeField] private float shipRevealDelayAfterDoorOpenStart = 0.10f;
    [SerializeField] private float shipRevealDuration = 0.25f;

    [Header("Ship Reveal Visuals")]
    [SerializeField, Range(0f, 1f)] private float shipHiddenAlpha = 0f;

    [Header("Options")]
    [SerializeField] private bool playCloseAndFadeInParallel = true;
    [SerializeField] private bool playOpenAndFadeInParallel = false;

    private bool isTransitionPlaying;

    /// <summary>
    /// Indique si une transition est deja en cours.
    /// Pratique si d autres scripts doivent eviter d envoyer des actions en parallele.
    /// </summary>
    public bool IsTransitionPlaying => isTransitionPlaying;

    private void Awake()
    {
        if (shipSelectController == null)
            Debug.LogError("[ShipSelectTransitionController] ShipSelectController manquant.");

        if (shipImageRenderer == null)
            Debug.LogWarning("[ShipSelectTransitionController] ShipImageRenderer manquant. Le reveal du ship sera ignore.");

        // On force visuellement l etat de depart : portes ouvertes.
        SetDoorsInstantOpen();

        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.alpha = 1f;
            contentCanvasGroup.interactable = true;
            contentCanvasGroup.blocksRaycasts = true;
        }

        if (shipImageRenderer != null)
            SetShipAlphaInstant(1f);
    }

    /// <summary>
    /// Demande une transition vers le ship precedent.
    /// Cette methode est faite pour etre branchee directement au bouton Previous.
    /// </summary>
    public void OnPreviousPressed()
    {
        if (!CanStartTransition())
            return;

        int targetIndex = shipSelectController.GetPreviousIndex();
        if (targetIndex < 0)
            return;

        StartCoroutine(PlayShipSwapTransitionRoutine(targetIndex));
    }

    /// <summary>
    /// Demande une transition vers le ship suivant.
    /// Cette methode est faite pour etre branchee directement au bouton Next.
    /// </summary>
    public void OnNextPressed()
    {
        if (!CanStartTransition())
            return;

        int targetIndex = shipSelectController.GetNextIndex();
        if (targetIndex < 0)
            return;

        StartCoroutine(PlayShipSwapTransitionRoutine(targetIndex));
    }

    /// <summary>
    /// Lance la sequence complete de transition.
    /// </summary>
    private IEnumerator PlayShipSwapTransitionRoutine(int targetIndex)
    {
        isTransitionPlaying = true;

        // On bloque tous les boutons du Ship Select pendant la transition.
        shipSelectController.SetButtonsInteractable(false);

        // On empeche aussi les interactions sur le bloc UI fade si besoin.
        SetCanvasGroupInput(false);

        // Phase 1 : disparition UI + fermeture des portes.
        if (playCloseAndFadeInParallel)
        {
            Coroutine fadeOutRoutine = StartCoroutine(
                FadeCanvasGroup(contentCanvasGroup, 1f, 0f, uiFadeOutDuration));

            Coroutine closeDoorsRoutine = StartCoroutine(
                AnimateDoors(
                    leftDoorOpenLocalPosition,
                    leftDoorClosedLocalPosition,
                    rightDoorOpenLocalPosition,
                    rightDoorClosedLocalPosition,
                    doorCloseDuration));

            yield return fadeOutRoutine;
            yield return closeDoorsRoutine;
        }
        else
        {
            yield return FadeCanvasGroup(contentCanvasGroup, 1f, 0f, uiFadeOutDuration);

            yield return AnimateDoors(
                leftDoorOpenLocalPosition,
                leftDoorClosedLocalPosition,
                rightDoorOpenLocalPosition,
                rightDoorClosedLocalPosition,
                doorCloseDuration);
        }

        // Petit temps mort portes fermees.
        if (closedHoldDuration > 0f)
            yield return new WaitForSeconds(closedHoldDuration);

        // Le vrai swap se fait ici, cache derriere les portes.
        shipSelectController.ApplyShipByIndex(targetIndex);

        // On prepare le ship dans un etat legerement "cache"
        // pour eviter qu il soit parfaitement lisible a la premiere frame.
        PrepareShipRevealStartState();

        // Phase 2 : ouverture des portes d abord.
        // Le reveal du ship commence seulement une fois les portes
        // deja un peu ouvertes, pour ne pas etre noye dans leur mouvement.
        Coroutine openDoorsRoutine = StartCoroutine(
            AnimateDoors(
                leftDoorClosedLocalPosition,
                leftDoorOpenLocalPosition,
                rightDoorClosedLocalPosition,
                rightDoorOpenLocalPosition,
                doorOpenDuration));

        Coroutine shipRevealRoutine = null;

        if (shipImageRenderer != null && shipRevealDelayAfterDoorOpenStart > 0f)
            yield return new WaitForSeconds(shipRevealDelayAfterDoorOpenStart);

        if (shipImageRenderer != null)
            shipRevealRoutine = StartCoroutine(AnimateShipReveal());

        if (playOpenAndFadeInParallel)
        {
            Coroutine fadeInRoutine = StartCoroutine(
                FadeCanvasGroup(contentCanvasGroup, 0f, 1f, uiFadeInDuration));

            yield return openDoorsRoutine;
            yield return fadeInRoutine;
        }
        else
        {
            yield return openDoorsRoutine;
            yield return FadeCanvasGroup(contentCanvasGroup, 0f, 1f, uiFadeInDuration);
        }

        if (shipRevealRoutine != null)
            yield return shipRevealRoutine;

        // On retablit l interaction.
        shipSelectController.SetButtonsInteractable(true);
        SetCanvasGroupInput(true);

        isTransitionPlaying = false;
    }

    /// <summary>
    /// Indique si une nouvelle transition peut commencer.
    /// </summary>
    private bool CanStartTransition()
    {
        if (isTransitionPlaying)
            return false;

        if (shipSelectController == null)
            return false;

        if (!shipSelectController.CanNavigate())
            return false;

        return true;
    }

    /// <summary>
    /// Active ou desactive l interaction sur le CanvasGroup cible.
    /// </summary>
    private void SetCanvasGroupInput(bool enabled)
    {
        if (contentCanvasGroup == null)
            return;

        contentCanvasGroup.interactable = enabled;
        contentCanvasGroup.blocksRaycasts = enabled;
    }

    /// <summary>
    /// Place instantanement les portes en position ouverte.
    /// </summary>
    private void SetDoorsInstantOpen()
    {
        if (leftDoor != null)
            leftDoor.localPosition = leftDoorOpenLocalPosition;

        if (rightDoor != null)
            rightDoor.localPosition = rightDoorOpenLocalPosition;
    }

    /// <summary>
    /// Prepare le ship juste apres le swap :
    /// alpha reduite.
    /// </summary>
    private void PrepareShipRevealStartState()
    {
        if (shipImageRenderer == null)
            return;

        SetShipAlphaInstant(shipHiddenAlpha);
    }

    /// <summary>
    /// Anime le reveal du ship :
    /// - fade alpha vers 1
    /// </summary>
    private IEnumerator AnimateShipReveal()
    {
        if (shipImageRenderer == null)
            yield break;

        float duration = shipRevealDuration;

        if (duration <= 0f)
        {
            SetShipAlphaInstant(1f);
            yield break;
        }

        float elapsed = 0f;
        Color currentColor = shipImageRenderer.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = EaseOutCubic(t);

            float alpha = Mathf.LerpUnclamped(shipHiddenAlpha, 1f, t);
            shipImageRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);

            yield return null;
        }

        SetShipAlphaInstant(1f);
    }

    /// <summary>
    /// Applique instantanement l alpha au ship.
    /// </summary>
    private void SetShipAlphaInstant(float alpha)
    {
        if (shipImageRenderer == null)
            return;

        Color color = shipImageRenderer.color;
        color.a = alpha;
        shipImageRenderer.color = color;
    }

    /// <summary>
    /// Anime le fade d un CanvasGroup.
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = EaseInOutCubic(t);
            group.alpha = Mathf.LerpUnclamped(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    /// <summary>
    /// Anime les deux portes en meme temps.
    /// </summary>
    private IEnumerator AnimateDoors(
        Vector3 leftFrom,
        Vector3 leftTo,
        Vector3 rightFrom,
        Vector3 rightTo,
        float duration)
    {
        if (leftDoor == null && rightDoor == null)
            yield break;

        if (duration <= 0f)
        {
            if (leftDoor != null)
                leftDoor.localPosition = leftTo;

            if (rightDoor != null)
                rightDoor.localPosition = rightTo;

            yield break;
        }

        float elapsed = 0f;

        if (leftDoor != null)
            leftDoor.localPosition = leftFrom;

        if (rightDoor != null)
            rightDoor.localPosition = rightFrom;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = EaseInOutCubic(t);

            if (leftDoor != null)
                leftDoor.localPosition = Vector3.LerpUnclamped(leftFrom, leftTo, t);

            if (rightDoor != null)
                rightDoor.localPosition = Vector3.LerpUnclamped(rightFrom, rightTo, t);

            yield return null;
        }

        if (leftDoor != null)
            leftDoor.localPosition = leftTo;

        if (rightDoor != null)
            rightDoor.localPosition = rightTo;
    }

    /// <summary>
    /// Ease cubic standard, douce a l entree et a la sortie.
    /// </summary>
    private float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    /// <summary>
    /// Ease de sortie, plus naturelle pour un reveal court.
    /// </summary>
    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}