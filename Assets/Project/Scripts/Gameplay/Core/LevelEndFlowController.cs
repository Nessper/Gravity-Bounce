using System.Collections;
using UnityEngine;

/// <summary>
/// Contrôleur de fin de niveau :
/// - écoute EndLevelUI (OnVictory / OnSequenceFailed)
/// - met à jour les ContractLives et la persistance
/// - joue une séquence de dialogues contract (via DialogManager + DialogSequenceRunner)
/// - affiche ensuite FinalPanelOverlay + stamp (Victory / Defeat / GameOver)
/// - configure les boutons de fin (MENU / RETRY / NEXT) via EndLevelButtonsUI.
/// </summary>
public class LevelEndFlowController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EndLevelUI endLevelUI;
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;
    [SerializeField] private EndLevelButtonsUI endLevelButtonsUI;

    [Header("Dialog sequences (ids du JSON)")]
    [SerializeField] private string victorySequenceId = "contract_victory";
    [SerializeField] private string defeatTwoLivesSequenceId = "contract_defeat_2";
    [SerializeField] private string defeatOneLifeSequenceId = "contract_defeat_1";
    [SerializeField] private string gameOverSequenceId = "contract_gameover";

    [Tooltip("Délai supplémentaire après la fin du dialogue avant d'afficher le stamp.")]
    [SerializeField] private float extraDelayAfterDialog = 0.6f;

    [Header("Final Panel Overlay")]
    [SerializeField] private GameObject finalPanelOverlay;
    [SerializeField] private GameObject stampVictory;
    [SerializeField] private GameObject stampDefeat;
    [SerializeField] private GameObject stampGameOver;

    private bool endHandled;

    private enum EndType
    {
        Victory,
        Defeat,
        GameOver
    }

    // =====================================================================
    // CYCLE UNITY
    // =====================================================================

    private void OnEnable()
    {
        if (endLevelUI == null)
        {
            Debug.LogError("[LevelEndFlowController] EndLevelUI reference missing.");
            return;
        }

        endLevelUI.OnVictory.AddListener(HandleVictory);
        endLevelUI.OnSequenceFailed.AddListener(HandleDefeat);
    }

    private void OnDisable()
    {
        if (endLevelUI == null)
            return;

        endLevelUI.OnVictory.RemoveListener(HandleVictory);
        endLevelUI.OnSequenceFailed.RemoveListener(HandleDefeat);
    }

    // =====================================================================
    // HANDLERS VICTOIRE / DEFAITE
    // =====================================================================

    private void HandleVictory()
    {
        if (endHandled)
            return;

        StartCoroutine(VictoryRoutine());
    }

    private void HandleDefeat()
    {
        if (endHandled)
            return;

        StartCoroutine(DefeatRoutine());
    }

    private IEnumerator VictoryRoutine()
    {
        endHandled = true;

        int contractLives = runSessionState != null ? runSessionState.ContractLives : 0;

        // 1) On affiche d'abord l'overlay global
        if (finalPanelOverlay != null)
            finalPanelOverlay.SetActive(true);

        // 2) Dialogues de fin de mission
        yield return PlayPostMissionDialog(EndType.Victory, contractLives);

        // 3) Stamp + boutons
        ShowEnd(EndType.Victory);
    }

    private IEnumerator DefeatRoutine()
    {
        endHandled = true;

        int remainingAfterLoss = 0;

        if (runSessionState != null)
        {
            // On perd 1 "contract life"
            runSessionState.LoseContractLife(1);
            remainingAfterLoss = runSessionState.ContractLives;

            // Persistance
            UpdateContractLivesInSave(remainingAfterLoss);
        }
        else
        {
            Debug.LogWarning("[LevelEndFlowController] RunSessionState missing, treating as simple Defeat.");
            remainingAfterLoss = 0;
        }

        EndType type = remainingAfterLoss > 0 ? EndType.Defeat : EndType.GameOver;

        // 1) Overlay global d'abord
        if (finalPanelOverlay != null)
            finalPanelOverlay.SetActive(true);

        // 2) Dialogues en fonction du type / nombre de vies restantes
        yield return PlayPostMissionDialog(type, remainingAfterLoss);

        // 3) Stamp + boutons
        ShowEnd(type);
    }

    // =====================================================================
    // DIALOGUES
    // =====================================================================

    private IEnumerator PlayPostMissionDialog(EndType type, int remainingContractLives)
    {
        if (dialogSequenceRunner == null)
            yield break;

        // On récupère le DialogManager global (Boot scene, DontDestroyOnLoad)
        DialogManager dialogManager = Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
        {
            Debug.LogWarning("[LevelEndFlowController] No DialogManager found in scene.");
            yield break;
        }

        // On attend que la base de dialogues soit prête
        while (!dialogManager.IsReady)
            yield return null;

        string sequenceId = ResolveSequenceId(type, remainingContractLives);
        if (string.IsNullOrEmpty(sequenceId))
            yield break;

        // On suppose que DialogManager expose GetSequenceById (sinon il faudra l'ajouter)
        DialogSequence seq = dialogManager.GetSequenceById(sequenceId);
        if (seq == null)
        {
            Debug.LogWarning("[LevelEndFlowController] No dialog sequence for id=" + sequenceId);
            yield break;
        }

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;
        dialogSequenceRunner.Play(lines, () => done = true);

        while (!done)
            yield return null;

        if (extraDelayAfterDialog > 0f)
            yield return new WaitForSecondsRealtime(extraDelayAfterDialog);
    }

    private string ResolveSequenceId(EndType type, int remainingContractLives)
    {
        switch (type)
        {
            case EndType.Victory:
                return victorySequenceId;

            case EndType.Defeat:
                if (remainingContractLives >= 2)
                    return defeatTwoLivesSequenceId;
                if (remainingContractLives == 1)
                    return defeatOneLifeSequenceId;
                // Si on passe ici avec 0 alors que type=Defeat, on bascule sur gameOver
                return gameOverSequenceId;

            case EndType.GameOver:
                return gameOverSequenceId;

            default:
                return null;
        }
    }

    // =====================================================================
    // AFFICHAGE FINAL (STAMP + BOUTONS)
    // =====================================================================

    private void ShowEnd(EndType type)
    {
        // Stamps
        if (stampVictory != null)
            stampVictory.SetActive(type == EndType.Victory);

        if (stampDefeat != null)
            stampDefeat.SetActive(type == EndType.Defeat);

        if (stampGameOver != null)
            stampGameOver.SetActive(type == EndType.GameOver);

        // Boutons
        if (endLevelButtonsUI != null)
        {
            switch (type)
            {
                case EndType.Victory:
                    endLevelButtonsUI.ShowVictory();
                    break;

                case EndType.Defeat:
                    endLevelButtonsUI.ShowDefeat();
                    break;

                case EndType.GameOver:
                    endLevelButtonsUI.ShowGameOver();
                    break;
            }
        }

        Debug.Log("[LevelEndFlowController] End type = " + type);
    }

    // =====================================================================
    // PERSISTANCE CONTRACT LIVES
    // =====================================================================

    private void UpdateContractLivesInSave(int contractLives)
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Current == null ||
            SaveManager.Instance.Current.runState == null)
        {
            return;
        }

        var run = SaveManager.Instance.Current.runState;

        int clamped = Mathf.Max(0, contractLives);
        run.remainingContractLives = clamped;

        if (clamped <= 0)
        {
            // Plus de vies de contrat : fin de run
            run.hasOngoingRun = false;
        }

        SaveManager.Instance.Save();

        Debug.Log("[LevelEndFlowController] Persisted contract lives = " + clamped
                  + ", hasOngoingRun=" + run.hasOngoingRun);
    }

    // =====================================================================
    // CALLBACKS BOUTONS (MENU / RETRY / NEXT)
    // =====================================================================

    /// <summary>
    /// Appelle par le bouton MENU en fin de niveau.
    /// Retourne au Title via GameFlow.
    /// </summary>
    public void OnClickMenu()
    {
        if (BootRoot.GameFlow == null)
        {
            Debug.LogError("[LevelEndFlowController] GameFlow est null, impossible de retourner au menu.");
            return;
        }

        // Option : on evite de rejouer l intro du Title a chaque retour.
        if (RunConfig.Instance != null)
        {
            RunConfig.Instance.SkipTitleIntroOnce = true;
        }

        BootRoot.GameFlow.GoToTitle();
    }

    /// <summary>
    /// Appelle par le bouton RETRY.
    /// Redemarre le niveau courant en conservant le hull runtime.
    /// </summary>
    public void OnClickRetry()
    {
        if (BootRoot.GameFlow == null)
        {
            Debug.LogError("[LevelEndFlowController] GameFlow est null, impossible de relancer le niveau.");
            return;
        }

        if (runSessionState != null)
        {
            // On arme le flag pour que RunSessionBootstrapper reutilise le hull courant.
            runSessionState.MarkCarryHullOnNextRestart(true);
        }
        else
        {
            Debug.LogWarning("[LevelEndFlowController] RunSessionState manquant, retry sans conservation de hull.");
        }

        // On redemarre le niveau via GameFlow (Main sera relance).
        BootRoot.GameFlow.StartLevel();
    }

    /// <summary>
    /// Appelle par le bouton NEXT (victoire).
    /// Pour l instant: relance le meme niveau via GameFlow.
    /// Plus tard: logique de campagne (niveau suivant).
    /// </summary>
    public void OnClickNext()
    {
        if (BootRoot.GameFlow == null)
        {
            Debug.LogError("[LevelEndFlowController] GameFlow est null, impossible de passer au niveau suivant.");
            return;
        }

        // Placeholder : on redemarre le niveau courant.
        // Quand la campagne sera en place, ce sera:
        // BootRoot.GameFlow.GoToNextLevel();
        BootRoot.GameFlow.StartLevel();
    }

}
