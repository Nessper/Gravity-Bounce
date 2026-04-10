using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatToastUI : MonoBehaviour
{
    private const string ModulesPackName = "modules";

    [Header("Container")]
    [SerializeField] private RectTransform container;

    [Header("Prefab")]
    [SerializeField] private RectTransform toastPanelPrefab;

    [Header("Display")]
    [SerializeField] private float toastHeight = 80f;
    [SerializeField] private float holdDuration = 2.2f;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Punch")]
    [SerializeField] private float punchScale = 1.08f;
    [SerializeField] private float punchDuration = 0.15f;

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float pulseAmount = 0.08f;

    [Header("Colors")]
    [SerializeField] private Color hullRepairColor = new Color(0.35f, 1f, 0.45f);
    [SerializeField] private Color maxHullColor = new Color(0.20f, 0.80f, 0.35f);

    [Header("Toast SFX")]
    [SerializeField] private SfxId moduleAddHullSfx = SfxId.ModuleAddHull;
    [SerializeField] private SfxId moduleAddMaxHullSfx = SfxId.ModuleAddMaxHull;
    [SerializeField] private float moduleSfxDelay = 0.10f;

    private struct PendingToast
    {
        public string title;
        public string value;
        public Color color;
        public SfxId sfxId;
    }

    private readonly Queue<PendingToast> pendingToasts = new Queue<PendingToast>(8);

    private bool isPlaying;
    private RectTransform currentPanel;

    public bool IsBusy => isPlaying;

    public IEnumerator WaitUntilIdle()
    {
        while (isPlaying)
            yield return null;
    }

    public void ShowHullRepair(ModuleDefinition mod, int actualAmount)
    {
        if (mod == null)
            return;

        if (actualAmount <= 0)
            return;

        string title = GetLocalizedModuleName(mod) + " T" + mod.tier;
        string value = "+" + actualAmount + " Hull";

        EnqueueToast(
            title: title,
            value: value,
            color: hullRepairColor,
            sfxId: moduleAddHullSfx
        );
    }

    public void ShowMaxHullGain(ModuleDefinition mod)
    {
        if (mod == null)
            return;

        int amount = Mathf.Max(0, mod.endLevelFullHullHullMaxAdd);
        if (amount <= 0)
            return;

        string title = GetLocalizedModuleName(mod) + " T" + mod.tier;
        string value = "+" + amount + " Max Hull";

        EnqueueToast(
            title: title,
            value: value,
            color: maxHullColor,
            sfxId: moduleAddMaxHullSfx
        );
    }

    private void EnqueueToast(string title, string value, Color color, SfxId sfxId)
    {
        pendingToasts.Enqueue(new PendingToast
        {
            title = title,
            value = value,
            color = color,
            sfxId = sfxId
        });

        if (!isPlaying)
            StartCoroutine(CoPlayQueue());
    }

    private IEnumerator CoPlayQueue()
    {
        isPlaying = true;

        while (pendingToasts.Count > 0)
        {
            PendingToast toast = pendingToasts.Dequeue();
            yield return StartCoroutine(CoShowSingleToast(toast));
        }

        isPlaying = false;
    }

    private IEnumerator CoShowSingleToast(PendingToast toast)
    {
        if (container == null || toastPanelPrefab == null)
            yield break;

        if (currentPanel != null)
            Destroy(currentPanel.gameObject);

        RectTransform panel = Instantiate(toastPanelPrefab, container);
        currentPanel = panel;

        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();

        LayoutElement le = panel.GetComponent<LayoutElement>();
        if (le == null)
            le = panel.gameObject.AddComponent<LayoutElement>();

        le.minHeight = toastHeight;
        le.preferredHeight = toastHeight;
        le.flexibleHeight = 0f;

        Transform titleTf = panel.transform.Find("TextToast_Txt");
        Transform valueTf = panel.transform.Find("StatsToast_Txt");

        if (titleTf == null || valueTf == null)
        {
            Destroy(panel.gameObject);
            currentPanel = null;
            yield break;
        }

        TMP_Text titleTxt = titleTf.GetComponent<TMP_Text>();
        TMP_Text valueTxt = valueTf.GetComponent<TMP_Text>();

        if (titleTxt == null || valueTxt == null)
        {
            Destroy(panel.gameObject);
            currentPanel = null;
            yield break;
        }

        bool hasTitle = !string.IsNullOrEmpty(toast.title);

        titleTxt.gameObject.SetActive(hasTitle);
        if (hasTitle)
            titleTxt.text = toast.title;

        valueTxt.text = toast.value;

        titleTxt.color = toast.color;
        valueTxt.color = toast.color;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = panel.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 1f;

        RectTransform rt = panel;
        rt.localScale = Vector3.one;

        yield return StartCoroutine(PlayPunch(rt));
        StartCoroutine(PlayToastSfxDelayed(toast.sfxId));

        float hold = Mathf.Max(0f, holdDuration);
        float elapsedHold = 0f;
        Vector3 baseScale = Vector3.one;

        while (elapsedHold < hold)
        {
            elapsedHold += Time.unscaledDeltaTime;

            float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float pulse = 1f + wave * pulseAmount;
            rt.localScale = baseScale * pulse;

            yield return null;
        }

        rt.localScale = baseScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cg.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        cg.alpha = 0f;

        if (panel != null)
            Destroy(panel.gameObject);

        if (currentPanel == panel)
            currentPanel = null;
    }

    private IEnumerator PlayPunch(RectTransform rt)
    {
        if (rt == null)
            yield break;

        float duration = Mathf.Max(0.01f, punchDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float punchT = Mathf.Sin(t * Mathf.PI);
            float scale = Mathf.Lerp(1f, punchScale, punchT);

            rt.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    private IEnumerator PlayToastSfxDelayed(SfxId sfxId)
    {
        if (moduleSfxDelay > 0f)
            yield return new WaitForSecondsRealtime(moduleSfxDelay);

        BootRoot.Audio?.PlayUi(sfxId);
    }

    private string GetLocalizedModuleName(ModuleDefinition mod)
    {
        if (mod == null)
            return "Unknown";

        if (string.IsNullOrWhiteSpace(mod.displayNameLocKey))
            return mod.id;

        if (LocalizationManager.Instance == null || !LocalizationManager.Instance.IsReady)
            return mod.displayNameLocKey;

        return LocalizationManager.Instance.GetTextOrKey(ModulesPackName, mod.displayNameLocKey);
    }
}