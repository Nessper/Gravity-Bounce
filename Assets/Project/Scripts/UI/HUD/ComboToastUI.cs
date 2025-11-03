using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ComboToastUI : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private ComboEngine comboEngine;
    [SerializeField] private ComboStyleProvider styleProvider;

    [Header("Containers (VerticalLayoutGroup SEUL)")]
    [SerializeField] private RectTransform leftContainer;
    [SerializeField] private RectTransform rightContainer;

    [Header("Prefab")]
   
    [SerializeField] private RectTransform toastPanelPrefab;

    [Header("Timing / Anim")]
    [SerializeField] private float lifetime = 1.5f;
    [SerializeField] private float fadeInLifetime = 0.25f;
    [SerializeField] private float fadeOutLifetime = 0.25f;
    [SerializeField] private float moveUpDistance = 30f;

    [Header("Affichage")]
    [SerializeField] private int maxPerSide = 2;   // nb de panneaux visibles par côté
    [SerializeField] private float fontSize = 24f; // taille des lignes
    [SerializeField] private float verticalPadding = 6f;  // marge ajoutée à la hauteur TMP

    private readonly Queue<RectTransform> leftActive = new Queue<RectTransform>(4);
    private readonly Queue<RectTransform> rightActive = new Queue<RectTransform>(4);

    // anti double-abonnement
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

    /// <summary>
    /// Reçoit un batch de combos et l’affiche dans un seul panneau multi-lignes
    /// du côté gauche/droite en fonction de binSource.
    /// </summary>
    private void ShowBatchIds((string id, int points)[] items, string binSource)
    {
        if (items == null || items.Length == 0 || !toastPanelPrefab || !styleProvider) return;

        // Normalise le côté : "r", "right", "bin_right", etc. => RIGHT
        string side = string.IsNullOrEmpty(binSource) ? "left" : binSource.Trim().ToLowerInvariant();
        bool isRight = side.StartsWith("r");

        RectTransform container = isRight ? rightContainer : leftContainer;
        Queue<RectTransform> queue = isRight ? rightActive : leftActive;
        if (!container) return;

        // Construit le texte multi-lignes (couleur par ligne, alpha forcé à 1)
        string rich = BuildRichText(items, out Color fallback);

        // Cap: enlève l’élément le plus ancien si nécessaire
        while (queue.Count >= maxPerSide)
        {
            var old = queue.Dequeue();
            if (old) Destroy(old.gameObject);
        }

        // Instancie le panneau (le VerticalLayoutGroup décidera de sa position)
        RectTransform panel = Instantiate(toastPanelPrefab, container);
        panel.gameObject.SetActive(true);

        // Renseigne le TMP du panneau
        TMP_Text tmp = panel.GetComponentInChildren<TMP_Text>(true);
        if (tmp)
        {
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = fontSize;

            // couleur de secours OPAQUE (si jamais une ligne n'a pas de balise)
            fallback.a = 1f;
            tmp.color = fallback;

            tmp.text = rich;
            tmp.alpha = 0f;

            // *** clé : fixe la hauteur via LayoutElement (pas de ContentSizeFitter) ***
            tmp.ForceMeshUpdate();
            float h = tmp.preferredHeight + verticalPadding;

            var le = panel.GetComponent<LayoutElement>();
            if (!le) le = panel.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;     // le LayoutGroup utilisera cette hauteur
            le.flexibleHeight = -1;    // pas d'expansion
        }

        // Anime uniquement le TMP (pas le panel) -> ne perturbe pas le layout
        StartCoroutine(CoPlay(panel, tmp));

        queue.Enqueue(panel);

        // Debug optionnel :
        // Debug.Log($"[Toast] {(isRight ? "RIGHT" : "LEFT")} lines={items.Length}");
    }

    /// <summary>
    /// Construit un bloc rich text multi-lignes (couleur par ligne, alpha=1).
    /// </summary>
    private string BuildRichText((string id, int points)[] items, out Color fallback)
    {
        fallback = Color.white;
        var sb = new StringBuilder(128);

        for (int i = 0; i < items.Length; i++)
        {
            var (id, pts) = items[i];
            var s = styleProvider.Build(id, pts); // s.line string, s.color Color

            s.color.a = 1f; // force opaque
            if (i == 0) fallback = s.color;

            string hex = ColorUtility.ToHtmlStringRGB(s.color); // pas d’alpha dans la balise
            sb.Append("<color=#").Append(hex).Append(">")
              .Append(s.line)
              .Append("</color>");
            if (i < items.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Animation du texte (slide + fade). Ne touche pas au panel (layout-safe).
    /// </summary>
    private IEnumerator CoPlay(RectTransform panel, TMP_Text tmp)
    {
        if (!panel || !tmp) yield break;

        RectTransform textRT = (RectTransform)tmp.transform;
        Vector2 start = Vector2.zero;
        Vector2 end = start + new Vector2(0f, moveUpDistance);

        float t = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / lifetime);

            // slide du texte (ease-out)
            float move = 1f - Mathf.Cos(u * Mathf.PI * 0.5f);
            textRT.anchoredPosition = Vector2.LerpUnclamped(start, end, move);

            // fade in/out
            if (u < fadeInLifetime)
                tmp.alpha = Mathf.InverseLerp(0f, fadeInLifetime, u);
            else if (u > 1f - fadeOutLifetime)
                tmp.alpha = 1f - Mathf.InverseLerp(1f - fadeOutLifetime, 1f, u);
            else
                tmp.alpha = 1f;

            yield return null;
        }

        Destroy(panel.gameObject);
    }
}
