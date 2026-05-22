using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HUDPathRunnerController : MonoBehaviour
{
    [System.Serializable]
    public class GhostSettings
    {
        [Range(0f, 1f)] public float alpha = 0.15f;
        [Min(1)] public int frameOffset = 3;
    }

    [Header("References")]
    [SerializeField] private RectTransform runner;
    [SerializeField] private Image runnerImage;
    [SerializeField] private RectTransform[] pathPoints;

    [Header("Ghost Trail")]
    [SerializeField]
    private GhostSettings[] ghosts =
    {
        new GhostSettings { alpha = 0.18f, frameOffset = 2 },
        new GhostSettings { alpha = 0.10f, frameOffset = 4 },
        new GhostSettings { alpha = 0.04f, frameOffset = 6 }
    };

    [Header("Movement")]
    [SerializeField] private float pixelsPerSecond = 140f;
    [SerializeField] private bool rotateAlongPath = true;
    [SerializeField] private float angleOffset = 0f;

    [Header("Cycle")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float startDelay = 1f;

    [Header("Runner Alpha Cycle")]
    [SerializeField, Range(0f, 1f)] private float runnerMinAlpha = 0.08f;
    [SerializeField, Range(0f, 1f)] private float runnerMaxAlpha = 0.35f;
    [SerializeField] private float holdMaxDuration = 0.25f;
    [SerializeField] private float fadeToMinDuration = 0.45f;
    [SerializeField] private float holdMinDuration = 0.15f;
    [SerializeField] private float fadeToMaxDuration = 0.25f;

    private struct TrailSample
    {
        public Vector2 position;
        public Quaternion rotation;
    }

    private const int TrailBufferSize = 256;
    private readonly TrailSample[] trailBuffer = new TrailSample[TrailBufferSize];

    private RectTransform[] ghostRects;
    private Image[] ghostImages;

    private Coroutine routine;
    private int currentPointIndex;
    private float segmentProgress;
    private int trailWriteIndex;
    private bool hasTrailSamples;
    private float currentRunnerAlpha;

    private void Awake()
    {
        CreateGhosts();
    }

    private void OnEnable()
    {
        HideAll();

        if (playOnStart)
            routine = StartCoroutine(RunLoop());
    }

    private void OnDisable()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = null;
        HideAll();
    }

    private void CreateGhosts()
    {
        if (runner == null || runnerImage == null || ghosts == null || ghosts.Length <= 0)
            return;

        ghostRects = new RectTransform[ghosts.Length];
        ghostImages = new Image[ghosts.Length];

        for (int i = 0; i < ghosts.Length; i++)
        {
            GameObject ghost = new GameObject(
                "HUDRunnerGhost_" + (i + 1),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            ghost.transform.SetParent(runner.parent, false);
            ghost.transform.SetSiblingIndex(runner.GetSiblingIndex());

            RectTransform rect = ghost.GetComponent<RectTransform>();
            Image img = ghost.GetComponent<Image>();

            rect.anchorMin = runner.anchorMin;
            rect.anchorMax = runner.anchorMax;
            rect.pivot = runner.pivot;
            rect.sizeDelta = runner.sizeDelta;
            rect.localScale = runner.localScale;

            img.sprite = runnerImage.sprite;
            img.raycastTarget = false;
            img.color = runnerImage.color;

            ghostRects[i] = rect;
            ghostImages[i] = img;
        }
    }

    private IEnumerator RunLoop()
    {
        HideAll();
        SnapToStart();

        if (startDelay > 0f)
            yield return new WaitForSecondsRealtime(startDelay);

        SetRunnerAlpha(runnerMaxAlpha);

        while (true)
        {
            bool completed = false;

            while (!completed)
            {
                completed = MoveAlongPath(Time.unscaledDeltaTime);

                UpdateRunnerAlphaCycle();
                RecordTrailSample();
                UpdateGhosts();

                yield return null;
            }

            SnapToStart();
        }
    }

    private bool MoveAlongPath(float deltaTime)
    {
        if (!CanRun())
            return true;

        RectTransform startPoint = pathPoints[currentPointIndex];
        RectTransform endPoint = pathPoints[GetNextIndex()];

        Vector2 startPos = startPoint.anchoredPosition;
        Vector2 endPos = endPoint.anchoredPosition;

        float distance = Vector2.Distance(startPos, endPos);

        if (distance <= 0.01f)
        {
            currentPointIndex++;
            segmentProgress = 0f;
            return currentPointIndex >= pathPoints.Length;
        }

        segmentProgress += (pixelsPerSecond * deltaTime) / distance;

        if (segmentProgress >= 1f)
        {
            runner.anchoredPosition = endPos;
            ApplyRotation(startPos, endPos);

            currentPointIndex++;
            segmentProgress = 0f;

            return currentPointIndex >= pathPoints.Length;
        }

        runner.anchoredPosition = Vector2.Lerp(startPos, endPos, segmentProgress);
        ApplyRotation(startPos, endPos);

        return false;
    }

    private void ApplyRotation(Vector2 startPos, Vector2 endPos)
    {
        if (!rotateAlongPath || runner == null)
            return;

        Vector2 direction = endPos - startPos;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        runner.localRotation = Quaternion.Euler(0f, 0f, angle + angleOffset);
    }

    private bool CanRun()
    {
        return runner != null
            && runnerImage != null
            && pathPoints != null
            && pathPoints.Length >= 2
            && currentPointIndex >= 0
            && currentPointIndex < pathPoints.Length
            && pathPoints[currentPointIndex] != null
            && pathPoints[GetNextIndex()] != null;
    }

    private int GetNextIndex()
    {
        if (currentPointIndex >= pathPoints.Length - 1)
            return 0;

        return currentPointIndex + 1;
    }

    private void UpdateRunnerAlphaCycle()
    {
        float total =
            holdMaxDuration
            + fadeToMinDuration
            + holdMinDuration
            + fadeToMaxDuration;

        if (total <= 0.001f)
        {
            SetRunnerAlpha(runnerMaxAlpha);
            return;
        }

        float t = Time.unscaledTime % total;

        if (t < holdMaxDuration)
        {
            SetRunnerAlpha(runnerMaxAlpha);
            return;
        }

        t -= holdMaxDuration;

        if (t < fadeToMinDuration)
        {
            float k = Mathf.Clamp01(t / fadeToMinDuration);
            SetRunnerAlpha(Mathf.Lerp(runnerMaxAlpha, runnerMinAlpha, k));
            return;
        }

        t -= fadeToMinDuration;

        if (t < holdMinDuration)
        {
            SetRunnerAlpha(runnerMinAlpha);
            return;
        }

        t -= holdMinDuration;

        float upK = Mathf.Clamp01(t / fadeToMaxDuration);
        SetRunnerAlpha(Mathf.Lerp(runnerMinAlpha, runnerMaxAlpha, upK));
    }

    private void SetRunnerAlpha(float alpha)
    {
        currentRunnerAlpha = Mathf.Clamp01(alpha);

        if (runnerImage == null)
            return;

        Color c = runnerImage.color;
        c.a = currentRunnerAlpha;
        runnerImage.color = c;
    }

    private void RecordTrailSample()
    {
        if (runner == null)
            return;

        trailBuffer[trailWriteIndex].position = runner.anchoredPosition;
        trailBuffer[trailWriteIndex].rotation = runner.localRotation;

        trailWriteIndex = (trailWriteIndex + 1) % TrailBufferSize;
        hasTrailSamples = true;
    }

    private void UpdateGhosts()
    {
        if (!hasTrailSamples || ghostRects == null || ghostImages == null || ghosts == null)
            return;

        int count = Mathf.Min(ghostRects.Length, ghostImages.Length, ghosts.Length);

        for (int i = 0; i < count; i++)
        {
            if (ghostRects[i] == null || ghostImages[i] == null || ghosts[i] == null)
                continue;

            int offset = Mathf.Max(1, ghosts[i].frameOffset);
            int sampleIndex = trailWriteIndex - offset;

            while (sampleIndex < 0)
                sampleIndex += TrailBufferSize;

            TrailSample sample = trailBuffer[sampleIndex];

            ghostRects[i].anchoredPosition = sample.position;
            ghostRects[i].localRotation = sample.rotation;

            Color c = ghostImages[i].color;
            c.a = Mathf.Clamp01(currentRunnerAlpha * ghosts[i].alpha);
            ghostImages[i].color = c;
        }
    }

    private void ClearTrail()
    {
        trailWriteIndex = 0;
        hasTrailSamples = false;

        if (ghostImages == null)
            return;

        for (int i = 0; i < ghostImages.Length; i++)
        {
            if (ghostImages[i] == null)
                continue;

            Color c = ghostImages[i].color;
            c.a = 0f;
            ghostImages[i].color = c;
        }
    }

    private void HideAll()
    {
        SetRunnerAlpha(0f);
        ClearTrail();
    }

    public void SnapToStart()
    {
        currentPointIndex = 0;
        segmentProgress = 0f;

        if (runner != null && pathPoints != null && pathPoints.Length > 0 && pathPoints[0] != null)
            runner.anchoredPosition = pathPoints[0].anchoredPosition;
    }
}