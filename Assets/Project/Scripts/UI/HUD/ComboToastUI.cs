using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ComboToastUI : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private ComboEngine comboEngine;
    [SerializeField] private ComboStyleProvider styleProvider;

    [Header("Containers (VerticalLayoutGroup uniquement)")]
    [SerializeField] private RectTransform leftContainer;
    [SerializeField] private RectTransform rightContainer;

    [Header("Prefab")]
    [SerializeField] private RectTransform toastPanelPrefab;

    [Header("Affichage")]
    [SerializeField] private int maxPerSide = 6;     // 600px de haut / 100px le prefab = 6
    [SerializeField] private float toastHeight = 100f;
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private float lifetime = 1.5f;

    private readonly Queue<RectTransform> leftActive = new Queue<RectTransform>(8);
    private readonly Queue<RectTransform> rightActive = new Queue<RectTransform>(8);

    private bool subscribed;

    private void Awake()
    {
        if (!comboEngine) comboEngine = Object.FindFirstObjectByType<ComboEngine>();
        if (!styleProvider) styleProvider = Object.FindFirstObjectByType<ComboStyleProvider>();
    }

    private void OnEnable()
    {
        if (comboEngine != null && !subscribed)
        {
            comboEngine.OnComboBatchIdsTriggered += ShowBatchIds;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (comboEngine != null && subscribed)
        {
            comboEngine.OnComboBatchIdsTriggered -= ShowBatchIds;
            subscribed = false;
        }
    }

    private void OnDestroy()
    {
        if (comboEngine != null && subscribed)
        {
            comboEngine.OnComboBatchIdsTriggered -= ShowBatchIds;
            subscribed = false;
        }
    }

    private void ShowBatchIds((string id, int points)[] items, string binSource)
    {
        if (items == null || items.Length == 0 || !toastPanelPrefab || !styleProvider) return;

        string side = string.IsNullOrEmpty(binSource) ? "left" : binSource.Trim().ToLowerInvariant();
        bool isRight = side.StartsWith("r");

        RectTransform container = isRight ? rightContainer : leftContainer;
        Queue<RectTransform> queue = isRight ? rightActive : leftActive;
        if (!container) return;

        for (int i = 0; i < items.Length; i++)
        {
            SpawnSingleToast(items[i].id, items[i].points, container, queue);
        }
    }

    private void SpawnSingleToast(string id, int points, RectTransform container, Queue<RectTransform> queue)
    {
        CleanQueue(queue);

        while (queue.Count >= maxPerSide)
        {
            var old = queue.Dequeue();
            if (old) Destroy(old.gameObject);
        }

        RectTransform panel = Instantiate(toastPanelPrefab, container);
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();

        // Garantit une hauteur fixe lisible pour le VerticalLayoutGroup
        var le = panel.GetComponent<LayoutElement>();
        if (!le) le = panel.gameObject.AddComponent<LayoutElement>();
        le.minHeight = toastHeight;
        le.preferredHeight = toastHeight;
        le.flexibleHeight = 0f;

        TMP_Text tmp = panel.GetComponentInChildren<TMP_Text>(true);
        if (tmp)
        {
            var style = styleProvider.Build(id, points);

            tmp.richText = false;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;

            // Permet le retour à la ligne proprement
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate; // ou .Ellipsis si tu veux les "…"

            tmp.color = style.color;
            tmp.text = style.line;
            tmp.alpha = 1f;
        }


        StartCoroutine(CoLifetime(panel, queue));

        queue.Enqueue(panel);
    }

    private IEnumerator CoLifetime(RectTransform panel, Queue<RectTransform> queue)
    {
        yield return new WaitForSeconds(lifetime);

        // Retire le panel de la file s'il est en tête
        if (queue != null)
        {
            while (queue.Count > 0 && queue.Peek() == null) queue.Dequeue();
            if (queue.Count > 0 && queue.Peek() == panel) queue.Dequeue();
        }

        if (panel) Destroy(panel.gameObject);
    }

    private static void CleanQueue(Queue<RectTransform> q)
    {
        if (q == null || q.Count == 0) return;
        int n = q.Count;
        for (int i = 0; i < n; i++)
        {
            var rt = q.Dequeue();
            if (rt != null) q.Enqueue(rt);
        }
    }
}
