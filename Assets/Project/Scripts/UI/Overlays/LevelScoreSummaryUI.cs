using UnityEngine;
using TMPro;

/// <summary>
/// Affiche le récapitulatif de score pour un niveau :
/// - Score du niveau (score courant)
/// - Soit un label "NEW BEST SCORE"
/// - Soit "BEST SCORE : X" avec le best persistant.
/// 
/// Utilisable autant sur l'écran de victoire que sur un écran de défaite.
/// </summary>
public class LevelScoreSummaryUI : MonoBehaviour
{
    [Header("Level score")]
    [SerializeField] private TMP_Text levelScoreValue;
    // levelScoreValue : texte affichant le score de CE run pour le niveau.

    [Header("Best score (persistant)")]
    [SerializeField] private TMP_Text bestScoreLabel;
    [SerializeField] private TMP_Text bestScoreValue;
    // bestScoreLabel : label type "BEST SCORE".
    // bestScoreValue : valeur du meilleur score persistant.

    [Header("New best")]
    [SerializeField] private TMP_Text newBestLabel;
    // newBestLabel : texte "NEW BEST SCORE" (ou équivalent).

    /// <summary>
    /// Configure le bloc avec :
    /// - levelScore : score de la run courante sur ce niveau
    /// - bestScore : best persistant (avant ou après update, à toi de choisir)
    /// - isNewBest : true si levelScore bat le bestScore persistant.
    /// </summary>
    public void Setup(int levelScore, int bestScore, bool isNewBest)
    {
        // On s'assure que le bloc est visible
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Score du niveau
        if (levelScoreValue != null)
            levelScoreValue.text = levelScore.ToString("N0");

        if (isNewBest)
        {
            // Nouveau record : on affiche uniquement "NEW BEST SCORE"
            if (newBestLabel != null)
                newBestLabel.gameObject.SetActive(true);

            if (bestScoreLabel != null)
                bestScoreLabel.gameObject.SetActive(false);

            if (bestScoreValue != null)
                bestScoreValue.gameObject.SetActive(false);
        }
        else
        {
            // Pas de nouveau record : on affiche "BEST SCORE : X"
            if (newBestLabel != null)
                newBestLabel.gameObject.SetActive(false);

            if (bestScoreLabel != null)
            {
                bestScoreLabel.gameObject.SetActive(true);
                bestScoreLabel.text = "BEST SCORE";
            }

            if (bestScoreValue != null)
            {
                bestScoreValue.gameObject.SetActive(true);
                bestScoreValue.text = bestScore.ToString("N0");
            }
        }
    }

    /// <summary>
    /// Optionnel : reset visuel si besoin au début d'une scène.
    /// </summary>
    public void ResetVisual()
    {
        if (levelScoreValue != null)
            levelScoreValue.text = "0";

        if (bestScoreValue != null)
            bestScoreValue.text = string.Empty;

        if (bestScoreLabel != null)
            bestScoreLabel.gameObject.SetActive(false);

        if (newBestLabel != null)
            newBestLabel.gameObject.SetActive(false);
    }
}
