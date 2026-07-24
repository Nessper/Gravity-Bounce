using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay final apres la Results Ceremony.
///
/// Objectif :
/// - reprendre le rythme / les FX du vieux FinalPanelUI
/// - garder le dialogue + hold-to-skip
/// - garder les boutons Menu / Retry / Next
///
/// IMPORTANT :
/// - ce script ne commit rien
/// - MainUIController doit deja avoir affiche le root global
/// </summary>
public class EndResultOverlayController : MonoBehaviour
{
    public enum EndResultType
    {
        Victory,
        Defeat,
        GameOver
    }

    [Serializable]
    public struct MoneyRewardLineData
    {
        public string Label;
        public int Amount;
    }

    [Header("Main UI")]
    [SerializeField] private MainUIController mainUIController;

    [Header("Header")]
    [SerializeField] private TMP_Text levelNameText;

    [Header("Panels")]
    [SerializeField] private GameObject levelScorePanel;
    [SerializeField] private GameObject campaignScorePanel;
    [SerializeField] private GameObject moneyPanel;

    [Header("Animated Values")]
    [SerializeField] private AnimatedIntText levelScoreAnimated;
    [SerializeField] private AnimatedIntText campaignScoreAnimated;
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

    [Header("Stamps")]
    [SerializeField] private CanvasGroup stampRoot;
    [SerializeField] private CanvasGroup stampVictory;
    [SerializeField] private CanvasGroup stampDefeat;
    [SerializeField] private CanvasGroup stampGameOver;

    [Header("Medals")]
    [SerializeField] private CanvasGroup medalBronze;
    [SerializeField] private CanvasGroup medalSilver;
    [SerializeField] private CanvasGroup medalGold;

    [Header("Buttons")]
    [SerializeField] private CanvasGroup bottomPanelGroup;
    [SerializeField] private Button buttonMenu;
    [SerializeField] private Button buttonRetry;
    [SerializeField] private Button buttonNext;

    [Header("Money Update FX")]
    [SerializeField] private Color moneyGainUpdateColor = new Color32(0x59, 0xFF, 0x73, 0xFF);
    [SerializeField] private float moneyPulseScale = 1.10f;
    [SerializeField] private float moneyPulseDuration = 0.22f;

    [Header("Pop Animation")]
    [SerializeField] private float popDurationSec = 0.16f;
    [SerializeField] private float popScaleMult = 1.10f;

    [Header("Reveal Timing")]
    [SerializeField] private float revealStepDelay = 0.25f;

    private TMP_Text moneyValueText;
    private Color moneyBaseColor = Color.white;

    private Coroutine playRoutine;
    private Coroutine moneyFxRoutine;

    private float lastMoneyTickSfxTime;
    private int lastMoneyAnimatedValue;

    private Vector3 moneyRewardToastBaseScale = Vector3.one;
    private Vector3 stampVictoryBaseScale = Vector3.one;
    private Vector3 stampDefeatBaseScale = Vector3.one;
    private Vector3 stampGameOverBaseScale = Vector3.one;
    private Vector3 medalBronzeBaseScale = Vector3.one;
    private Vector3 medalSilverBaseScale = Vector3.one;
    private Vector3 medalGoldBaseScale = Vector3.one;

    private bool skipRequested;

    private Action onMenu;
    private Action onRetry;
    private Action onNext;

