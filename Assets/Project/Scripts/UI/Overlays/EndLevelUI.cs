// EndLevelUI.cs (updated to display MainObjective line)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EndLevelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject endLevelOverlay;   // root container
    [SerializeField] private Transform panelContainer;     // Panel_Container

    [Header("Title / stats (Stats_Panel)")]
    [SerializeField] private TMP_Text timeInt;             // Timer_Time
    [SerializeField] private TMP_Text collectedInt;        // CollectedBalls_Int
    [SerializeField] private TMP_Text lostInt;             // LostBalls_Int
    [SerializeField] private TMP_Text scoreInt;            // Score_Int (raw score)

    [Header("Goals")]
    [SerializeField] private RectTransform goalsContent;   // Vertical Layout parent
    [SerializeField] private GameObject goalLinePrefab;    // prefab Goal_Panel (with Goal_Txt, Goal_Int)

    [Header("Combos")]
    [SerializeField] private RectTransform combosContent;  // Vertical Layout parent
    [SerializeField] private GameObject comboLinePrefab;   // prefab Combo_Panel

    [Header("Final score + bar (optional)")]
    [SerializeField] private TMP_Text scoreFinalInt;       // ScoreFinal_Int
    [SerializeField] private Image progressFill;           // ProgressBarFull_Img (Filled)
    [SerializeField] private RectTransform tickBronze, tickSilver, tickGold;
    [SerializeField] private int bronze = 600, silver = 1200, gold = 3000;

    private static readonly Color GrayText = new Color(0.6f, 0.6f, 0.6f, 1f);

    // --- Public API ---
    // New signature including main objective result
    public void Show(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        if (endLevelOverlay) endLevelOverlay.SetActive(true);

        // Base stats
        if (timeInt) timeInt.text = $"{stats.TimeElapsedSec}s";
        if (collectedInt) collectedInt.text = stats.BallsCollected.ToString();
        if (lostInt) lostInt.text = stats.BallsLost.ToString();
        if (scoreInt) scoreInt.text = stats.RawScore.ToString("N0");

        // Goals (clear then add the main objective line)
        if (goalsContent) ClearChildren(goalsContent);
        AddMainObjectiveLine(mainObj);

        // Optionally place ticks if you use score thresholds
        if (levelData != null && levelData.ScoreGoals != null && levelData.ScoreGoals.Length > 0)
        {
            foreach (var g in levelData.ScoreGoals)
            {
                if (g.Type == "Bronze") PlaceTick(tickBronze, g.Points);
                else if (g.Type == "Silver") PlaceTick(tickSilver, g.Points);
                else if (g.Type == "Gold") PlaceTick(tickGold, g.Points);
            }
        }

        // Final score (optional)
        if (scoreFinalInt) scoreFinalInt.text = stats.FinalScore.ToString("N0");
        if (progressFill && gold > 0)
        {
            float v = Mathf.Clamp01((float)stats.FinalScore / gold);
            progressFill.fillAmount = v;
        }
    }

    public void Hide()
    {
        if (endLevelOverlay) endLevelOverlay.SetActive(false);
    }

    // --- Helpers ---
    private void AddMainObjectiveLine(MainObjectiveResult mainObj)
    {
        if (!goalsContent || !goalLinePrefab) return;

        var go = Instantiate(goalLinePrefab, goalsContent);
        TMP_Text txt = null;
        TMP_Text val = null;

        // Try to find by expected child names; fallback to first TMP_Texts
        var txtT = go.transform.Find("Goal_Txt");
        var valT = go.transform.Find("Goal_Int");
        if (txtT) txt = txtT.GetComponent<TMP_Text>();
        if (valT) val = valT.GetComponent<TMP_Text>();
        if (txt == null || val == null)
        {
            var tmps = go.GetComponentsInChildren<TMP_Text>(true);
            if (tmps.Length >= 2)
            {
                // Heuristic: first = label, last = value
                txt = tmps[0];
                val = tmps[tmps.Length - 1];
            }
        }

        if (txt) txt.text = mainObj.Text;
        if (val) val.text = mainObj.BonusApplied.ToString();

        var color = mainObj.Achieved ? Color.white : GrayText;
        if (txt) txt.color = color;
        if (val) val.color = color;
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (!parent) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    private void PlaceTick(RectTransform tick, int threshold)
    {
        if (!tick || gold <= 0) return;
        float x = Mathf.Clamp01((float)threshold / gold);
        var a = tick.anchorMin; var b = tick.anchorMax; a.x = b.x = x;
        tick.anchorMin = a; tick.anchorMax = b; tick.anchoredPosition = Vector2.zero;
    }
}

// Expected DTO provided by LevelManager when calling Show(...)
[System.Serializable]
public struct MainObjectiveResult
{
    public string Text;
    public int ThresholdPct;   // 0–100
    public int Required;       // ceil(pct * spawnedForEval)
    public int Collected;      // collected count
    public bool Achieved;      // Collected >= Required
    public int BonusApplied;   // Achieved ? Bonus : 0
}
