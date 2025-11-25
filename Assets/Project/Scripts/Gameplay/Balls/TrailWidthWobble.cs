using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class TrailWidthWobble : MonoBehaviour
{
    [Header("Paramètres du wobble")]
    [SerializeField] private float amplitude = 0.03f;
    [SerializeField] private float frequency = 6f;
    [SerializeField] private float noiseSpeed = 1.4f;

    private TrailRenderer tr;
    private AnimationCurve baseCurve;

    private void Awake()
    {
        tr = GetComponent<TrailRenderer>();

        // On garde une copie de la width curve initiale
        baseCurve = new AnimationCurve(tr.widthCurve.keys);
    }

    private void Update()
    {
        float t = Time.time;

        // Bruit pour éviter un wobble trop “mathématique”
        float noise = Mathf.PerlinNoise(t * noiseSpeed, 0f) * 2f - 1f;

        // Valeur d’oscillation
        float wobble = Mathf.Sin(t * frequency) * amplitude + noise * amplitude * 0.5f;

        // Nouvelle width curve
        AnimationCurve curve = new AnimationCurve();

        for (int i = 0; i < baseCurve.keys.Length; i++)
        {
            Keyframe k = baseCurve.keys[i];
            k.value = Mathf.Max(0f, baseCurve.keys[i].value + wobble);
            curve.AddKey(k);
        }

        tr.widthCurve = curve;
    }
}
