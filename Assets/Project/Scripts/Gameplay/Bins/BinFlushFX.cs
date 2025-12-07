using System.Collections;
using UnityEngine;

/// <summary>
/// Gère le flash de flush d'un bin :
/// - Ne modifie QUE le SpriteRenderer interne (BinFlushGlow).
/// - Pas de shake, pas de scale punch.
/// - Couleur en fonction de la présence de bille noire + score du flush.
/// - Gère l'activation/désactivation du GameObject du glow si besoin.
/// </summary>
public class BinFlushFX : MonoBehaviour
{
    [Header("Référence du flash")]
    [Tooltip("SpriteRenderer du glow interne (BinFlushGlow), placé dans la cuve.")]
    [SerializeField] private SpriteRenderer flashRenderer;

    [Header("Timing du flash")]
    [Tooltip("Durée de montée de l'alpha (0 -> max).")]
    [SerializeField] private float flashUpTime = 0.05f;

    [Tooltip("Durée de descente de l'alpha (max -> 0).")]
    [SerializeField] private float flashDownTime = 0.20f;

    [Header("Intensité")]
    [Tooltip("Alpha max appliqué au SpriteRenderer (0.0 = invisible, 1.0 = très violent).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.75f;

    [Header("Couleurs")]
    [Tooltip("Couleur utilisée si une bille noire est présente dans le flush.")]
    [SerializeField] private Color blackFlushColor = Color.red;

    [Tooltip("Couleur par défaut si aucune range ne matche.")]
    [SerializeField] private Color defaultScoreColor = Color.cyan;

    [System.Serializable]
    public class FlushScoreColorRange
    {
        [Tooltip("Score minimum inclus pour cette couleur.")]
        public int minScore;

        [Tooltip("Score maximum inclus pour cette couleur.")]
        public int maxScore;

        [Tooltip("Couleur associée à cette plage de score.")]
        public Color color;
    }

    [Tooltip("Plages de score -> couleur (flush positif, etc.).")]
    [SerializeField] private FlushScoreColorRange[] scoreColorRanges;

    private Coroutine flashRoutine;

    private void Awake()
    {
        if (flashRenderer != null)
        {
            // Etat initial: alpha à 0.
            var c = flashRenderer.color;
            c.a = 0f;
            flashRenderer.color = c;

            // Si tu veux vraiment que le glow soit invisible de base,
            // on s'assure qu'il est désactivé.
            if (flashRenderer.gameObject.activeSelf)
            {
                flashRenderer.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Appelé par le BinCollector au moment d'un flush.
    /// hasBlackBall = true si au moins une bille noire dans ce flush.
    /// flushScore = score des billes de ce flush uniquement.
    /// </summary>
    public void PlayFlush(bool hasBlackBall, int flushScore)
    {
        if (flashRenderer == null)
            return;

        // On réactive l'objet du glow si nécessaire pour que le flash soit visible.
        if (!flashRenderer.gameObject.activeSelf)
        {
            flashRenderer.gameObject.SetActive(true);
        }

        Color baseColor = ResolveFlashColor(hasBlackBall, flushScore);

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine(baseColor));
    }

    private Color ResolveFlashColor(bool hasBlackBall, int flushScore)
    {
        if (hasBlackBall)
        {
            return ForceAlpha(blackFlushColor, 1f);
        }

        Color result = defaultScoreColor;

        if (scoreColorRanges != null)
        {
            for (int i = 0; i < scoreColorRanges.Length; i++)
            {
                var r = scoreColorRanges[i];
                if (flushScore >= r.minScore && flushScore <= r.maxScore)
                {
                    result = r.color;
                    break;
                }
            }
        }

        return ForceAlpha(result, 1f);
    }

    private Color ForceAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private IEnumerator FlashRoutine(Color baseColor)
    {
        // Montée
        float t = 0f;
        while (t < flashUpTime)
        {
            float k = flashUpTime > 0f ? t / flashUpTime : 1f;
            float a = Mathf.Lerp(0f, maxAlpha, k);

            Color c = baseColor;
            c.a = a;
            flashRenderer.color = c;

            t += Time.deltaTime;
            yield return null;
        }

        // Descente
        t = 0f;
        while (t < flashDownTime)
        {
            float k = flashDownTime > 0f ? t / flashDownTime : 1f;
            float a = Mathf.Lerp(maxAlpha, 0f, k);

            Color c = baseColor;
            c.a = a;
            flashRenderer.color = c;

            t += Time.deltaTime;
            yield return null;
        }

        // Sécurité: on coupe l'alpha et on désactive le glow.
        {
            Color c = flashRenderer.color;
            c.a = 0f;
            flashRenderer.color = c;

            // On re-désactive l'objet pour revenir à l'état "off" propre.
            if (flashRenderer.gameObject.activeSelf)
            {
                flashRenderer.gameObject.SetActive(false);
            }
        }

        flashRoutine = null;
    }
}
