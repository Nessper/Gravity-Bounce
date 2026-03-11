using System.Collections;
using UnityEngine;

public class FinalPanelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject levelScorePanel;
    [SerializeField] private GameObject campaignScorePanel;

    [Header("Money Panel")]
    [SerializeField] private GameObject moneyPanel;
    [SerializeField] private AnimatedIntText moneyAnimated;

    [Header("Animated values")]
    [SerializeField] private AnimatedIntText levelScoreAnimated;
    [SerializeField] private AnimatedIntText campaignScoreAnimated;

    [Header("Stamp roots")]
    [SerializeField] private GameObject stampRoot;
    [SerializeField] private GameObject stampVictory;
    [SerializeField] private GameObject stampDefeat;
    [SerializeField] private GameObject stampGameOver;

    [Header("Medal")]
    [SerializeField] private GameObject medalRoot;
    [SerializeField] private GameObject medalBronze;
    [SerializeField] private GameObject medalSilver;
    [SerializeField] private GameObject medalGold;

    [Header("Pop animation")]
    [SerializeField] private float popDurationSec = 0.16f;
    [SerializeField] private float popScaleMult = 1.10f;

    public enum FinalEndType
    {
        Victory,
        Defeat,
        GameOver
    }

    public void ResetAll()
    {
        if (levelScorePanel != null)
            levelScorePanel.SetActive(false);

        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(false);

        if (moneyPanel != null)
            moneyPanel.SetActive(false);

        HideAllStamps();
        HideAllMedals();

        if (levelScoreAnimated == null)
            Debug.LogError("[FinalPanelUI] levelScoreAnimated manquant.");
        else
            levelScoreAnimated.SetInstant(0);

        if (campaignScoreAnimated == null)
            Debug.LogError("[FinalPanelUI] campaignScoreAnimated manquant.");
        else
            campaignScoreAnimated.SetInstant(0);

        if (moneyAnimated == null)
            Debug.LogError("[FinalPanelUI] moneyAnimated manquant.");
        else
            moneyAnimated.SetInstant(0);
    }

    public IEnumerator ShowStamp(FinalEndType type)
    {
        HideAllStamps();

        if (stampRoot != null)
            stampRoot.SetActive(true);

        GameObject target = null;

        if (type == FinalEndType.Victory)
            target = stampVictory;
        else if (type == FinalEndType.Defeat)
            target = stampDefeat;
        else
            target = stampGameOver;

        if (target == null)
        {
            Debug.LogError("[FinalPanelUI] Stamp target manquant pour type=" + type);
            yield break;
        }

        target.SetActive(true);
        yield return StartCoroutine(PopRoutine(target.transform));
    }

    public void ShowLevelScorePanel(int levelScore)
    {
        if (levelScorePanel != null)
            levelScorePanel.SetActive(true);

        if (levelScoreAnimated == null)
        {
            Debug.LogError("[FinalPanelUI] ShowLevelScorePanel: levelScoreAnimated manquant.");
            return;
        }

        levelScoreAnimated.SetInstant(levelScore);
    }

    public void ShowCampaignScorePanelInstant(int value)
    {
        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(true);

        if (campaignScoreAnimated == null)
        {
            Debug.LogError("[FinalPanelUI] ShowCampaignScorePanelInstant: campaignScoreAnimated manquant.");
            return;
        }

        campaignScoreAnimated.SetInstant(value);
    }

    public IEnumerator AnimateCampaignScore(int from, int to)
    {
        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(true);

        if (campaignScoreAnimated == null)
        {
            Debug.LogError("[FinalPanelUI] AnimateCampaignScore: campaignScoreAnimated manquant.");
            yield break;
        }

        campaignScoreAnimated.SetInstant(from);
        campaignScoreAnimated.AnimateTo(to);

        while (campaignScoreAnimated.IsAnimating)
            yield return null;
    }

    // ============================================================
    // MONEY
    // ============================================================

    public void ShowMoneyPanelInstant(int value)
    {
        if (moneyPanel != null)
            moneyPanel.SetActive(true);

        if (moneyAnimated == null)
        {
            Debug.LogError("[FinalPanelUI] ShowMoneyPanelInstant: moneyAnimated manquant.");
            return;
        }

        moneyAnimated.SetInstant(Mathf.Max(0, value));
    }

    public IEnumerator AnimateMoney(int from, int to)
    {
        if (moneyPanel != null)
            moneyPanel.SetActive(true);

        if (moneyAnimated == null)
        {
            Debug.LogError("[FinalPanelUI] AnimateMoney: moneyAnimated manquant.");
            yield break;
        }

        moneyAnimated.SetInstant(Mathf.Max(0, from));
        moneyAnimated.AnimateTo(Mathf.Max(0, to));

        while (moneyAnimated.IsAnimating)
            yield return null;
    }

    // ============================================================
    // MEDAL
    // ============================================================

    public IEnumerator ShowMedal(EndMedal medal)
    {
        HideAllMedals();

        if (medal == EndMedal.None)
            yield break;

        if (medalRoot != null)
            medalRoot.SetActive(true);

        GameObject target = null;

        if (medal == EndMedal.Bronze)
            target = medalBronze;
        else if (medal == EndMedal.Silver)
            target = medalSilver;
        else if (medal == EndMedal.Gold)
            target = medalGold;

        if (target == null)
        {
            Debug.LogError("[FinalPanelUI] Medal target manquant pour medal=" + medal);
            yield break;
        }

        target.SetActive(true);
        yield return StartCoroutine(PopRoutine(target.transform));
    }

    private void HideAllStamps()
    {
        if (stampRoot != null)
            stampRoot.SetActive(false);

        if (stampVictory != null)
            stampVictory.SetActive(false);

        if (stampDefeat != null)
            stampDefeat.SetActive(false);

        if (stampGameOver != null)
            stampGameOver.SetActive(false);
    }

    private void HideAllMedals()
    {
        if (medalRoot != null)
            medalRoot.SetActive(false);

        if (medalBronze != null)
            medalBronze.SetActive(false);

        if (medalSilver != null)
            medalSilver.SetActive(false);

        if (medalGold != null)
            medalGold.SetActive(false);
    }

    private IEnumerator PopRoutine(Transform t)
    {
        if (t == null)
            yield break;

        Vector3 baseScale = t.localScale;
        Vector3 targetScale = baseScale * popScaleMult;

        float half = Mathf.Max(0.001f, popDurationSec * 0.5f);

        float a = 0f;
        while (a < half)
        {
            a += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(a / half);
            t.localScale = Vector3.Lerp(baseScale, targetScale, k);
            yield return null;
        }

        a = 0f;
        while (a < half)
        {
            a += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(a / half);
            t.localScale = Vector3.Lerp(targetScale, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
    }
}
