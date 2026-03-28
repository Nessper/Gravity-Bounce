using System.Collections;
using UnityEngine;
using TMPro;

public class FinalPanelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject levelScorePanel;
    [SerializeField] private GameObject campaignScorePanel;

    [Header("Money")]
    [SerializeField] private GameObject moneyPanel;
    [SerializeField] private AnimatedIntText moneyAnimated;

    [Header("Money Tick SFX")]
    [SerializeField] private SfxId moneyTickSfx = SfxId.ShopBuy;
    [SerializeField] private float minMoneyTickSfxInterval = 0.03f;

    [Header("Money Reward Toast")]
    [SerializeField] private CanvasGroup moneyRewardCG;
    [SerializeField] private TMP_Text moneyRewardLabelText;
    [SerializeField] private TMP_Text moneyRewardValueText;
    [SerializeField] private float moneyRewardHoldSec = 0.7f;
    [SerializeField] private float moneyRewardFadeDuration = 0.15f;

    [Header("Animated values")]
    [SerializeField] private AnimatedIntText levelScoreAnimated;
    [SerializeField] private AnimatedIntText campaignScoreAnimated;

    [Header("Stamp roots")]
    [SerializeField] private GameObject stampRoot;
    [SerializeField] private GameObject stampVictory;
    [SerializeField] private GameObject stampDefeat;
    [SerializeField] private GameObject stampGameOver;

    [Header("Medal")]
    [SerializeField] private CanvasGroup medalBronze;
    [SerializeField] private CanvasGroup medalSilver;
    [SerializeField] private CanvasGroup medalGold;

    [Header("Money Update FX")]
    [SerializeField] private Color moneyGainUpdateColor = new Color32(0x59, 0xFF, 0x73, 0xFF);
    [SerializeField] private float moneyPulseScale = 1.10f;
    [SerializeField] private float moneyPulseDuration = 0.22f;

    [Header("Pop animation")]
    [SerializeField] private float popDurationSec = 0.16f;
    [SerializeField] private float popScaleMult = 1.10f;

    public enum FinalEndType
    {
        Victory,
        Defeat,
        GameOver
    }

    private TMP_Text moneyValueText;
    private Color moneyBaseColor = Color.white;
    private Coroutine moneyFxRoutine;

    private float lastMoneyTickSfxTime;
    private int lastMoneyAnimatedValue;

    private void Awake()
    {
        ResolveMoneyText();
        HideAllMedals();
    }

    private void OnEnable()
    {
        if (moneyAnimated != null)
            moneyAnimated.OnValueStep += HandleMoneyValueStep;
    }

    private void OnDisable()
    {
        if (moneyAnimated != null)
            moneyAnimated.OnValueStep -= HandleMoneyValueStep;
    }

    private void ResolveMoneyText()
    {
        if (moneyAnimated == null)
            return;

        moneyValueText = moneyAnimated.GetComponent<TMP_Text>();

        if (moneyValueText == null)
            moneyValueText = moneyAnimated.GetComponentInChildren<TMP_Text>();

        if (moneyValueText != null)
            moneyBaseColor = moneyValueText.color;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetAll()
    {
        if (levelScorePanel != null)
            levelScorePanel.SetActive(false);

        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(false);

        if (moneyPanel != null)
            moneyPanel.SetActive(false);

        ResetMoneyRewardToastInstant();

        HideAllStamps();
        HideAllMedals();

        if (moneyFxRoutine != null)
        {
            StopCoroutine(moneyFxRoutine);
            moneyFxRoutine = null;
        }

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

        lastMoneyTickSfxTime = 0f;
        lastMoneyAnimatedValue = 0;

        if (moneyValueText != null)
        {
            moneyValueText.color = moneyBaseColor;
            moneyValueText.transform.localScale = Vector3.one;
        }
    }

    // =========================================================
    // STAMP
    // =========================================================

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

    // =========================================================
    // SCORE PANELS
    // =========================================================

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

    // =========================================================
    // MONEY
    // =========================================================

    public void ShowMoneyPanelInstant(int value)
    {
        if (moneyPanel != null)
            moneyPanel.SetActive(true);

        if (moneyAnimated == null)
        {
            Debug.LogError("[FinalPanelUI] ShowMoneyPanelInstant: moneyAnimated manquant.");
            return;
        }

        int clampedValue = Mathf.Max(0, value);
        moneyAnimated.SetInstant(clampedValue);
        lastMoneyAnimatedValue = clampedValue;
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

        if (moneyFxRoutine != null)
        {
            StopCoroutine(moneyFxRoutine);
            moneyFxRoutine = null;
        }

        int clampedFrom = Mathf.Max(0, from);
        int clampedTo = Mathf.Max(0, to);

        moneyAnimated.SetInstant(clampedFrom);
        lastMoneyAnimatedValue = clampedFrom;

        moneyAnimated.AnimateTo(clampedTo);

        if (clampedTo > clampedFrom && moneyValueText != null)
            moneyFxRoutine = StartCoroutine(CoMoneyPulseFx());

        while (moneyAnimated.IsAnimating)
            yield return null;

        if (moneyFxRoutine != null)
        {
            yield return moneyFxRoutine;
            moneyFxRoutine = null;
        }
    }

    private void HandleMoneyValueStep(int value)
    {
        if (value <= lastMoneyAnimatedValue)
        {
            lastMoneyAnimatedValue = value;
            return;
        }

        lastMoneyAnimatedValue = value;

        float now = Time.unscaledTime;
        if (now - lastMoneyTickSfxTime < minMoneyTickSfxInterval)
            return;

        lastMoneyTickSfxTime = now;
        BootRoot.Audio?.PlayUi(moneyTickSfx);
    }

    public IEnumerator ShowMoneyRewardToast(string label, int amount)
    {
        if (moneyRewardCG == null)
        {
            Debug.LogWarning("[FinalPanelUI] ShowMoneyRewardToast: moneyRewardCG manquant.");
            yield break;
        }

        if (moneyRewardLabelText != null)
            moneyRewardLabelText.text = label;

        if (moneyRewardValueText != null)
            moneyRewardValueText.text = (amount >= 0 ? "+" : "") + amount.ToString();

        Transform t = moneyRewardCG.transform;

        moneyRewardCG.alpha = 0f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        t.localScale = Vector3.one;

        float fadeDur = Mathf.Max(0.01f, moneyRewardFadeDuration);
        float timer = 0f;

        while (timer < fadeDur)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / fadeDur);
            moneyRewardCG.alpha = k;
            yield return null;
        }

        moneyRewardCG.alpha = 1f;

        yield return StartCoroutine(PopRoutine(t));

        if (moneyRewardHoldSec > 0f)
            yield return new WaitForSecondsRealtime(moneyRewardHoldSec);

        timer = 0f;
        while (timer < fadeDur)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / fadeDur);
            moneyRewardCG.alpha = 1f - k;
            yield return null;
        }

        moneyRewardCG.alpha = 0f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        t.localScale = Vector3.one;
    }

    private void ResetMoneyRewardToastInstant()
    {
        if (moneyRewardCG == null)
            return;

        moneyRewardCG.alpha = 0f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        moneyRewardCG.transform.localScale = Vector3.one;
    }

    private IEnumerator CoMoneyPulseFx()
    {
        if (moneyValueText == null)
            yield break;

        Transform t = moneyValueText.transform;

        Vector3 baseScale = Vector3.one;
        Vector3 peakScale = baseScale * Mathf.Max(1f, moneyPulseScale);

        float duration = Mathf.Max(0.01f, moneyPulseDuration);
        float half = duration * 0.5f;

        moneyValueText.color = moneyGainUpdateColor;

        float timer = 0f;

        while (timer < half)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / half);
            t.localScale = Vector3.Lerp(baseScale, peakScale, k);
            yield return null;
        }

        t.localScale = peakScale;

        timer = 0f;
        while (timer < half)
        {
            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / half);
            t.localScale = Vector3.Lerp(peakScale, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
        moneyValueText.color = moneyBaseColor;
    }

    // =========================================================
    // MEDAL
    // =========================================================

    public void SetMedalInstant(EndMedal medal)
    {
        HideAllMedals();

        CanvasGroup target = GetMedalCanvasGroup(medal);
        if (target == null)
            return;

        SetAlpha(target, 1f);
    }

    public IEnumerator ShowMedal(EndMedal medal)
    {
        SetMedalInstant(medal);

        CanvasGroup target = GetMedalCanvasGroup(medal);
        if (target == null)
            yield break;

        yield return StartCoroutine(PopRoutine(target.transform));
    }

    private CanvasGroup GetMedalCanvasGroup(EndMedal medal)
    {
        if (medal == EndMedal.Bronze)
            return medalBronze;

        if (medal == EndMedal.Silver)
            return medalSilver;

        if (medal == EndMedal.Gold)
            return medalGold;

        return null;
    }

    // =========================================================
    // INTERNALS
    // =========================================================

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
        SetAlpha(medalBronze, 0f);
        SetAlpha(medalSilver, 0f);
        SetAlpha(medalGold, 0f);
    }

    private void SetAlpha(CanvasGroup cg, float alpha)
    {
        if (cg == null)
            return;

        cg.alpha = alpha;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private IEnumerator PopRoutine(Transform t)
    {
        if (t == null)
            yield break;

        Vector3 baseScale = Vector3.one;
        t.localScale = baseScale;

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