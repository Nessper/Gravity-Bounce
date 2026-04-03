using System;
using UnityEngine;

/// <summary>
/// Gère le dialogue d'entrée dans le shop.
///
/// Responsabilités :
/// - Joue un dialogue d'accueil (une seule fois).
/// - Cache l'UI du shop pendant le dialogue.
/// - Affiche l'UI du shop une fois le dialogue terminé.
/// - Notifie un éventuel callback à la fin.
///
/// Comportement :
/// Le dialogue est en mode INTERACTIF (clic pour avancer).
/// - Si déjà joué une fois, on affiche directement le shop sans rejouer le dialogue.
/// </summary>
public class ShopDialogController : MonoBehaviour
{
    // ============================
    // REFS
    // ============================
    [Header("References")]

    [Tooltip("Runner centralisé des dialogues.")]
    [SerializeField] private DialogSequenceRunner dialogSequenceRunner;

    [Tooltip("Root qui contient toute l'UI du shop (tabs, panels...).")]
    [SerializeField] private GameObject shopUiPackageRoot;


    // ============================
    // CONFIG
    // ============================
    [Header("Dialog")]

    [Tooltip("ID de la séquence de dialogue d'accueil (ex: 'shop_welcome').")]
    [SerializeField] private string welcomeSequenceId = "shop_welcome";


    // ============================
    // STATE
    // ============================
    private bool hasPlayedWelcome;


    // ============================
    // PUBLIC API
    // ============================

    /// <summary>
    /// Version simple sans callback.
    /// </summary>
    public void PlayWelcomeThenShowUI()
    {
        PlayWelcomeThenShowUI(null);
    }

    /// <summary>
    /// Lance le dialogue d'accueil puis affiche l'UI du shop.
    /// </summary>
    public void PlayWelcomeThenShowUI(Action onComplete)
    {
        // Si déjà joué une fois -> on ne rejoue pas le dialogue
        if (hasPlayedWelcome)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        hasPlayedWelcome = true;

        // On cache le shop pendant le dialogue
        HideShopUI();

        // Sécurité : si pas de runner -> fallback direct
        if (dialogSequenceRunner == null)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        // Récupération du DialogManager
        DialogManager dialogManager = UnityEngine.Object.FindFirstObjectByType<DialogManager>();
        if (dialogManager == null || !dialogManager.IsReady || string.IsNullOrEmpty(welcomeSequenceId))
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        // Récupération de la séquence
        DialogSequence seq = dialogManager.GetSequenceById(welcomeSequenceId);
        if (seq == null)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        // Récupération des lignes (variant aléatoire si applicable)
        DialogLine[] lines = dialogManager.GetRandomVariantLines(seq);
        if (lines == null || lines.Length == 0)
        {
            ShowShopUI();
            onComplete?.Invoke();
            return;
        }

        // ============================
        // IMPORTANT : MODE INTERACTIF
        // ============================
        dialogSequenceRunner.Play(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () =>
            {
                ShowShopUI();
                onComplete?.Invoke();
            }
        );
    }


    // ============================
    // UI HELPERS
    // ============================

    /// <summary>
    /// Cache complètement l'UI du shop.
    /// </summary>
    private void HideShopUI()
    {
        if (shopUiPackageRoot != null)
            shopUiPackageRoot.SetActive(false);
    }

    /// <summary>
    /// Affiche l'UI du shop.
    /// </summary>
    private void ShowShopUI()
    {
        if (shopUiPackageRoot != null)
            shopUiPackageRoot.SetActive(true);
    }
}