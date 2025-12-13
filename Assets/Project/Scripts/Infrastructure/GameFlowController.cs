using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game flow controller.
/// Responsible for switching between Boot, Title, ShipSelect and Level scenes.
/// It does not decide which ship or level to play. That data lives in RunConfig and SaveManager.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    /// <summary>
    /// High level phase of the game flow.
    /// Used mainly for debugging and future extensions.
    /// </summary>
    public enum GameFlowPhase
    {
        Boot,
        Title,
        ShipSelect,
        Level,
        Loading
    }

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string shipSelectSceneName = "ShipSelect";
    [SerializeField] private string levelSceneName = "Main";

    /// <summary>
    /// Current phase of the global game flow.
    /// </summary>
    public GameFlowPhase CurrentPhase { get; private set; } = GameFlowPhase.Boot;

    /// <summary>
    /// True while an async scene load is in progress.
    /// Prevents double transitions.
    /// </summary>
    private bool isLoadingScene;

    private void Awake()
    {
        // Register this instance in BootRoot so it can be accessed as BootRoot.GameFlow.
        BootRoot.RegisterGameFlow(this);
    }

    // ---------------------------------------------------------
    // Public API
    // ---------------------------------------------------------

    /// <summary>
    /// Switches to the Title scene.
    /// Called for example:
    /// - at the end of Boot (Bootstrapper)
    /// - from ShipSelect "Back" button.
    /// </summary>
    public void GoToTitle()
    {
        if (isLoadingScene)
            return;
        Debug.Log("[GameFlow] GoToTitle appelé");
        StartSceneTransition(titleSceneName, GameFlowPhase.Title);
    }

    /// <summary>
    /// Switches to the ShipSelect scene.
    /// Called for example:
    /// - from Title "New Game" flow.
    /// </summary>
    public void GoToShipSelect()
    {
        if (isLoadingScene)
            return;

        StartSceneTransition(shipSelectSceneName, GameFlowPhase.ShipSelect);
    }

    /// <summary>
    /// Starts or resumes a level.
    /// This method only loads the level scene.
    /// The actual ship, world and level information must already be set
    /// in RunConfig and SaveManager.runState before calling this.
    /// </summary>
    public void StartLevel()
    {
        if (isLoadingScene)
            return;

        StartSceneTransition(levelSceneName, GameFlowPhase.Level);
    }

    // ---------------------------------------------------------
    // Internal scene loading helpers
    // ---------------------------------------------------------

    /// <summary>
    /// Starts an async scene transition to the given scene name and target phase.
    /// </summary>
    private void StartSceneTransition(string sceneName, GameFlowPhase targetPhase)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameFlowController] Scene name is empty. Cannot start transition.");
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName, targetPhase));
    }

    /// <summary>
    /// Coroutine that performs the async scene load and updates the phase.
    /// </summary>
    private IEnumerator LoadSceneRoutine(string sceneName, GameFlowPhase targetPhase)
    {
        isLoadingScene = true;
        CurrentPhase = GameFlowPhase.Loading;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        op.allowSceneActivation = true;

        // Later we can expose op.progress for a loading screen.
        while (!op.isDone)
        {
            yield return null;
        }

        CurrentPhase = targetPhase;
        isLoadingScene = false;
    }
}
