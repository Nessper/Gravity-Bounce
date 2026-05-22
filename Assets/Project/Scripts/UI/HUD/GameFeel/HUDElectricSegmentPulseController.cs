using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HUDElectricPulseController : MonoBehaviour
{
    private enum PulseAxis
    {
        Horizontal,
        Vertical
    }

    private enum PulseDirection
    {
        Negative,
        Positive
    }

    [System.Serializable]
    private class PulseEntry
    {
        [Header("References")]
        public RectTransform rect;
        public Image image;

        [Header("Movement")]
        public PulseAxis axis = PulseAxis.Horizontal;
        public PulseDirection direction = PulseDirection.Positive;
        public float distance = 10f;
        public float duration = 0.06f;

        [Header("Timing")]
        public float delayMin = 2f;
        public float delayMax = 6f;

        [Header("Visual")]
        [Range(0f, 1f)] public float maxAlpha = 0.8f;
    }

    [Header("Pulses")]
    [SerializeField] private PulseEntry[] pulses;

    private Coroutine[] routines;
    private Vector2[] startPositions;

    private void OnEnable()
    {
        if (pulses == null || pulses.Length == 0)
            return;

        routines = new Coroutine[pulses.Length];
        startPositions = new Vector2[pulses.Length];

        for (int i = 0; i < pulses.Length; i++)
        {
            if (!IsValid(pulses[i]))
                continue;

            startPositions[i] = pulses[i].rect.anchoredPosition;
            SetAlpha(pulses[i].image, 0f);

            routines[i] = StartCoroutine(PulseRoutine(i));
        }
    }

    private void OnDisable()
    {
        if (routines != null)
        {
            for (int i = 0; i < routines.Length; i++)
            {
                if (routines[i] != null)
                    StopCoroutine(routines[i]);
            }
        }

        if (pulses != null)
        {
            for (int i = 0; i < pulses.Length; i++)
            {
                if (pulses[i] != null && pulses[i].image != null)
                    SetAlpha(pulses[i].image, 0f);

                if (pulses[i] != null && pulses[i].rect != null && startPositions != null && i < startPositions.Length)
                    pulses[i].rect.anchoredPosition = startPositions[i];
            }
        }
    }

    private IEnumerator PulseRoutine(int index)
    {
        PulseEntry pulse = pulses[index];

        while (true)
        {
            float delay = Random.Range(pulse.delayMin, pulse.delayMax);
            yield return new WaitForSecondsRealtime(delay);

            yield return PlayPulse(index);
        }
    }

    private IEnumerator PlayPulse(int index)
    {
        PulseEntry pulse = pulses[index];

        if (!IsValid(pulse))
            yield break;

        Vector2 start = startPositions[index];
        Vector2 end = start + GetOffset(pulse);

        float duration = Mathf.Max(0.001f, pulse.duration);
        float t = 0f;

        pulse.rect.anchoredPosition = start;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / duration);
            float eased = Mathf.SmoothStep(0f, 1f, k);

            pulse.rect.anchoredPosition = Vector2.Lerp(start, end, eased);

            float alpha = Mathf.Sin(k * Mathf.PI) * pulse.maxAlpha;
            SetAlpha(pulse.image, alpha);

            yield return null;
        }

        pulse.rect.anchoredPosition = start;
        SetAlpha(pulse.image, 0f);
    }

    private Vector2 GetOffset(PulseEntry pulse)
    {
        float sign = pulse.direction == PulseDirection.Positive ? 1f : -1f;

        if (pulse.axis == PulseAxis.Horizontal)
            return new Vector2(pulse.distance * sign, 0f);

        return new Vector2(0f, pulse.distance * sign);
    }

    private bool IsValid(PulseEntry pulse)
    {
        return pulse != null
            && pulse.rect != null
            && pulse.image != null;
    }

    private void SetAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }
}