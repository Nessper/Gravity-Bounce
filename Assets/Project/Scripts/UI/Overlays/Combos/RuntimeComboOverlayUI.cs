using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RuntimeComboOverlayUI : MonoBehaviour
{
    [Header("Chain Lines")]
    [SerializeField] private RuntimeComboChainLineUI whiteChainLine;
    [SerializeField] private RuntimeComboChainLineUI blueChainLine;
    [SerializeField] private RuntimeComboChainLineUI redChainLine;

    [Header("Timing")]
    [SerializeField] private CanvasGroup timingGroup;
    [SerializeField] private TMP_Text timingLabel;

    private Coroutine timingRoutine;

    public void HideAll()
    {
        whiteChainLine?.Hide();
        blueChainLine?.Hide();
        redChainLine?.Hide();
        HideTiming();
    }

    public void SetChain(
    string comboId,
    string displayName,
    Color uiColor,
    int currentBalls,
    int stepBalls,
    int awardedLevel)
    {
        RuntimeComboChainLineUI line = GetChainLine(comboId);

        if (line == null)
            return;

        line.SetValue(
            displayName,
            uiColor,
            currentBalls,
            stepBalls,
            awardedLevel
        );
    }


    public void PulseChainReset()
    {
        // Placeholder volontairement simple.
        // On ajoutera shake / flash / anim propre apres validation visuelle.
    }

    public void PulseTimingStart()
    {
        // Placeholder.
    }

   

    private RuntimeComboChainLineUI GetChainLine(string comboId)
    {
        if (comboId == ComboIds.WhiteChain)
            return whiteChainLine;

        if (comboId == ComboIds.BlueChain)
            return blueChainLine;

        if (comboId == ComboIds.RedChain)
            return redChainLine;

        return null;
    }

    public void SetTiming(
    string comboId,
    string displayName,
    Color uiColor,
    float remainingSec,
    float durationSec)
    {
        StopTimingRoutine();

        float safeDuration = Mathf.Max(0.01f, durationSec);
        float ratio = Mathf.Clamp01(remainingSec / safeDuration);

        if (timingGroup != null)
        {
            timingGroup.alpha = ratio;
            timingGroup.interactable = false;
            timingGroup.blocksRaycasts = false;
        }

        if (timingLabel != null)
        {
            timingLabel.color = uiColor;
            timingLabel.text =
                displayName +
                "\n" +
                remainingSec.ToString("0.0") + "s";
        }
    }

    public void HideTiming()
    {
        StopTimingRoutine();

        if (timingGroup != null)
        {
            timingGroup.alpha = 0f;
            timingGroup.interactable = false;
            timingGroup.blocksRaycasts = false;
        }
    }


    public void PulseTimingSuccess()
    {
        StopTimingRoutine();

        if (timingGroup == null)
            return;

        timingRoutine = StartCoroutine(TimingSuccessRoutine());
    }

    public void PulseTimingExpired()
    {
        HideTiming();
    }

    public RectTransform TimingSourceRoot
    {
        get
        {
            if (timingGroup == null)
                return null;

            return timingGroup.transform as RectTransform;
        }
    }

    private IEnumerator TimingSuccessRoutine()
    {
        timingGroup.alpha = 1f;

        yield return new WaitForSeconds(0.08f);

        float from = timingGroup.alpha;
        float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            timingGroup.alpha = Mathf.Lerp(from, 0f, t);
            yield return null;
        }

        timingGroup.alpha = 0f;
        timingRoutine = null;
    }

    private void StopTimingRoutine()
    {
        if (timingRoutine == null)
            return;

        StopCoroutine(timingRoutine);
        timingRoutine = null;
    }
}