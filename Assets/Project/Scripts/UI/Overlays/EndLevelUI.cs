using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class EndLevelUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LevelManager levelManagerRef;
    [SerializeField] private bool autoBindLevelManager = true;

    [Header("Panels")]
    [SerializeField] private GameObject endLevelOverlay;
    [SerializeField] private Transform panelContainer;

    [Header("Title / stats (Stats_Panel)")]
    [SerializeField] private TMP_Text collectedInt;
    [SerializeField] private TMP_Text lostInt;
    [SerializeField] private TMP_Text scoreInt;

    [Header("Goals")]
    [SerializeField] private RectTransform goalsContent;
    [SerializeField] private GameObject goalLinePrefab;

    [Header("Combos")]
    [SerializeField] private RectTransform combosContent;
    [SerializeField] private GameObject comboLinePrefab;

    [Header("Final score + bar (optional)")]
    [SerializeField] private TMP_Text scoreFinalInt;
    [SerializeField] private Image progressFill;
    [SerializeField] private RectTransform tickBronze, tickSilver, tickGold;
    [SerializeField] private int bronze = 600, silver = 1200, gold = 3000;

    [Header("Sequence")]
    [SerializeField] private float lineDelay = 0.25f;
    [SerializeField] private float failDelaySec = 1f;

    [Header("Events")]
    public UnityEvent OnSequenceFailed;
    public UnityEvent OnVictory;

    // Gris neutre pour l’échec
    private static readonly Color GrayText = new Color(0.6f, 0.6f, 0.6f, 1f);

    private void Awake()
    {
        if (autoBindLevelManager && levelManagerRef == null)
            levelManagerRef = FindFirstObjectByType<LevelManager>();

        if (levelManagerRef != null)
            levelManagerRef.OnEndComputed.AddListener(HandleEndComputed);
    }

    private void OnDestroy()
    {
        if (levelManagerRef != null)
            levelManagerRef.OnEndComputed.RemoveListener(HandleEndComputed);
    }

    private void HandleEndComputed(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        ShowSequenced(stats, levelData, mainObj);
    }

    public void Show(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        StopAllCoroutines();
        StartCoroutine(RevealRoutine(stats, levelData, mainObj));
    }

    public void ShowSequenced(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        StopAllCoroutines();
        StartCoroutine(RevealRoutine(stats, levelData, mainObj));
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (endLevelOverlay) endLevelOverlay.SetActive(false);
    }

    private IEnumerator RevealRoutine(EndLevelStats stats, LevelData levelData, MainObjectiveResult mainObj)
    {
        if (endLevelOverlay) endLevelOverlay.SetActive(true);

        if (goalsContent) ClearChildren(goalsContent);
        if (combosContent) ClearChildren(combosContent);

        // Ligne 1 : billes collectées
        if (collectedInt)
        {
            collectedInt.text = stats.BallsCollected.ToString();
            collectedInt.gameObject.SetActive(true);
        }
        yield return new WaitForSeconds(lineDelay);

        // Ligne 2 : billes perdues
        if (lostInt)
        {
            lostInt.text = stats.BallsLost.ToString();
            lostInt.gameObject.SetActive(true);
        }
        yield return new WaitForSeconds(lineDelay);

        // Ligne 3 : score brut
        if (scoreInt)
        {
            scoreInt.text = stats.RawScore.ToString("N0");
            scoreInt.gameObject.SetActive(true);
        }
        yield return new WaitForSeconds(lineDelay);

        // Ligne 4 : objectif principal
        AddMainObjectiveLine(mainObj);
        yield return new WaitForSeconds(lineDelay);

        // Si l'objectif n'est pas atteint : on stoppe ici
        if (!mainObj.Achieved)
        {
            yield return new WaitForSeconds(failDelaySec);
            Debug.Log("FAILED");
            OnSequenceFailed?.Invoke();
            yield break;
        }

        // À partir d’ici : VICTOIRE

        // Ticks de score (bronze / silver / gold)
        if (levelData != null && levelData.ScoreGoals != null && levelData.ScoreGoals.Length > 0)
        {
            foreach (var g in levelData.ScoreGoals)
            {
                if (g.Type == "Bronze") PlaceTick(tickBronze, g.Points);
                else if (g.Type == "Silver") PlaceTick(tickSilver, g.Points);
                else if (g.Type == "Gold") PlaceTick(tickGold, g.Points);
            }
        }

        // Score final
        if (scoreFinalInt)
        {
            scoreFinalInt.text = stats.FinalScore.ToString("N0");
            scoreFinalInt.gameObject.SetActive(true);
        }
        yield return new WaitForSeconds(lineDelay);

        // Barre de progression finale
        if (progressFill && gold > 0)
        {
            float v = Mathf.Clamp01((float)stats.FinalScore / gold);
            progressFill.fillAmount = v;
            progressFill.gameObject.SetActive(true);
        }

        // Ici : la séquence de victoire est terminée -> on notifie
        OnVictory?.Invoke();
    }

    private void AddMainObjectiveLine(MainObjectiveResult mainObj)
    {
        if (!goalsContent || !goalLinePrefab) return;

        var go = Instantiate(goalLinePrefab, goalsContent);
        TMP_Text txt = null;
        TMP_Text val = null;

        var txtT = go.transform.Find("Goal_Txt");
        var valT = go.transform.Find("Goal_Int");
        if (txtT) txt = txtT.GetComponent<TMP_Text>();
        if (valT) val = valT.GetComponent<TMP_Text>();
        if (txt == null || val == null)
        {
            var tmps = go.GetComponentsInChildren<TMP_Text>(true);
            if (tmps.Length >= 2)
            {
                txt = tmps[0];
                val = tmps[tmps.Length - 1];
            }
        }

        if (txt) txt.text = mainObj.Text;

        if (val)
        {
            if (mainObj.BonusApplied > 0)
                val.text = $"{mainObj.Collected}/{mainObj.Required}  (+{mainObj.BonusApplied})";
            else
                val.text = $"{mainObj.Collected}/{mainObj.Required}";
        }

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

    public void HideStatsPanel()
    {
        if (panelContainer != null)
            panelContainer.gameObject.SetActive(false);
    }

}



[System.Serializable]
public struct MainObjectiveResult
{
    public string Text;
    public int ThresholdPct;
    public int Required;
    public int Collected;
    public bool Achieved;
    public int BonusApplied;
}
