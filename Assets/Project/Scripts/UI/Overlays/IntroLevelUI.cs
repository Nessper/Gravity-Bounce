using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;

public class IntroLevelUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject overlayIntro; // ton panel complet
    [SerializeField] private TMP_Text levelIdText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text objectifText;
    [SerializeField] private TMP_Text objectifText2;
    [SerializeField] private TMP_Text objectifText3;
    [SerializeField] private TMP_Text objectifText4;
    [SerializeField] private TMP_Text objectifText5;
    [SerializeField] private TMP_Text levelDurationSec;
    [SerializeField] private TMP_Text lives;
    [SerializeField] private TMP_Text tip;
    [SerializeField] private TMP_Text bronzeGoalText;
    [SerializeField] private TMP_Text silverGoalText;
    [SerializeField] private TMP_Text goldGoalText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;

    private System.Action onPlayCallback;
    private System.Action onBackCallback;

    private void Awake()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    public void Show(LevelData data, System.Action onPlay, System.Action onBack)
    {
        if (data == null) return;

        onPlayCallback = onPlay;
        onBackCallback = onBack;

        if (levelIdText) levelIdText.text = data.LevelID.ToString();
        if (titleText) titleText.text = data.Title;
        if (objectifText) objectifText.text = data.MainObjective.Text;
       
        if (levelDurationSec) levelDurationSec.text = data.LevelDurationSec.ToString();
        if (lives) lives.text = "x"+data.Lives.ToString();
        if (tip) tip.text = data.Tip.ToString();
        if (data.ScoreGoals != null && data.ScoreGoals.Length >= 3)
        {
            // format fr-FR (espaces fines pour milliers)
            var fr = new CultureInfo("fr-FR");

            if (bronzeGoalText) bronzeGoalText.text = data.ScoreGoals[0].Points.ToString("N0", fr);
            if (silverGoalText) silverGoalText.text = data.ScoreGoals[1].Points.ToString("N0", fr);
            if (goldGoalText) goldGoalText.text = data.ScoreGoals[2].Points.ToString("N0", fr);
        }
        else
        {
            // fallback propre si JSON incomplet
            if (bronzeGoalText) bronzeGoalText.text = "-";
            if (silverGoalText) silverGoalText.text = "-";
            if (goldGoalText) goldGoalText.text = "-";
        }

        if (overlayIntro != null)
            overlayIntro.SetActive(true);
    }

    public void Hide()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);
    }

    private void OnPlayClicked()
    {
        onPlayCallback?.Invoke();
    }

    private void OnBackClicked()
    {
        onBackCallback?.Invoke();
    }
}
