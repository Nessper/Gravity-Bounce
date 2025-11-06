using UnityEngine;
using UnityEngine.UI;

public class ProgressBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private Image fillImage;              // Image (Filled Horizontal) – remplissage courant
    [SerializeField] private Image goalMarkerImage = null; // Optionnel : une 2e image pour afficher le seuil objectif (type Filled, Raycast off)

    [Header("Données fixes du niveau")]
    [SerializeField] private int plannedTotalBalls = 1;    // total prévu (JSON)
    [SerializeField] private int objectifPourcentage = 100;// pour affichage repère

    private bool isConfigured;
    private bool attached;

    private void OnEnable()
    {
        if (attached) Refresh();
    }

    private void OnDisable()
    {
        if (attached && scoreManager != null)
            scoreManager.onScoreChanged.RemoveListener(HandleScoreChanged);
        attached = false;
    }

    /// <summary>Appelée une fois au début du niveau par LevelManager.</summary>
    /// <param name="plannedTotalBalls">Total de billes prévues sur le niveau (toutes phases confondues si c’est la référence voulue).</param>
    /// <param name="objectifPourcentage">Seuil objectif (affichage repère), 1..100.</param>
    public void Configure(int plannedTotalBalls, int objectifPourcentage)
    {
        this.plannedTotalBalls = Mathf.Max(1, plannedTotalBalls);
        this.objectifPourcentage = Mathf.Clamp(objectifPourcentage, 1, 100);

        isConfigured = true;

        // Abonnement maintenant seulement
        if (!attached && scoreManager != null)
        {
            scoreManager.onScoreChanged.AddListener(HandleScoreChanged);
            attached = true;
        }

        // Place le repère d’objectif si une image est fournie
        UpdateGoalMarker();

        Refresh();
    }

    public void Refresh()
    {
        HandleScoreChanged(scoreManager != null ? scoreManager.CurrentScore : 0);
    }

    private void HandleScoreChanged(int _)
    {
        if (!isConfigured || scoreManager == null || fillImage == null)
        {
            UpdateFill(0f);
            return;
        }

        // >>> Mode attendu : Full = total de billes prévues <<<
        int collected = scoreManager.TotalBilles; // Assure-toi que c’est bien "billes collectées" (pas "spawns" ni "perdues")
        float t = Mathf.Clamp01(collected / (float)plannedTotalBalls);

        UpdateFill(t);
    }

    private void UpdateFill(float t)
    {
        fillImage.fillAmount = t;
    }

    private void UpdateGoalMarker()
    {
        if (goalMarkerImage == null) return;

        // On affiche un ghost-fill à la hauteur de l’objectif (ex: 0.6 pour 60%)
        float goalT = objectifPourcentage / 100f;
        goalMarkerImage.fillAmount = Mathf.Clamp01(goalT);
        // Astuce UI : mettre cette image SOUS le fill principal, opacité 35–50%
        // pour un repère visuel clair.
    }
}