    private void Awake()
    {
        ResolveMoneyText();
        CacheBaseScales();
        ResetAll();
        BindButtons();
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

        mainUIController?.HideHoldToSkip(this);
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void RequestSkipReveal()
    {
        skipRequested = true;

        mainUIController?.StopAndHideDialog();

        mainUIController?.HideHoldToSkip(this);
    }

    /// <summary>
    /// Nettoie l'ancien contenu avant que le conteneur global commence son fondu.
    /// Les animations de reveal restent lancees uniquement par Play().
    /// </summary>
    public void PrepareForReveal()
    {
        StopPlayRoutine();
        ResetAll();
    }

    public void Play(
        string levelName,
        EndResultType resultType,
        int levelScore,
        int campaignScoreBefore,
        int campaignScoreAfter,
        EndMedal medal,
        bool showMoney,
        int moneyBefore,
        int moneyAfter,
        List<MoneyRewardLineData> moneyRewardLines,
        string dialogSequenceId,
        bool showMenu,
        bool showRetry,
        bool showNext,
        Action onMenuClicked,
        Action onRetryClicked,
        Action onNextClicked)
    {
        StopPlayRoutine();

        onMenu = onMenuClicked;
        onRetry = onRetryClicked;
        onNext = onNextClicked;

        ResetAll();

        playRoutine = StartCoroutine(
            PlayRoutine(
                levelName,
                resultType,
                levelScore,
                campaignScoreBefore,
                campaignScoreAfter,
                medal,
                showMoney,
                moneyBefore,
                moneyAfter,
                moneyRewardLines,
                dialogSequenceId,
                showMenu,
                showRetry,
                showNext
            )
        );
    }

    public void ShowInstant(
        string levelName,
        EndResultType resultType,
        int levelScore,
        int campaignScore,
        EndMedal medal,
        bool showMoney,
        int moneyCount,
        List<MoneyRewardLineData> moneyRewardLines,
        bool showMenu,
        bool showRetry,
        bool showNext,
        Action onMenuClicked,
        Action onRetryClicked,
        Action onNextClicked)
    {
        StopPlayRoutine();

        onMenu = onMenuClicked;
        onRetry = onRetryClicked;
        onNext = onNextClicked;

        ResetAll();

        if (levelNameText != null)
            levelNameText.text = levelName ?? string.Empty;

        SetStampInstant(resultType);
        ShowLevelScorePanel(levelScore);
        ShowCampaignScorePanelInstant(campaignScore);
        SetMedalInstant(medal);

        if (showMoney)
            ShowMoneyPanelInstant(moneyCount);

        ApplyButtons(showMenu, showRetry, showNext);
    }

    private IEnumerator PlayRoutine(
        string levelName,
        EndResultType resultType,
        int levelScore,
        int campaignScoreBefore,
        int campaignScoreAfter,
        EndMedal medal,
        bool showMoney,
        int moneyBefore,
        int moneyAfter,
        List<MoneyRewardLineData> moneyRewardLines,
        string dialogSequenceId,
        bool showMenu,
        bool showRetry,
        bool showNext)
    {
        skipRequested = false;

        if (levelNameText != null)
            levelNameText.text = levelName ?? string.Empty;

        mainUIController?.ShowHoldToSkip(this, RequestSkipReveal);

        yield return StartCoroutine(PlayDialogByIdSkippable(dialogSequenceId));
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        yield return StartCoroutine(StepDelaySkippable());
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        yield return StartCoroutine(ShowStamp(resultType));
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        yield return StartCoroutine(StepDelaySkippable());
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        ShowLevelScorePanel(levelScore);

        yield return StartCoroutine(StepDelaySkippable());
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        ShowCampaignScorePanelInstant(campaignScoreBefore);

        if (campaignScoreAfter != campaignScoreBefore)
        {
            yield return StartCoroutine(AnimateCampaignScore(campaignScoreBefore, campaignScoreAfter));
            if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
                yield break;
        }

        yield return StartCoroutine(StepDelaySkippable());
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        yield return StartCoroutine(ShowMedal(medal));
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        if (showMoney)
        {
            yield return StartCoroutine(StepDelaySkippable());
            if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
                yield break;

            ShowMoneyPanelInstant(moneyBefore);

            int currentMoney = moneyBefore;

            if (moneyRewardLines != null && moneyRewardLines.Count > 0)
            {
                for (int i = 0; i < moneyRewardLines.Count; i++)
                {
                    MoneyRewardLineData line = moneyRewardLines[i];
                    if (line.Amount <= 0)
                        continue;

                    yield return StartCoroutine(StepDelaySkippable());
                    if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
                        yield break;

                    yield return StartCoroutine(ShowMoneyRewardToast(line.Label, line.Amount));
                    if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
                        yield break;

                    int nextMoney = currentMoney + line.Amount;

                    yield return StartCoroutine(AnimateMoney(currentMoney, nextMoney));
                    if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
                        yield break;

                    currentMoney = nextMoney;
                }
            }
        }

        yield return StartCoroutine(StepDelaySkippable());
        if (HandleSkip(levelName, resultType, levelScore, campaignScoreAfter, medal, showMoney, moneyAfter, moneyRewardLines, showMenu, showRetry, showNext))
            yield break;

        ApplyButtons(showMenu, showRetry, showNext);
        EndRoutine();
    }

    private bool HandleSkip(
        string levelName,
        EndResultType resultType,
        int levelScore,
        int campaignScore,
        EndMedal medal,
        bool showMoney,
        int moneyCount,
        List<MoneyRewardLineData> moneyRewardLines,
        bool showMenu,
        bool showRetry,
        bool showNext)
    {
        if (!skipRequested)
            return false;

        ShowInstant(
            levelName,
            resultType,
            levelScore,
            campaignScore,
            medal,
            showMoney,
            moneyCount,
            moneyRewardLines,
            showMenu,
            showRetry,
            showNext,
            onMenu,
            onRetry,
            onNext
        );

        EndRoutine();
        return true;
    }

    private void EndRoutine()
    {
        mainUIController?.HideHoldToSkip(this); ;

        skipRequested = false;
        playRoutine = null;
    }

    private IEnumerator StepDelaySkippable()
    {
        if (revealStepDelay <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < revealStepDelay)
        {
            if (skipRequested)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
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

    private void CacheBaseScales()
    {
        if (moneyRewardCG != null)
            moneyRewardToastBaseScale = moneyRewardCG.transform.localScale;

        if (stampVictory != null)
            stampVictoryBaseScale = stampVictory.transform.localScale;

        if (stampDefeat != null)
            stampDefeatBaseScale = stampDefeat.transform.localScale;

        if (stampGameOver != null)
            stampGameOverBaseScale = stampGameOver.transform.localScale;

        if (medalBronze != null)
            medalBronzeBaseScale = medalBronze.transform.localScale;

        if (medalSilver != null)
            medalSilverBaseScale = medalSilver.transform.localScale;

        if (medalGold != null)
            medalGoldBaseScale = medalGold.transform.localScale;
    }

    public void ResetAll()
    {
        if (levelScorePanel != null)
            levelScorePanel.SetActive(false);

        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(false);

        if (moneyPanel != null)
            moneyPanel.SetActive(false);

        if (bottomPanelGroup != null)
        {
            bottomPanelGroup.alpha = 0f;
            bottomPanelGroup.interactable = false;
            bottomPanelGroup.blocksRaycasts = false;
        }

        ResetMoneyRewardToastInstant();
        HideAllStamps();
        HideAllMedals();

        if (moneyFxRoutine != null)
        {
            StopCoroutine(moneyFxRoutine);
            moneyFxRoutine = null;
        }

        if (levelScoreAnimated != null)
            levelScoreAnimated.SetInstant(0);

        if (campaignScoreAnimated != null)
            campaignScoreAnimated.SetInstant(0);

        if (moneyAnimated != null)
            moneyAnimated.SetInstant(0);

        lastMoneyTickSfxTime = 0f;
        lastMoneyAnimatedValue = 0;

        if (moneyValueText != null)
        {
            moneyValueText.color = moneyBaseColor;
            moneyValueText.transform.localScale = Vector3.one;
        }

        RestoreAllKnownScales();

        mainUIController?.StopAndHideDialog();

        mainUIController?.HideHoldToSkip(this);
    }

    public void SetStampInstant(EndResultType type)
    {
        SetAlpha(stampRoot, 1f);

        SetStampVisible(stampVictory, type == EndResultType.Victory);
        SetStampVisible(stampDefeat, type == EndResultType.Defeat);
        SetStampVisible(stampGameOver, type == EndResultType.GameOver);

        RestoreStampScale(type);
    }

    public IEnumerator ShowStamp(EndResultType type)
    {
        SetStampInstant(type);

        CanvasGroup target = GetStampTarget(type);
        if (target == null)
            yield break;

        yield return StartCoroutine(PopRoutine(target.transform, GetStampBaseScale(type)));
    }

    public void ShowLevelScorePanel(int levelScore)
    {
        if (levelScorePanel != null)
            levelScorePanel.SetActive(true);

        if (levelScoreAnimated == null)
            return;

        levelScoreAnimated.SetInstant(levelScore);
    }

    public void ShowCampaignScorePanelInstant(int value)
    {
        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(true);

        if (campaignScoreAnimated == null)
            return;

        campaignScoreAnimated.SetInstant(value);
    }

    public IEnumerator AnimateCampaignScore(int from, int to)
    {
        if (campaignScorePanel != null)
            campaignScorePanel.SetActive(true);

        if (campaignScoreAnimated == null)
            yield break;

        campaignScoreAnimated.SetInstant(from);
        campaignScoreAnimated.AnimateTo(to);

        while (campaignScoreAnimated.IsAnimating)
        {
            if (skipRequested)
                yield break;

            yield return null;
        }
    }

    public void ShowMoneyPanelInstant(int value)
    {
        if (moneyPanel != null)
            moneyPanel.SetActive(true);

        if (moneyAnimated == null)
            return;

        int clampedValue = Mathf.Max(0, value);
        moneyAnimated.SetInstant(clampedValue);
        lastMoneyAnimatedValue = clampedValue;
    }

    public IEnumerator AnimateMoney(int from, int to)
    {
        if (moneyPanel != null)
            moneyPanel.SetActive(true);

        if (moneyAnimated == null)
            yield break;

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
        {
            if (skipRequested)
                yield break;

            yield return null;
        }

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
            yield break;

        if (moneyRewardLabelText != null)
            moneyRewardLabelText.text = label;

        if (moneyRewardValueText != null)
            moneyRewardValueText.text = (amount >= 0 ? "+" : "") + amount.ToString();

        Transform t = moneyRewardCG.transform;

        moneyRewardCG.alpha = 0f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        t.localScale = moneyRewardToastBaseScale;

        float fadeDur = Mathf.Max(0.01f, moneyRewardFadeDuration);
        float timer = 0f;

        while (timer < fadeDur)
        {
            if (skipRequested)
                yield break;

            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / fadeDur);
            moneyRewardCG.alpha = k;
            yield return null;
        }

        moneyRewardCG.alpha = 1f;

        yield return StartCoroutine(PopRoutine(t, moneyRewardToastBaseScale));

        if (moneyRewardHoldSec > 0f)
        {
            float hold = 0f;
            while (hold < moneyRewardHoldSec)
            {
                if (skipRequested)
                    yield break;

                hold += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        timer = 0f;
        while (timer < fadeDur)
        {
            if (skipRequested)
                yield break;

            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / fadeDur);
            moneyRewardCG.alpha = 1f - k;
            yield return null;
        }

        moneyRewardCG.alpha = 0f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        t.localScale = moneyRewardToastBaseScale;
    }

    private void SetMoneyRewardToastInstant(string label, int amount)
    {
        if (moneyRewardCG == null)
            return;

        if (moneyRewardLabelText != null)
            moneyRewardLabelText.text = label;

        if (moneyRewardValueText != null)
            moneyRewardValueText.text = (amount >= 0 ? "+" : "") + amount.ToString();

        moneyRewardCG.alpha = 1f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        moneyRewardCG.transform.localScale = moneyRewardToastBaseScale;
    }

    private void ResetMoneyRewardToastInstant()
    {
        if (moneyRewardCG == null)
            return;

        moneyRewardCG.alpha = 0f;
        moneyRewardCG.interactable = false;
        moneyRewardCG.blocksRaycasts = false;
        moneyRewardCG.transform.localScale = moneyRewardToastBaseScale;
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
            if (skipRequested)
                yield break;

            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / half);
            t.localScale = Vector3.Lerp(baseScale, peakScale, k);
            yield return null;
        }

        t.localScale = peakScale;

        timer = 0f;
        while (timer < half)
        {
            if (skipRequested)
                yield break;

            timer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(timer / half);
            t.localScale = Vector3.Lerp(peakScale, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
        moneyValueText.color = moneyBaseColor;
    }

    public void SetMedalInstant(EndMedal medal)
    {
        HideAllMedals();

        CanvasGroup target = GetMedalCanvasGroup(medal);
        if (target == null)
            return;

        SetAlpha(target, 1f);
        RestoreMedalScale(medal);
    }

    public IEnumerator ShowMedal(EndMedal medal)
    {
        SetMedalInstant(medal);

        CanvasGroup target = GetMedalCanvasGroup(medal);
        if (target == null)
            yield break;

        yield return StartCoroutine(PopRoutine(target.transform, GetMedalBaseScale(medal)));
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

    private void HideAllStamps()
    {
        SetAlpha(stampRoot, 0f);

        SetStampVisible(stampVictory, false);
        SetStampVisible(stampDefeat, false);
        SetStampVisible(stampGameOver, false);

        RestoreStampScale(EndResultType.Victory);
        RestoreStampScale(EndResultType.Defeat);
        RestoreStampScale(EndResultType.GameOver);
    }

    private void SetStampVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null)
            return;

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private void HideAllMedals()
    {
        SetAlpha(medalBronze, 0f);
        SetAlpha(medalSilver, 0f);
        SetAlpha(medalGold, 0f);

        RestoreMedalScale(EndMedal.Bronze);
        RestoreMedalScale(EndMedal.Silver);
        RestoreMedalScale(EndMedal.Gold);
    }

    private void SetAlpha(CanvasGroup cg, float alpha)
    {
        if (cg == null)
            return;

        cg.alpha = alpha;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private CanvasGroup GetStampTarget(EndResultType type)
    {
        if (type == EndResultType.Victory)
            return stampVictory;

        if (type == EndResultType.Defeat)
            return stampDefeat;

        return stampGameOver;
    }

    private Vector3 GetStampBaseScale(EndResultType type)
    {
        if (type == EndResultType.Victory)
            return stampVictoryBaseScale;

        if (type == EndResultType.Defeat)
            return stampDefeatBaseScale;

        return stampGameOverBaseScale;
    }

    private void RestoreStampScale(EndResultType type)
    {
        CanvasGroup target = GetStampTarget(type);
        if (target == null)
            return;

        target.transform.localScale = GetStampBaseScale(type);
    }

    private Vector3 GetMedalBaseScale(EndMedal medal)
    {
        if (medal == EndMedal.Bronze)
            return medalBronzeBaseScale;

        if (medal == EndMedal.Silver)
            return medalSilverBaseScale;

        if (medal == EndMedal.Gold)
            return medalGoldBaseScale;

        return Vector3.one;
    }

    private void RestoreMedalScale(EndMedal medal)
    {
        CanvasGroup target = GetMedalCanvasGroup(medal);
        if (target == null)
            return;

        target.transform.localScale = GetMedalBaseScale(medal);
    }

    private void RestoreAllKnownScales()
    {
        if (moneyRewardCG != null)
            moneyRewardCG.transform.localScale = moneyRewardToastBaseScale;

        RestoreStampScale(EndResultType.Victory);
        RestoreStampScale(EndResultType.Defeat);
        RestoreStampScale(EndResultType.GameOver);

        RestoreMedalScale(EndMedal.Bronze);
        RestoreMedalScale(EndMedal.Silver);
        RestoreMedalScale(EndMedal.Gold);
    }

    private IEnumerator PopRoutine(Transform t, Vector3 baseScale)
    {
        if (t == null)
            yield break;

        t.localScale = baseScale;

        Vector3 targetScale = baseScale * popScaleMult;
        float half = Mathf.Max(0.001f, popDurationSec * 0.5f);

        float a = 0f;
        while (a < half)
        {
            if (skipRequested)
                yield break;

            a += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(a / half);
            t.localScale = Vector3.Lerp(baseScale, targetScale, k);
            yield return null;
        }

        a = 0f;
        while (a < half)
        {
            if (skipRequested)
                yield break;

            a += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(a / half);
            t.localScale = Vector3.Lerp(targetScale, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
    }

    private void ApplyButtons(bool showMenu, bool showRetry, bool showNext)
    {
        if (buttonMenu != null)
            buttonMenu.gameObject.SetActive(showMenu);

        if (buttonRetry != null)
            buttonRetry.gameObject.SetActive(showRetry);

        if (buttonNext != null)
            buttonNext.gameObject.SetActive(showNext);

        if (bottomPanelGroup != null)
        {
            bottomPanelGroup.alpha = 1f;
            bottomPanelGroup.interactable = true;
            bottomPanelGroup.blocksRaycasts = true;
        }
    }

    private void BindButtons()
    {
        if (buttonMenu != null)
            buttonMenu.onClick.AddListener(HandleMenuClicked);

        if (buttonRetry != null)
            buttonRetry.onClick.AddListener(HandleRetryClicked);

        if (buttonNext != null)
            buttonNext.onClick.AddListener(HandleNextClicked);
    }

    private void UnbindButtons()
    {
        if (buttonMenu != null)
            buttonMenu.onClick.RemoveListener(HandleMenuClicked);

        if (buttonRetry != null)
            buttonRetry.onClick.RemoveListener(HandleRetryClicked);

        if (buttonNext != null)
            buttonNext.onClick.RemoveListener(HandleNextClicked);
    }

    private void HandleMenuClicked()
    {
        onMenu?.Invoke();
    }

    private void HandleRetryClicked()
    {
        onRetry?.Invoke();
    }

    private void HandleNextClicked()
    {
        onNext?.Invoke();
    }

    private void StopPlayRoutine()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        mainUIController?.StopAndHideDialog();

        mainUIController?.HideHoldToSkip(this);
    }

    private IEnumerator PlayDialogByIdSkippable(string sequenceId)
    {
        if (mainUIController == null)
            yield break;

        if (string.IsNullOrWhiteSpace(sequenceId))
            yield break;

        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null)
        {
            Debug.LogError("[EndResultOverlayController] LocalizationManager.Instance est null.");
            yield break;
        }

        while (!loc.IsReady)
        {
            if (skipRequested)
                yield break;

            yield return null;
        }

        DialogSequence sequence = loc.GetSequenceById(sequenceId);
        if (sequence == null)
            yield break;

        DialogLine[] lines = loc.GetRandomVariantLines(sequence);
        if (lines == null || lines.Length == 0)
            yield break;

        bool done = false;

        mainUIController.PlayDialogSequence(
            lines,
            DialogSequenceRunner.PlaybackMode.Interactive,
            () => done = true
        );

        while (!done)
        {
            if (skipRequested)
            {
                mainUIController.StopAndHideDialog();
                yield break;
            }

            yield return null;
        }
    }
}
