using System;
using UnityEngine;

/// <summary>
/// Gere le dialogue d entree dans le shop.
///
/// Responsabilites :
/// - jouer le dialogue d accueil une seule fois
/// - cacher l UI du shop pendant le dialogue
/// - afficher l UI du shop une fois le dialogue termine
/// - notifier un callback optionnel a la fin
///
/// Comportement :
/// - le dialogue est joue en mode interactif
/// - si deja joue, le shop s ouvre directement
/// </summary>
public class ShopDialogController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Runner centralise des dialogues.")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Tooltip("Root qui contient toute l UI du shop.")]
    [SerializeField] private GameObject shopUiPackageRoot;

    [Header("Dialog")]
    [Tooltip("ID de la sequence de dialogue d accueil (ex: 'shop_welcome').")]
    [SerializeField] private string welcomeSequenceId = "shop_welcome";

    private bool hasPlayedWelcome;

    private LocalizationManager Loc => LocalizationManager.Instance;

    /// <summary>
    /// Version simple sans callback.
    /// </summary>
    public void PlayWelcomeThenShowUI()
    {
        PlayWelcomeThenShowUI(null);
    }

    /// <summary>
    /// Lance le dialogue d accueil puis affiche l UI du shop.
    /// </summary>
    public void PlayWelcomeThenShowUI(Action onComplete)
    {
        if (hasPlayedWelcome)
        {
            CompleteWelcomeFlow(onComplete);
            return;
        }

        hasPlayedWelcome = true;
        HideShopUI();

        DialogLine[] lines = TryResolveWelcomeLines();
        if (lines == null)
        {
            CompleteWelcomeFlow(onComplete);
            return;
        }

        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () => CompleteWelcomeFlow(onComplete)
        );
    }

    /// <summary>
    /// Resout les lignes du dialogue d accueil.
    /// Retourne null si la sequence ne peut pas etre jouee.
    /// </summary>
    private DialogLine[] TryResolveWelcomeLines()
    {
        if (dialogSequenceRunner == null)
        {
            Debug.LogError("[ShopDialogController] DialogSequenceRunner manquant.");
            return null;
        }

        if (Loc == null)
        {
            Debug.LogError("[ShopDialogController] LocalizationManager.Instance est null.");
            return null;
        }

        if (!Loc.IsReady)
        {
            Debug.LogError("[ShopDialogController] LocalizationManager non pret.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(welcomeSequenceId))
        {
            Debug.LogError("[ShopDialogController] welcomeSequenceId vide.");
            return null;
        }

        DialogSequence sequence = Loc.GetSequenceById(welcomeSequenceId);
        if (sequence == null)
        {
            Debug.LogError("[ShopDialogController] Sequence introuvable : " + welcomeSequenceId);
            return null;
        }

        DialogLine[] lines = Loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
        {
            Debug.LogError("[ShopDialogController] Sequence vide : " + welcomeSequenceId);
            return null;
        }

        return lines;
    }

    /// <summary>
    /// Termine le flow d accueil du shop.
    /// </summary>
    private void CompleteWelcomeFlow(Action onComplete)
    {
        ShowShopUI();
        onComplete?.Invoke();
    }

    /// <summary>
    /// Cache completement l UI du shop.
    /// </summary>
    private void HideShopUI()
    {
        if (shopUiPackageRoot != null)
            shopUiPackageRoot.SetActive(false);
    }

    /// <summary>
    /// Affiche l UI du shop.
    /// </summary>
    private void ShowShopUI()
    {
        if (shopUiPackageRoot != null)
            shopUiPackageRoot.SetActive(true);
    }
}