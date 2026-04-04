using TMPro;
using UnityEngine;

/// <summary>
/// RunHub controller (map/steps of the run).
/// Responsibilities:
/// - Load/display world name
/// - Build the node list (map)
/// - Route NEXT to Shop / Level(Boss) / Credits
///
/// Rules:
/// - Ending nodes are NOT displayed on the hub list.
/// - If the current node is Ending, we automatically route to CreditsScene.
/// </summary>
public class RunHubController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("UI")]
    [SerializeField] private TMP_Text worldNameText;
    [SerializeField] private Transform listRoot;
    [SerializeField] private RunHubNodeView nodePrefab;

    [Header("Shop")]
    [SerializeField] private RunHubShopTransition shopTransition;
    [SerializeField] private ShopDialogController shopDialogController;

    [Header("Icons")]
    [SerializeField] private Sprite levelIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite bossIcon;

    private void Awake()
    {
        if (runSession != null)
            runSession.LoadFromSave();

        if (shopTransition != null)
            shopTransition.OnShopPanelRevealed = HandleShopPanelRevealed;
    }

    private void Start()
    {
        if (runSession == null)
            return;


        UpdateWorldName();
        BuildList();

        // If we're already on Ending, don't stay on the hub.
        TryAutoRouteEnding();

        if (shopTransition != null)
            shopTransition.ResetToRunHubState();
    }

    /// <summary>
    /// NEXT button (hub).
    /// Routes according to the current node:
    /// - Ending => consume + Credits
    /// - Shop   => transition + dialog
    /// - Else   => StartLevel (Level/Boss)
    /// </summary>
    public void OnNextPressed()
    {
        if (runSession == null)
            return;

        if (!runSession.EnsurePlanLoaded())
            return;

        RunPlan plan = runSession.CurrentRunPlan;
        if (plan == null || !plan.HasNodes)
            return;

        int idx = runSession.CurrentNodeIndex;
        if (idx < 0 || idx >= plan.nodes.Count)
            return;

        RunNode node = plan.nodes[idx];
        if (node == null)
            return;

        // Ending first (safety + deterministic)
        if (TryAutoRouteEnding())
            return;

        // Shop
        if (node.type == RunNodeType.Shop)
        {
            if (shopTransition != null)
                shopTransition.PlayToShopTransition();
            else
                HandleShopPanelRevealed();

            return;
        }

        // Level / Boss
        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.StartLevel();
    }

    /// <summary>
    /// NEXT button (shop UI).
    /// Consumes the SHOP node, then returns to the RunHub.
    /// The next action will be decided from the hub based on the new current node.
    /// </summary>
    public void OnShopNextPressed()
    {
        if (runSession == null)
            return;

        if (!runSession.EnsurePlanLoaded())
            return;

        RunPlan plan = runSession.CurrentRunPlan;
        if (plan == null || !plan.HasNodes)
            return;

        int idx = runSession.CurrentNodeIndex;
        if (idx < 0 || idx >= plan.nodes.Count)
            return;

        RunNode node = plan.nodes[idx];
        if (node == null)
            return;

        if (node.type != RunNodeType.Shop)
            return;

        runSession.CommitVictoryAndAdvanceNode();

        UpdateWorldName();
        BuildList();

        if (shopTransition != null)
            shopTransition.ResetToRunHubState();
    }

    public void OnMenuPressed()
    {
        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.GoToTitle();
    }

    // ---------------------------------------------------------
    // Internals
    // ---------------------------------------------------------

    private void HandleShopPanelRevealed()
    {
        if (shopDialogController == null)
        {
            Debug.LogWarning("[RunHub] shopDialogController is not assigned.");
            if (shopTransition != null)
                shopTransition.ShowUIAfterDialog();
            return;
        }

        shopDialogController.PlayWelcomeThenShowUI(() =>
        {
            if (shopTransition != null)
                shopTransition.ShowUIAfterDialog();
        });
    }

    private void UpdateWorldName()
    {
        if (worldNameText == null || runSession == null)
            return;

        string worldId = runSession.WorldId;
        if (string.IsNullOrEmpty(worldId))
            return;

        string displayName = WorldCatalogService.GetWorldDisplayName(worldId);
        worldNameText.text = string.IsNullOrEmpty(displayName) ? worldId : displayName;
    }

    /// <summary>
    /// Builds the hub list.
    /// Ending nodes are intentionally not displayed.
    /// </summary>
    private void BuildList()
    {
        if (runSession == null || listRoot == null || nodePrefab == null)
            return;

        RunPlan plan = runSession.CurrentRunPlan;
        if (plan == null || !plan.HasNodes)
            return;

        int currentIndex = runSession.CurrentNodeIndex;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        for (int i = 0; i < plan.nodes.Count; i++)
        {
            RunNode node = plan.nodes[i];
            if (node == null)
                continue;

            // Hide ending from the hub map
            if (node.type == RunNodeType.Ending)
                continue;

            RunHubNodeView view = Instantiate(nodePrefab, listRoot);

            RunHubNodeView.VisualState state =
                i < currentIndex ? RunHubNodeView.VisualState.Done :
                i == currentIndex ? RunHubNodeView.VisualState.Current :
                RunHubNodeView.VisualState.Locked;

            view.Setup(GetIconForNode(node.type), ResolveLabel(node), state);
        }
    }

    /// <summary>
    /// If current node is Ending:
    /// - consume the node (advance) to avoid looping
    /// - route to CreditsScene
    /// </summary>
    private bool TryAutoRouteEnding()
    {
        if (runSession == null)
            return false;

        if (!runSession.EnsurePlanLoaded())
            return false;

        RunPlan plan = runSession.CurrentRunPlan;
        if (plan == null || !plan.HasNodes)
            return false;

        int idx = runSession.CurrentNodeIndex;
        if (idx < 0 || idx >= plan.nodes.Count)
            return false;

        RunNode node = plan.nodes[idx];
        if (node == null)
            return false;

        if (node.type != RunNodeType.Ending)
            return false;

        runSession.CommitVictoryAndAdvanceNode();

        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.StartCredits();

        return true;
    }

    private Sprite GetIconForNode(RunNodeType type)
    {
        switch (type)
        {
            case RunNodeType.Shop: return shopIcon;
            case RunNodeType.Boss: return bossIcon;
            case RunNodeType.Level:
            case RunNodeType.Event:
            default: return levelIcon;
        }
    }

    private string ResolveLabel(RunNode node)
    {
        if (node == null)
            return "";

        if (node.type == RunNodeType.Shop)
            return "SHOP";

        if (node.type == RunNodeType.Event)
            return "EVENT";

        if (!string.IsNullOrEmpty(node.levelId))
        {
            LevelCatalogService.LevelCatalogEntry meta;
            if (LevelCatalogService.TryGet(node.levelId, out meta) && !string.IsNullOrEmpty(meta.title))
                return meta.title;

            return node.levelId;
        }

        return "";
    }
}