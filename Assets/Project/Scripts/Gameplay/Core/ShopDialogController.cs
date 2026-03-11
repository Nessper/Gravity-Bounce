using System;
using UnityEngine;

public class ShopDialogController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Tooltip("Root qui contient ton UI shop (onglets, panels, etc). On l'active après le dialogue.")]
    [SerializeField] private GameObject shopUiPackageRoot;

    [Header("Dialog")]
    [SerializeField] private string welcomeSequenceId = "shop_welcome";

    private bool hasPlayedWelcome;

    // API EXISTANTE (ne casse rien)
    public void PlayWelcomeThenShowUI()
    {
        PlayWelcomeThenShowUI(null);
    }

    // NOUVELLE API (pour RunHubController)
    public void PlayWelcomeThenShowUI(Action onComplete)
    {
        if (hasPlayedWelcome)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        hasPlayedWelcome = true;
        HideShopUI();

        if (dialogSequenceRunner == null)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null || !dialogManager.IsReady || string.IsNullOrEmpty(welcomeSequenceId))
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        DialogSequence seq = dialogManager.GetSequenceById(welcomeSequenceId);
        if (seq == null)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        dialogSequenceRunner.Play(lines, onComplete: () =>
        {
            ShowShopUI();
            onComplete?.Invoke();
        });
    }

    private void HideShopUI()
    {
        if (shopUiPackageRoot != null)
            shopUiPackageRoot.SetActive(false);
    }

    private void ShowShopUI()
    {
        if (shopUiPackageRoot != null)
            shopUiPackageRoot.SetActive(true);
    }
}
