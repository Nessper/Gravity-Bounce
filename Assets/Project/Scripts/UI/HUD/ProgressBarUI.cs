using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de barre de progression des billes collectées par rapport au plan du niveau.
///
/// Rôle :
/// - Affiche la progression globale des billes collectées (TotalBilles / plannedTotalBalls).
/// - Place un marqueur visuel sur la barre pour l'objectif principal (ThresholdCount).
/// - Anime le remplissage via AnimatedFillImage.
/// - Déclenche une petite animation sur le marqueur lorsqu'on atteint l'objectif principal.
///
/// Ce composant ne gère pas la logique de score.
/// Il lit les données dans ScoreManager et se contente de refléter l'état courant en UI.
/// </summary>
public class ProgressBarUI : MonoBehaviour
{
    // --------------------------------------------------------------------
    // RÉFÉRENCES
    // --------------------------------------------------------------------

    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    // Référence vers le ScoreManager, pour lire TotalBilles et écouter les changements de score.

    [SerializeField] private AnimatedFillImage animatedFill;
    // Composant responsable d'animer le fillAmount de l'image de barre.
    // Doit être associé à l'Image de la barre (celle qui a un fillAmount).

    [SerializeField] private Image goalMarkerImage = null;
    // RectTransform (Image) utilisé comme conteneur du marqueur de seuil.
    // C'est cet objet qui est positionné sur la barre.

    [Header("Animation seuil objectif")]
    [SerializeField] private ThresholdPulseUI goalMarkerPulse = null;
    // Composant optionnel chargé de gérer l'animation du marqueur (swap d'icône + scale).

    [SerializeField] private bool playPulseOnReach = true;
    // Si true, déclenche l'animation du marqueur lorsqu'on atteint l'objectif principal.

    // --------------------------------------------------------------------
    // DONNÉES FIXES DU NIVEAU
    // --------------------------------------------------------------------

    [Header("Données fixes du niveau")]
    [SerializeField] private int plannedTotalBalls = 1;
    // Nombre total de billes prévues pour le niveau (plan théorique, JSON).

    [SerializeField] private int objectiveThreshold = 0;
    // Nombre de billes à atteindre pour l'objectif principal (ThresholdCount).

    // --------------------------------------------------------------------
    // ÉTAT INTERNE
    // --------------------------------------------------------------------

    // Indique si la barre a été configurée pour ce niveau.
    private bool isConfigured;

    // Indique si le listener ScoreManager.onScoreChanged est branché.
    private bool attached;

    // Indique si l'animation du seuil a déjà été jouée.
    private bool goalPulsePlayed;

    // --------------------------------------------------------------------
    // CYCLE UNITY
    // --------------------------------------------------------------------

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

    // --------------------------------------------------------------------
    // CONFIGURATION
    // --------------------------------------------------------------------

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
        goalPulsePlayed = false;

        // Branchement sur l'event de score si ce n'est pas déjà fait.
        if (!attached && scoreManager != null)
        {
            scoreManager.onScoreChanged.AddListener(HandleScoreChanged);
            attached = true;
        }

        // Met à jour la position du marqueur d'objectif (trait / icône).
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

    // --------------------------------------------------------------------
    // RÉACTION AUX CHANGEMENTS DE SCORE
    // --------------------------------------------------------------------

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

        // Progression globale basée sur le nombre total de billes collectées.
        int collected = scoreManager.TotalBilles;
        float t = Mathf.Clamp01(collected / (float)plannedTotalBalls);

        // Si on a un objectif principal défini et qu'on vient de l'atteindre ou dépasser,
        // on déclenche une fois l'animation du marqueur.
        if (!goalPulsePlayed &&
     playPulseOnReach &&
     objectiveThreshold > 0 &&
     collected >= objectiveThreshold)
        {
            goalPulsePlayed = true;

            if (goalMarkerPulse != null)
                goalMarkerPulse.PlayReached();
        }


        UpdateFill(t);
    }

    // --------------------------------------------------------------------
    // MISE À JOUR VISUELLE
    // --------------------------------------------------------------------

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
        }
    }

    /// <summary>
    /// Place un marqueur sur la barre, à la position objectiveThreshold / plannedTotalBalls.
    /// Hypothèse simple : barre horizontale, left->right, pivot du conteneur centré.
    /// </summary>
    private void UpdateGoalMarker()
    {
        if (goalMarkerImage == null)
            return;

        // markerRect = l'Image par défaut (Icon_Default)
        RectTransform markerRect = goalMarkerImage.rectTransform;
        RectTransform barRect = GetComponent<RectTransform>();

        if (objectiveThreshold <= 0 || plannedTotalBalls <= 0)
        {
            // On désactive le conteneur complet s'il existe, sinon juste l'icône
            Transform parent = markerRect.parent;
            if (parent != null)
                parent.gameObject.SetActive(false);
            else
                markerRect.gameObject.SetActive(false);

            return;
        }

        // On s'assure que le conteneur est actif
        Transform markerParent = markerRect.parent;
        if (markerParent != null)
            markerParent.gameObject.SetActive(true);
        else
            markerRect.gameObject.SetActive(true);

        // t = ratio de l'objectif principal par rapport au total prévu.
        float t = Mathf.Clamp01(objectiveThreshold / (float)plannedTotalBalls);
        float barWidth = barRect.rect.width;

        // Pivot du BarContainer supposé à 0.5 (centre).
        // t=0 -> bord gauche, t=1 -> bord droit.
        float xLocal = (t - 0.5f) * barWidth;

        // IMPORTANT :
        // On déplace le PARENT si possible (GoalMarkerContainer), pas seulement l'icône.
        RectTransform targetRect = markerParent as RectTransform ?? markerRect;

        Vector2 anchored = targetRect.anchoredPosition;
        anchored.x = xLocal;
        targetRect.anchoredPosition = anchored;
    }

}
