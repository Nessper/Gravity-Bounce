using UnityEngine;

/// <summary>
/// Affiche les médailles du reveal sous la barre de score.
/// Reflète l'état courant exact dérivé du score.
/// </summary>
public class EndLevelMedalsUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private CanvasGroup bronzeCG;
    [SerializeField] private CanvasGroup silverCG;
    [SerializeField] private CanvasGroup goldCG;

    private void Awake()
    {
        ResetInstant();
    }

    public void ResetInstant()
    {
        SetVisible(bronzeCG, false);
        SetVisible(silverCG, false);
        SetVisible(goldCG, false);
    }

    public void SetDisplayedMedalInstant(EndMedal medal)
    {
        SetVisible(bronzeCG, medal >= EndMedal.Bronze);
        SetVisible(silverCG, medal >= EndMedal.Silver);
        SetVisible(goldCG, medal >= EndMedal.Gold);
    }

    private void SetVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null)
            return;

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.transform.localScale = Vector3.one;
    }
}