using UnityEngine;

/// <summary>
/// Pont entre le ScoreManager et l'affichage du score dans le HUD.
/// Ne s'occupe pas de la logique de score :
/// - Reçoit une valeur entière depuis ScoreManager.onScoreChanged.
/// - La transmet à un AnimatedIntText pour un affichage animé.
/// </summary>
public class ScoreUI : MonoBehaviour
{
    [SerializeField] private AnimatedIntText animatedScore;
    // Référence vers le composant responsable d'afficher le score avec animation.
    // Doit être associé au TMP_Text qui affiche le score.

    /// <summary>
    /// Appelé par ScoreManager.onScoreChanged à chaque changement de score.
    /// Déclenche une animation de la valeur affichée vers la nouvelle valeur.
    /// </summary>
    public void UpdateScoreText(int value)
    {
        if (animatedScore != null)
        {
            animatedScore.AnimateTo(value);
        }
    }

    /// <summary>
    /// Permet de fixer immédiatement le score affiché (sans animation),
    /// utile au démarrage d'un niveau ou lors d'un reset.
    /// </summary>
    public void SetInitialScore(int value)
    {
        if (animatedScore != null)
        {
            animatedScore.SetInstant(value);
        }
    }
}
