using System.Collections;
using UnityEngine;

/// <summary>
/// Contrôle l'état visuel d'un bac (OPEN -> bleu / CLOSED -> rouge)
/// avec animation uniquement sur la barre qui apparaît.
/// - closed = false : bleu visible (barre bleue "plein scale")
/// - closed = true  : rouge visible (barre rouge "plein scale")
/// </summary>
public class BinVisualController : MonoBehaviour
{
    [Header("Références SpriteRenderers")]
    [SerializeField] private SpriteRenderer barBlue;   // État OPEN (idle)
    [SerializeField] private SpriteRenderer barRed;    // État CLOSED (Shift)

    [Header("Animation")]
    [SerializeField] private float transitionDuration = 0.12f;

    private Coroutine currentRoutine;

    // Scales "pleins" définis dans l'inspector
    private Vector3 baseBlueScale = Vector3.one;
    private Vector3 baseRedScale = Vector3.one;

    private void Awake()
    {
        if (barBlue != null)
            baseBlueScale = barBlue.transform.localScale;

        if (barRed != null)
            baseRedScale = barRed.transform.localScale;
    }

    private void Start()
    {
        // Idle = bleu plein, rouge repliée/invisible
        if (barBlue != null)
        {
            barBlue.enabled = true;
            barBlue.transform.localScale = baseBlueScale;
        }

        if (barRed != null)
        {
            barRed.enabled = false;
            barRed.transform.localScale = new Vector3(0f, baseRedScale.y, baseRedScale.z);
        }
    }

    /// <summary>
    /// Appelé par CloseBinController.
    /// closed = true  -> bleu disparaît, rouge s'anime en grossissant
    /// closed = false -> rouge disparaît, bleu s'anime en grossissant
    /// </summary>
    public void SetClosed(bool closed)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(AnimateToState(closed));
    }

    private IEnumerator AnimateToState(bool closed)
    {
        float t = 0f;

        if (closed)
        {
            // ON PASSE EN MODE ROUGE
            // 1) couper le bleu tout de suite
            if (barBlue != null)
            {
                barBlue.enabled = false;
                barBlue.transform.localScale = baseBlueScale; // on garde son scale plein en mémoire
            }

            // 2) préparer le rouge à apparaître
            if (barRed != null)
            {
                barRed.enabled = true;
                barRed.transform.localScale = new Vector3(0f, baseRedScale.y, baseRedScale.z);
            }

            // 3) animer seulement le rouge de 0 -> plein
            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / transitionDuration);
                float redX = Mathf.Lerp(0f, baseRedScale.x, k);

                if (barRed != null)
                    barRed.transform.localScale = new Vector3(redX, baseRedScale.y, baseRedScale.z);

                yield return null;
            }

            if (barRed != null)
                barRed.transform.localScale = baseRedScale;
        }
        else
        {
            // ON REPASSE EN MODE BLEU
            // 1) couper le rouge tout de suite
            if (barRed != null)
            {
                barRed.enabled = false;
                barRed.transform.localScale = baseRedScale; // garde le plein pour la prochaine fois
            }

            // 2) préparer le bleu à apparaître
            if (barBlue != null)
            {
                barBlue.enabled = true;
                barBlue.transform.localScale = new Vector3(0f, baseBlueScale.y, baseBlueScale.z);
            }

            // 3) animer seulement le bleu de 0 -> plein
            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / transitionDuration);
                float blueX = Mathf.Lerp(0f, baseBlueScale.x, k);

                if (barBlue != null)
                    barBlue.transform.localScale = new Vector3(blueX, baseBlueScale.y, baseBlueScale.z);

                yield return null;
            }

            if (barBlue != null)
                barBlue.transform.localScale = baseBlueScale;
        }

        currentRoutine = null;
    }
}
