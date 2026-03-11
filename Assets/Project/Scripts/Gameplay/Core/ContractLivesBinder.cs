using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gere l affichage des ContractLives dans le HUD.
/// - push initial au demarrage
/// - ecoute RunSessionState.OnContractLivesChanged
/// 
/// But: sortir cette responsabilite de LevelManager.
/// </summary>
public class ContractLivesBinder : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;
    [SerializeField] private ContractLivesUI contractLivesUI;

    private void OnEnable()
    {
        if (runSession != null)
            runSession.OnContractLivesChanged.AddListener(HandleContractLivesChanged);

        Refresh();
    }

    private void OnDisable()
    {
        if (runSession != null)
            runSession.OnContractLivesChanged.RemoveListener(HandleContractLivesChanged);
    }

    /// <summary>
    /// Force un refresh immediat depuis la valeur runtime.
    /// </summary>
    public void Refresh()
    {
        if (runSession == null || contractLivesUI == null)
            return;

        contractLivesUI.SetContractLives(runSession.ContractLives);
    }

    private void HandleContractLivesChanged(int lives)
    {
        if (contractLivesUI == null)
            return;

        contractLivesUI.SetContractLives(lives);
    }
}
