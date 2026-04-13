using TMPro;
using UnityEngine;

/// <summary>
/// Controleur principal du RunHub.
///
/// Responsabilites :
/// - Charger l'etat de run
/// - Mettre a jour le nom du monde
/// - Construire la liste des nodes visibles
/// - Router les actions principales du hub (Next / Menu)
///
/// Ce controller reste volontairement "haut niveau".
/// La logique specifique du shop est deleguee a RunHubShopController.
/// </summary>
public class RunHubController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("UI")]
    [SerializeField] private TMP_Text worldNameText;
    [SerializeField] private Transform listRoot;
    [SerializeField] private RunHubNodeView nodePrefab;

    [Header("Sub Controllers")]
    [SerializeField] private RunHubShopController shopController;

    [Header("Icons")]
    [SerializeField] private Sprite levelIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite bossIcon;

    private void Awake()
    {
        if (runSession != null)
            runSession.LoadFromSave();
    }

    private void Start()
    {
        if (runSession == null)
            return;

        RefreshHub();

        // Si on est deja sur un noeud de fin, on ne reste pas sur le hub.
        TryAutoRouteEnding();
    }

    /// <summary>
    /// Bouton NEXT depuis le hub.
    ///
    /// Regles :
    /// - Ending => route automatiquement vers Credits
    /// - Shop   => ouvre le shop
    /// - Sinon  => lance le niveau
    /// </summary>
    public void OnNextPressed()
    {
        if (!TryGetCurrentNode(out RunPlan plan, out int index, out RunNode node))
            return;

        // Securite : si on est sur Ending, on route tout de suite.
        if (TryAutoRouteEnding())
            return;

        if (node.type == RunNodeType.Shop)
        {
            if (shopController != null)
                shopController.OpenShop();
            else
                Debug.LogWarning("[RunHub] shopController is not assigned.");

            return;
        }

        // Level / Boss / Event route actuellement vers StartLevel.
        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.StartLevel();
    }

    /// <summary>
    /// Bouton NEXT depuis l'UI du shop.
    ///
    /// Le shop consomme le noeud Shop courant, avance la run,
    /// puis revient au hub sur le noeud suivant.
    /// </summary>
    public void OnShopNextPressed()
    {
        if (!TryGetCurrentNode(out RunPlan plan, out int index, out RunNode node))
            return;

        if (node.type != RunNodeType.Shop)
            return;

        runSession.CommitVictoryAndAdvanceNode();

        RefreshHub();

        if (shopController != null)
            shopController.CloseShopToHub();
    }

    /// <summary>
    /// Bouton MENU depuis le hub.
    /// </summary>
    public void OnMenuPressed()
    {
        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.GoToTitle();
    }

    /// <summary>
    /// Rafraichit les elements principaux du hub.
    /// </summary>
    private void RefreshHub()
    {
        UpdateWorldName();
        BuildList();

        if (shopController != null)
            shopController.ResetToRunHubState();
    }

    /// <summary>
    /// Met a jour le nom du monde affiche.
    /// </summary>
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
    /// Construit la liste visible des nodes du hub.
    ///
    /// Regle :
    /// - les noeuds Ending ne sont pas affiches dans la liste
    /// </summary>
    private void BuildList()
    {
        if (runSession == null || listRoot == null || nodePrefab == null)
            return;

        RunPlan plan = runSession.CurrentRunPlan;
        if (plan == null || !plan.HasNodes)
            return;

        int currentIndex = runSession.CurrentNodeIndex;

        ClearList();

        for (int i = 0; i < plan.nodes.Count; i++)
        {
            RunNode node = plan.nodes[i];
            if (node == null)
                continue;

            // Le hub n'affiche pas les noeuds Ending.
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
    /// Supprime proprement les vues existantes de la liste.
    /// </summary>
    private void ClearList()
    {
        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);
    }

    /// <summary>
    /// Si le noeud courant est un Ending :
    /// - on le consomme pour eviter une boucle
    /// - on route vers les credits
    /// </summary>
    private bool TryAutoRouteEnding()
    {
        if (!TryGetCurrentNode(out RunPlan plan, out int index, out RunNode node))
            return false;

        if (node.type != RunNodeType.Ending)
            return false;

        runSession.CommitVictoryAndAdvanceNode();

        if (BootRoot.GameFlow != null)
            BootRoot.GameFlow.StartCredits();

        return true;
    }

    /// <summary>
    /// Helper central pour recuperer le noeud courant de maniere sure.
    /// Evite de dupliquer les memes checks partout dans le controller.
    /// </summary>
    private bool TryGetCurrentNode(out RunPlan plan, out int index, out RunNode node)
    {
        plan = null;
        index = -1;
        node = null;

        if (runSession == null)
            return false;

        if (!runSession.EnsurePlanLoaded())
            return false;

        plan = runSession.CurrentRunPlan;
        if (plan == null || !plan.HasNodes)
            return false;

        index = runSession.CurrentNodeIndex;
        if (index < 0 || index >= plan.nodes.Count)
            return false;

        node = plan.nodes[index];
        return node != null;
    }

    /// <summary>
    /// Retourne l'icone adaptee au type de noeud.
    /// </summary>
    private Sprite GetIconForNode(RunNodeType type)
    {
        switch (type)
        {
            case RunNodeType.Shop:
                return shopIcon;

            case RunNodeType.Boss:
                return bossIcon;

            case RunNodeType.Level:
            case RunNodeType.Event:
            default:
                return levelIcon;
        }
    }

    /// <summary>
    /// Resout le label affiche pour un noeud.
    /// </summary>
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
            if (LevelCatalogService.TryGet(node.levelId, out LevelCatalogService.LevelCatalogEntry meta) &&
                !string.IsNullOrEmpty(meta.title))
            {
                return meta.title;
            }

            return node.levelId;
        }

        return "";
    }
}