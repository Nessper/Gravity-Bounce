using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreUI : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private TextMeshProUGUI label;   // texte du score
    [SerializeField] private Slider progressBar;      // ta barre PB_Balles
    [SerializeField] private ScoreManager scoreManager;

    // appelée par ScoreManager via le listener
    public void UpdateLabel(int newScore)
    {
        if (label != null)
            label.text = newScore.ToString();

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (progressBar == null || scoreManager == null)
            return;

        var planned = Mathf.Max(1, scoreManager.TotalBillesPrevues);
        float ratio = Mathf.Clamp01((float)scoreManager.TotalBilles / planned);
        progressBar.value = ratio;
    }
}
