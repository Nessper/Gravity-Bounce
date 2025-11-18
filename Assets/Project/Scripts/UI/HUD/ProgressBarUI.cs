using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de barre de progression des billes collectées par rapport au plan du niveau.
/// 
/// Rôle :
/// - Affiche la progression globale des billes collectées (TotalBilles / plannedTotalBalls).
/// - Place un marqueur visuel sur la barre pour l'objectif principal (ThresholdCount).
/// - Anime le remplissage via AnimatedFillImage.
/// 
/// Ce composant ne gère pas la logique de score.
/// Il lit les données dans ScoreManager et se contente de refléter l'état courant en UI.
/// </summary>
public class ProgressBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    // Référence vers le ScoreManager, pour lire TotalBilles et écouter les changements de score.

    [SerializeField] private AnimatedFillImage animatedFill;
    // Composant responsable d'animer le fillAmount de l'image de barre.
    // Doit être associé à l'Image de la barre (celle qui a un fillAmount).

    [SerializeField] private Image goalMarkerImage = null;
    // Trait vertical (facultatif) indiquant le seuil de l'objectif principal sur la barre.

    [Header("Données fixes du niveau")]
    [SerializeField] private int plannedTotalBalls = 1;
    // Nombre total de billes prévues pour le niveau (plan théorique, JSON).

    [SerializeField] private int objectiveThreshold = 0;
    // Nombre de billes à atteindre pour l'objectif principal (ThresholdCount).

    // Indique si la barre a été configurée pour ce niveau.
    private bool isConfigured;

    // Indique si le listener ScoreManager.onScoreChanged est branché.
    private bool attached;

    private void OnEnable()
    {
        // Quand l'objet est réactivé, on rafraîchit si on était déjà attaché.
        if (attached)
            Refresh();
    }

    private void OnDisable()
    {
        // On se désabonne proprement des events du ScoreManager quand la barre est désactivée.
        if (attached && scoreManager != null)
            scoreManager.onScoreChanged.RemoveListener(HandleScoreChanged);

        attached = false;
    }

    /// <summary>
    /// Configure la barre de progression pour ce niveau.
    /// Appelée une fois au début du niveau par le contrôleur (LevelManager).
    /// 
    /// plannedTotalBalls  = nombre total de billes prévues sur le niveau.
    /// objectiveThreshold = nombre de billes à atteindre pour l'objectif principal.
    /// </summary>
    public void Configure(int plannedTotalBalls, int objectiveThreshold)
    {
        this.plannedTotalBalls = Mathf.Max(1, plannedTotalBalls);
        this.objectiveThreshold = Mathf.Max(0, objectiveThreshold);

        isConfigured = true;

        // Branchement sur l'event de score si ce n'est pas déjà fait.
        if (!attached && scoreManager != null)
        {
            scoreManager.onScoreChanged.AddListener(HandleScoreChanged);
            attached = true;
        }

        // Met à jour la position du marqueur d'objectif (trait vertical).
        UpdateGoalMarker();

        // Met à jour la barre avec l'état actuel.
        Refresh();
    }

    /// <summary>
    /// Forçage visuel de la barre (appelé par LevelManager après Configure
    /// et par BallSpawner.OnActivated pour refléter la progression en runtime).
    /// </summary>
    public void Refresh()
    {
        int currentScore = scoreManager != null ? scoreManager.CurrentScore : 0;
        HandleScoreChanged(currentScore);
    }

    /// <summary>
    /// Callback appelé lorsqu'un changement de score est notifié par ScoreManager.
    /// On ne se sert pas ici de la valeur du score, mais des billes collectées.
    /// </summary>
    private void HandleScoreChanged(int _)
    {
        if (!isConfigured || scoreManager == null)
        {
            UpdateFill(0f);
            return;
        }

        if (plannedTotalBalls <= 0)
        {
            UpdateFill(0f);
            return;
        }

        int collected = scoreManager.TotalBilles;
        float t = Mathf.Clamp01(collected / (float)plannedTotalBalls);

        UpdateFill(t);
    }

    /// <summary>
    /// Met à jour le remplissage visuel de la barre.
    /// Si AnimatedFillImage est disponible, on anime vers la nouvelle valeur.
    /// </summary>
    /// <param name="t">Ratio de remplissage entre 0 et 1.</param>
    private void UpdateFill(float t)
    {
        float clamped = Mathf.Clamp01(t);

        if (animatedFill != null)
        {
            // Animation vers la nouvelle valeur.
            animatedFill.AnimateTo01(clamped);
        }
        else
        {
            // Fallback de sécurité : si animatedFill n'est pas assigné,
            // on ne fait rien (mais on pourrait logguer un warning si nécessaire).
            // L'ancien comportement direct sur fillImage a été retiré.
        }
    }

    /// <summary>
    /// Place un trait vertical sur la barre, à la position objectiveThreshold / plannedTotalBalls.
    /// Hypothèse simple : barre horizontale, left->right, pivot du conteneur centré.
    /// </summary>
    private void UpdateGoalMarker()
    {
        if (goalMarkerImage == null)
            return;

        RectTransform markerRect = goalMarkerImage.rectTransform;
        RectTransform barRect = GetComponent<RectTransform>();

        if (objectiveThreshold <= 0 || plannedTotalBalls <= 0)
        {
            markerRect.gameObject.SetActive(false);
            return;
        }

        markerRect.gameObject.SetActive(true);

        // t = ratio de l'objectif principal par rapport au total prévu.
        float t = Mathf.Clamp01(objectiveThreshold / (float)plannedTotalBalls);
        float barWidth = barRect.rect.width;

        // Pivot du BarContainer supposé à 0.5 (centre).
        // t=0 -> bord gauche, t=1 -> bord droit.
        float xLocal = (t - 0.5f) * barWidth;

        Vector2 anchored = markerRect.anchoredPosition;
        anchored.x = xLocal;
        markerRect.anchoredPosition = anchored;
    }
}
