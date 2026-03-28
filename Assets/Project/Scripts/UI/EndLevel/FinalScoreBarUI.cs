using UnityEngine;

/// <summary>
/// Contrôleur de la barre de score final.
/// - Met à jour la barre segmentée
/// - Répercute la médaille réellement affichée par la barre animée
///   vers EndLevelMedalsUI
/// </summary>
public class FinalScoreBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private SegmentedFinalScoreBarUI segmentedBar;
    [SerializeField] private EndLevelMedalsUI medalsUI;

    [Header("Runtime")]
    [SerializeField] private int progressMax = 0;

    private int currentScore = 0;
    private int bronzeThreshold = 0;
    private int silverThreshold = 0;
    private int goldThreshold = 0;

    public int ProgressMax => progressMax;
    public int CurrentScore => currentScore;

    private void Awake()
    {
        if (segmentedBar == null)
            segmentedBar = GetComponentInChildren<SegmentedFinalScoreBarUI>();

        if (medalsUI == null)
            medalsUI = GetComponentInChildren<EndLevelMedalsUI>(true);
    }

    private void OnEnable()
    {
        if (segmentedBar != null)
            segmentedBar.OnDisplayedMedalChanged += HandleDisplayedMedalChanged;
    }

    private void OnDisable()
    {
        if (segmentedBar != null)
            segmentedBar.OnDisplayedMedalChanged -= HandleDisplayedMedalChanged;
    }

    public void Configure(int bronzeThreshold, int silverThreshold, int goldThreshold, int maxScore)
    {
        this.bronzeThreshold = Mathf.Max(0, bronzeThreshold);
        this.silverThreshold = Mathf.Max(0, silverThreshold);
        this.goldThreshold = Mathf.Max(0, goldThreshold);

        progressMax = Mathf.Max(1, maxScore);

        if (segmentedBar != null)
        {
            segmentedBar.SetThresholdsFromGoals(
                this.bronzeThreshold,
                this.silverThreshold,
                this.goldThreshold,
                progressMax);
        }

        ResetInstant();
    }

    public void ResetInstant()
    {
        currentScore = 0;

        if (segmentedBar != null)
            segmentedBar.ResetInstant();

        if (medalsUI != null)
            medalsUI.ResetInstant();
    }

    public void SetScore(int newScore)
    {
        currentScore = Mathf.Max(0, newScore);

        if (segmentedBar == null || progressMax <= 0)
            return;

        float ratio = Mathf.Clamp01((float)currentScore / progressMax);
        segmentedBar.SetProgress01(ratio);
    }

    private void HandleDisplayedMedalChanged(EndMedal medal)
    {
        if (medalsUI == null)
            return;

        medalsUI.SetDisplayedMedalInstant(medal);
    }
}