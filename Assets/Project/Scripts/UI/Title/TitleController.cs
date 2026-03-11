using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controle la scene Title.
/// Gere :
/// - Fade-in / fade-out
/// - New Game / Continue / Quit
/// - Affichage d'un toast d'information 1-shot (ex: penalite Hull suite a un abandon)
/// - Verrouillage de l'input tant que le toast n'a pas ete affiche (evite overlaps avec les panels modaux)
/// </summary>
public class TitleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Main Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("Warning New Game")]
    [SerializeField] private GameObject warningNewGamePanel;
    [SerializeField] private Button warningNewGame_BackButton;
    [SerializeField] private Button warningNewGame_OkButton;

    // --------------------------------------------------------------------
    // INFO TOAST
    // --------------------------------------------------------------------

    [Header("Info Toast")]
    [Tooltip("Racine du toast (desactivee par defaut dans la scene).")]
    [SerializeField] private GameObject infoToastRoot;

    [Tooltip("Texte TMP du toast.")]
    [SerializeField] private TMP_Text infoToastText;

    [Tooltip("Duree d'affichage du toast (temps reel).")]
    [SerializeField] private float infoToastDurationSec = 3f;

    [Tooltip("Delai apres la fin du fade-in avant l'apparition du toast (temps reel).")]
    [SerializeField] private float infoToastDelayAfterFadeSec = 1f;

    [Tooltip("Temps minimal pendant lequel on bloque les clics apres l'affichage du toast (evite les overlaps).")]
    [SerializeField] private float infoToastInputLockAfterShowSec = 0.25f;

    [Header("Info Toast - Textes")]
    [Tooltip("Message affiche quand une penalite Hull est appliquee (placeholder {0} = montant).")]
    [SerializeField] private string abortPenaltyText = "Mission abandonnee : Hull -{0}";

    [Tooltip("Message affiche quand la penalite provoque un Game Over (placeholder {0} = montant).")]
    [SerializeField] private string abortPenaltyGameOverText = "Mission abandonnee : Hull -{0} (Game Over)";

    // --------------------------------------------------------------------
    // FADE CONFIG
    // --------------------------------------------------------------------

    [Header("Fade")]
    [SerializeField] private float fadeInDelay = 5f;
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;

    // --------------------------------------------------------------------
    // INTERNAL
    // --------------------------------------------------------------------

    private Coroutine infoToastCo;
    private bool hasPendingInfoToast;
    private string pendingInfoToastMessage;

    // --------------------------------------------------------------------
    // Unity
    // --------------------------------------------------------------------

    /// <summary>
    /// Point d'entree de la scene Title.
    /// Initialise l'UI, prepare le toast 1-shot, puis lance le fade-in (ou le skip).
    /// </summary>
    private void Start()
    {
        // La scene Title doit etre chargee depuis Boot pour garantir que BootRoot/GameFlow existent.
        if (BootRoot.GameFlow == null)
            Debug.LogError("[TitleController] BootRoot.GameFlow est null. La scene Title doit etre lancee depuis Boot.");

        SetupInitialState();
        SetupContinueButtonVisibility();

        // Prepare un message 1-shot (sans affichage immediat).
        PrepareAbortPenaltyFeedback();

        // Skip optionnel du fade d'intro (1 fois), controle par RunConfig.
        if (RunConfig.Instance != null && RunConfig.Instance.SkipTitleIntroOnce)
        {
            SkipIntroFade();

            // Apres un skip, on applique la meme regle : si un toast est pending, on verrouille l'input jusqu'au toast.
            if (hasPendingInfoToast && !string.IsNullOrEmpty(pendingInfoToastMessage))
                StartCoroutine(ToastThenUnlockInputRoutine());
            else
                EnableMainInput();

            return;
        }

        StartCoroutine(FadeInRoutine());
    }

    // ---------------------------------------------------------
    // Initialisation UI
    // ---------------------------------------------------------

    /// <summary>
    /// Met l'UI dans un etat propre au demarrage :
    /// - Canvas invisible et non cliquable
    /// - Panels modaux et toast desactives
    /// </summary>
    private void SetupInitialState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);

        if (infoToastRoot != null)
            infoToastRoot.SetActive(false);
    }

    /// <summary>
    /// Retourne true si une run peut etre continuee.
    /// Garde-fou : une run "en cours" avec Hull=0 (ou ContractLives=0) est invalide.
    /// </summary>
    private bool CanContinueRun()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.Current : null;
        if (save == null || save.runState == null)
            return false;

        var run = save.runState;

        return run.hasOngoingRun
               && run.remainingHullInRun > 0
               && run.remainingContractLives > 0;
    }

    /// <summary>
    /// Affiche / masque le bouton Continue selon l'etat de sauvegarde.
    /// </summary>
    private void SetupContinueButtonVisibility()
    {
        bool canContinue = CanContinueRun();

        if (continueButton != null)
            continueButton.gameObject.SetActive(canContinue);
    }

    /// <summary>
    /// Applique instantanement l'etat "fin de fade" :
    /// - Canvas visible
    /// - Input decide plus tard (toast ou pas)
    /// </summary>
    private void SkipIntroFade()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;

            // Important : on ne rend pas l'UI cliquable ici.
            // L'activation de l'input depend de l'existence d'un toast pending.
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = false;
    }

    // ---------------------------------------------------------
    // Preparation du feedback penalite (sans affichage)
    // ---------------------------------------------------------

    /// <summary>
    /// Prepare un toast 1-shot si une penalite d'abandon a ete appliquee sur la run precedente.
    /// Consomme le flag de feedback immediatement (evite repetition au prochain Start).
    /// </summary>
    private void PrepareAbortPenaltyFeedback()
    {
        if (SaveManager.Instance == null)
            return;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        // IMPORTANT :
        // On n'affiche "Mission abandonnee..." QUE si une penalite d'abandon a ete appliquee.
        // pendingGameOverFromAbort seul ne doit pas declencher ce toast (sinon faux positifs).
        bool showPenalty = run.pendingAbortHullPenaltyFeedback;
        if (!showPenalty)
            return;

        bool showGameOver = run.pendingGameOverFromAbort;

        int amount = Mathf.Max(1, run.lastAbortHullPenaltyAmount);

        pendingInfoToastMessage = showGameOver
            ? string.Format(abortPenaltyGameOverText, amount)
            : string.Format(abortPenaltyText, amount);

        hasPendingInfoToast = true;

        // Consommation du 1-shot tout de suite (pas de repetition au prochain Start).
        run.pendingAbortHullPenaltyFeedback = false;
        run.lastAbortHullPenaltyAmount = 0;
        run.pendingGameOverFromAbort = false;

        SaveManager.Instance.Save();
    }

    // ---------------------------------------------------------
    // Toast
    // ---------------------------------------------------------

    /// <summary>
    /// Affiche un toast immediatement et programme sa disparition.
    /// </summary>
    private void ShowInfoToast(string msg)
    {
        if (infoToastRoot == null || infoToastText == null)
            return;

        infoToastText.text = msg;
        infoToastRoot.SetActive(true);

        // Si un timer etait deja en cours (cas rare), on le remplace.
        if (infoToastCo != null)
            StopCoroutine(infoToastCo);

        infoToastCo = StartCoroutine(HideInfoToastLater());
    }

    /// <summary>
    /// Cache le toast apres la duree configuree.
    /// </summary>
    private IEnumerator HideInfoToastLater()
    {
        float d = Mathf.Max(0f, infoToastDurationSec);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        if (infoToastRoot != null)
            infoToastRoot.SetActive(false);

        infoToastCo = null;
    }

    /// <summary>
    /// Force la disparition du toast immediatement (utile avant d'ouvrir un panel modal).
    /// </summary>
    private void ForceHideInfoToast()
    {
        if (infoToastCo != null)
        {
            StopCoroutine(infoToastCo);
            infoToastCo = null;
        }

        if (infoToastRoot != null)
            infoToastRoot.SetActive(false);
    }

    // ---------------------------------------------------------
    // Input
    // ---------------------------------------------------------

    /// <summary>
    /// Rend l'UI principale cliquable (CanvasGroup).
    /// Centralise l'activation des interactions pour eviter les incoherences.
    /// </summary>
    private void EnableMainInput()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Routine de presentation du toast, puis activation de l'input.
    /// Objectif : eviter que le joueur clique avant l'apparition du toast, ce qui cree des overlaps.
    /// </summary>
    private IEnumerator ToastThenUnlockInputRoutine()
    {
        // Delai apres fade-in avant le toast.
        float d = Mathf.Max(0f, infoToastDelayAfterFadeSec);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        // Si un panel modal est deja ouvert (cas rare), on ne montre pas le toast.
        // Choix volontaire : on prefere perdre le toast plutot que de chevaucher l'UI.
        if (warningNewGamePanel != null && warningNewGamePanel.activeSelf)
        {
            hasPendingInfoToast = false;
            pendingInfoToastMessage = null;

            EnableMainInput();
            yield break;
        }

        // Affiche le toast puis consomme le 1-shot.
        ShowInfoToast(pendingInfoToastMessage);

        hasPendingInfoToast = false;
        pendingInfoToastMessage = null;

        // Micro-lock d'input apres apparition du toast pour eviter un clic "pile au meme frame".
        float lockSec = Mathf.Max(0f, infoToastInputLockAfterShowSec);
        if (lockSec > 0f)
            yield return new WaitForSecondsRealtime(lockSec);

        EnableMainInput();
    }

    // ---------------------------------------------------------
    // Fade
    // ---------------------------------------------------------

    /// <summary>
    /// Fait apparaitre l'UI en fondu.
    /// A la fin du fade, l'input est active soit immediatement (pas de toast),
    /// soit apres l'affichage du toast (toast pending).
    /// </summary>
    private IEnumerator FadeInRoutine()
    {
        float d = Mathf.Max(0f, fadeInDelay);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        float duration = Mathf.Max(0.0001f, fadeInTime);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t / duration));

            yield return null;
        }

        // Etat final du fade.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;

            // Important : on verrouille l'input par defaut ici.
            // La suite decide si on doit attendre le toast.
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Si un toast est pending, on le montre d'abord puis on active l'input.
        // Sinon, on active l'input tout de suite.
        if (hasPendingInfoToast && !string.IsNullOrEmpty(pendingInfoToastMessage))
            StartCoroutine(ToastThenUnlockInputRoutine());
        else
            EnableMainInput();
    }

    /// <summary>
    /// Fait disparaitre l'UI en fondu, puis attend un court instant (comfort visuel).
    /// </summary>
    private IEnumerator FadeOutRoutine()
    {
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float duration = Mathf.Max(0.0001f, fadeOutTime);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / duration));

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        yield return new WaitForSecondsRealtime(0.2f);
    }

    // ---------------------------------------------------------
    // Boutons (hooks Inspector)
    // ---------------------------------------------------------

    /// <summary>
    /// Clique sur New Game :
    /// - Si une run valide existe, ouvre un panel d'avertissement
    /// - Sinon, lance directement une nouvelle run
    /// Important : on cache le toast pour eviter un chevauchement avec le panel modal.
    /// </summary>
    public void OnNewGamePressed()
    {
        ForceHideInfoToast();

        // IMPORTANT :
        // Si la run est invalide (Hull=0), CanContinueRun() renvoie false
        // => pas de warning inutile, on lance New Game directement.
        bool hasValidRun = CanContinueRun();

        if (hasValidRun)
        {
            if (warningNewGamePanel != null)
                warningNewGamePanel.SetActive(true);

            return;
        }

        StartNewGame();
    }

    /// <summary>
    /// Bouton Back du panel d'avertissement New Game : ferme le panel.
    /// </summary>
    public void OnNewGameWarningBack()
    {
        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);
    }

    /// <summary>
    /// Bouton OK du panel d'avertissement :
    /// - Ferme le panel
    /// - Reset la run
    /// - Lance une nouvelle partie
    /// </summary>
    public void OnNewGameWarningOk()
    {
        if (warningNewGamePanel != null)
            warningNewGamePanel.SetActive(false);

        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetRunState();

        StartNewGame();
    }

    /// <summary>
    /// Ddemarre un New Game en garantissant un etat de sauvegarde propre.
    /// </summary>
    private void StartNewGame()
    {
        // Reset explicite pour garantir un etat propre.
        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetRunState();

        StartCoroutine(StartNewGameRoutine());
    }

    /// <summary>
    /// Routine de transition vers ShipSelect :
    /// - Desactive l'input
    /// - Fade-out
    /// - Navigation via GameFlow
    /// </summary>
    private IEnumerator StartNewGameRoutine()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        yield return FadeOutRoutine();

        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.GoToShipSelect();
    }

    /// <summary>
    /// Clique sur Continue :
    /// - Verifie une derniere fois la validite de la run (garde-fou)
    /// - Desactive l'input
    /// - Lance la routine de transition vers le niveau
    /// </summary>
    public void OnContinuePressed()
    {
        // Guard rail : si jamais Continue est cliquable via debug/hot reload,
        // on verifie ici aussi.
        if (!CanContinueRun())
        {
            Debug.LogWarning("[TitleController] Continue refuse : run invalide (Hull<=0 ou ContractLives<=0).");
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        StartCoroutine(ContinueRoutine());
    }

    /// <summary>
    /// Transition vers le niveau en cours :
    /// - Fade-out
    /// - Navigation via GameFlow
    /// </summary>
    private IEnumerator ContinueRoutine()
    {
        yield return FadeOutRoutine();

        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.GoToRunHub();
    }

    /// <summary>
    /// Quitte l'application (ou stop Play Mode dans l'editor).
    /// </summary>
    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
