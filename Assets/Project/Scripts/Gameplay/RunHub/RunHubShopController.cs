using UnityEngine;

/// <summary>
/// Controleur dedie au sous-systeme shop du RunHub.
///
/// Responsabilites :
/// - Jouer la transition vers le shop
/// - Lancer le dialogue de bienvenue du shop
/// - Reveler l'UI shop apres le dialogue
/// - Revenir a l'etat visuel RunHub
///
/// Ce controller ne decide PAS quand ouvrir le shop :
/// cette decision reste dans RunHubController.
/// </summary>
public class RunHubShopController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunHubShopTransition shopTransition;
    [SerializeField] private ShopDialogController shopDialogController;

    private void Awake()
    {
        if (shopTransition != null)
            shopTransition.OnShopTransitionCompleted = HandleShopPanelRevealed;
    }

    /// <summary>
    /// Ouvre le shop depuis le hub.
    /// </summary>
    public void OpenShop()
    {
        if (shopTransition != null)
            shopTransition.PlayToShopTransition();
        else
            HandleShopPanelRevealed();
    }

    /// <summary>
    /// Remet l'UI dans l'etat standard du hub.
    /// </summary>
    public void ResetToRunHubState()
    {
        if (shopTransition != null)
            shopTransition.RestoreRunHubState();
    }

    /// <summary>
    /// Ferme le shop et revient a l'etat hub.
    /// </summary>
    public void CloseShopToHub()
    {
        if (shopTransition != null)
            shopTransition.RestoreRunHubState();
    }

    /// <summary>
    /// Appel interne lorsque le panel shop est pret a etre revele.
    /// </summary>
    private void HandleShopPanelRevealed()
    {
        if (shopDialogController == null)
        {
            Debug.LogWarning("[RunHubShopController] shopDialogController is not assigned.");

            if (shopTransition != null)
                shopTransition.ShowShopUiAfterDialog();

            return;
        }

        shopDialogController.PlayWelcomeThenShowUI(() =>
        {
            if (shopTransition != null)
                shopTransition.ShowShopUiAfterDialog();
        });
    }
}