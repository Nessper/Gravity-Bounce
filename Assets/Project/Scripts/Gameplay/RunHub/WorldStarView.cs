using UnityEngine;
using UnityEngine.UI;

public class WorldStarView : MonoBehaviour
{
    public enum StarState
    {
        Done,
        Current,
        Locked
    }

    [Header("UI")]
    [SerializeField] private Image starImage;
    [Header("Ship Badge")]
    [SerializeField] private GameObject shipBadge;


    [Header("Base Color (identite du monde)")]
    [SerializeField] private Color baseColor = Color.white;

    [Header("State")]
    [SerializeField] private StarState state = StarState.Locked;

    [Header("Pulse Speed")]
    [SerializeField] private float donePulseSpeed = 0.9f;
    [SerializeField] private float currentPulseSpeed = 1.6f;
    [SerializeField] private float lockedPulseSpeed = 0.8f;

    [Header("Scale Pulse Amplitude")]
    [SerializeField] private float doneScaleAmplitude = 0.03f;
    [SerializeField] private float currentScaleAmplitude = 0.07f;
    [SerializeField] private float lockedScaleAmplitude = 0.02f;

    [Header("Intensity")]
    [SerializeField] private float doneBrightness = 0.85f;
    [SerializeField] private float currentBrightness = 1.05f;
    [SerializeField] private float lockedBrightness = 0.55f;

    [Header("Locked Tint")]
    [Range(0f, 1f)]
    [SerializeField] private float lockedTintAmount = 0.2f;

    [Header("Alpha")]
    [SerializeField] private float alpha = 1f;

    private float timeOffset;
    private Vector3 baseScale;

    private void Awake()
    {
        if (starImage == null)
            starImage = GetComponent<Image>();

        timeOffset = Random.value * 10f;
        baseScale = transform.localScale;

        // Securite: evite les surprises si l Image a ete teintee a la main.
        if (starImage != null)
            starImage.color = Color.white;
    }

    private void Update()
    {
        if (starImage == null)
            return;

        float speed = donePulseSpeed;
        float scaleAmp = doneScaleAmplitude;
        float brightness = doneBrightness;
        Color color = baseColor;

        switch (state)
        {
            case StarState.Done:
                speed = donePulseSpeed;
                scaleAmp = doneScaleAmplitude;
                brightness = doneBrightness;
                break;

            case StarState.Current:
                speed = currentPulseSpeed;
                scaleAmp = currentScaleAmplitude;
                brightness = currentBrightness;
                break;

            default:
                speed = lockedPulseSpeed;
                scaleAmp = lockedScaleAmplitude;
                brightness = lockedBrightness;
                color = BuildLockedTint(baseColor, lockedTintAmount);
                break;
        }

        // Pulse 0..1 (unscaled pour ne pas etre affecte par le pause/timeScale)
        float s = Mathf.Sin((Time.unscaledTime + timeOffset) * speed);
        float pulse01 = 0.5f + 0.5f * s;

        // 1) Scintillement par grossissement (pas de disparition)
        float scale = 1f + (pulse01 - 0.5f) * 2f * scaleAmp;
        transform.localScale = baseScale * scale;

        // 2) Couleur: luminosite uniquement + alpha fixe
        color.a = 1f;

        Color final = color;
        final.r *= brightness;
        final.g *= brightness;
        final.b *= brightness;
        final.a = Mathf.Clamp01(alpha);

        starImage.color = final;
    }

    public void SetState(StarState newState)
    {
        state = newState;
    }

    public void SetBaseColor(Color c)
    {
        baseColor = c;
    }

    private static Color BuildLockedTint(Color source, float amount)
    {
        // Gris teinte: on garde une petite part de la couleur du monde.
        Color gray = new Color(0.2f, 0.2f, 0.2f, 1f);
        Color src = new Color(source.r, source.g, source.b, 1f);
        Color tinted = Color.Lerp(gray, src, Mathf.Clamp01(amount));
        tinted.a = 1f;
        return tinted;
    }

    public void SetShipBadgeVisible(bool visible)
    {
        if (shipBadge != null)
            shipBadge.SetActive(visible);
    }

}
