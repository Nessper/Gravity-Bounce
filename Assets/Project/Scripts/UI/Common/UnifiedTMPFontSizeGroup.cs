using UnityEngine;
using TMPro;
using System.Collections;

public class UnifiedTMPFontSizeGroup : MonoBehaviour
{
    [Header("Références TMP à unifier")]
    [SerializeField] private TMP_Text[] texts;

    [Header("Paramètres d'auto-size")]
    [SerializeField] private float minFontSize = 10f;
    [SerializeField] private float maxFontSize = 36f;

    private void OnEnable()
    {
        // On attend un frame que les layouts se mettent en place
        StartCoroutine(DelayedApply());
    }

    private IEnumerator DelayedApply()
    {
        // 1 frame (voire 2 si tu veux être ultra safe)
        yield return null;
        //yield return null;

        Canvas.ForceUpdateCanvases();
        ApplyUnifiedFontSize();
    }

    public void ApplyUnifiedFontSize()
    {
        if (texts == null || texts.Length == 0)
            return;

        // 1) Laisser chaque TMP auto-sizer
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;

            t.enableAutoSizing = true;
            t.fontSizeMin = minFontSize;
            t.fontSizeMax = maxFontSize;
            t.ForceMeshUpdate();
        }

        // 2) Trouver la plus petite taille calculée
        float smallest = maxFontSize;

        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;

            if (t.fontSize < smallest)
                smallest = t.fontSize;
        }

        // 3) Appliquer cette taille à tout le monde
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;

            t.enableAutoSizing = false;
            t.fontSize = smallest;
        }
    }
}
