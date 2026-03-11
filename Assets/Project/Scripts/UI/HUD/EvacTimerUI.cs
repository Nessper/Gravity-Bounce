using TMPro;
using UnityEngine;

/// <summary>
/// UI dédiée à l'évacuation :
/// - Affiche un compteur en secondes (ceil).
/// - Ne joue jamais de "GO".
/// - Se masque automatiquement quand le compteur atteint 0.
/// </summary>
public class EvacTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    [Header("Audio")]
    [Tooltip("Si vrai, joue EvacTick à chaque changement de valeur affichée (> 0).")]
    [SerializeField] private bool playTickSfx = true;

    private int lastDisplayed = -1;

    public void OnEvacStart()
    {
        lastDisplayed = -1;

        if (text != null)
            text.text = string.Empty;

        EnsureVisible();
    }

    public void OnEvacTick(float remainingSeconds)
    {
        if (text == null)
            return;

        int v = Mathf.CeilToInt(remainingSeconds);

        // Dès que le compteur arrive à 0 -> on masque l'UI
        if (v <= 0)
        {
            Hide();
            return;
        }

        if (v == lastDisplayed)
            return;

        lastDisplayed = v;
        text.text = v.ToString();

        if (playTickSfx)
            BootRoot.Audio?.PlaySfx(SfxId.EvacTick);
    }

    public void Hide()
    {
        if (text != null)
            text.gameObject.SetActive(false);
    }

    private void EnsureVisible()
    {
        if (text != null && !text.gameObject.activeSelf)
            text.gameObject.SetActive(true);
    }
}
