using System.Collections;
using UnityEngine;

public class FlushComboOverlayController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    [Header("Base Score UI")]
    [SerializeField] private BallScoreUI ballScorePrefab;

    [SerializeField] private RectTransform leftBinScoreRoot;
    [SerializeField] private RectTransform rightBinScoreRoot;

    [Header("Base Score Timing")]
    [SerializeField] private float baseItemDelay = 0.04f;

    [Header("Base Score Scatter")]
    [SerializeField] private float scatterRadius = 10f;

    private Coroutine playRoutine;

    public void Play(FlushResolution resolution)
    {
        if (resolution == null)
            return;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(
            PlayRoutine(resolution));
    }

    private IEnumerator PlayRoutine(
        FlushResolution resolution)
    {
        if (verboseLogs)
        {
            Debug.Log(
                $"[FlushComboOverlay] " +
                $"Base={resolution.BaseTotal} " +
                $"Combo={resolution.ComboTotal} " +
                $"Final={resolution.FinalTotal}");
        }

        yield return PlayBaseLayer(resolution);

        playRoutine = null;
    }

    private IEnumerator PlayBaseLayer(
        FlushResolution resolution)
    {
        if (resolution.BaseItems == null)
            yield break;

        RectTransform root =
            GetScoreRoot(resolution);

        if (root == null)
        {
            Debug.LogWarning(
                "[FlushComboOverlay] Missing score root.");
            yield break;
        }

        if (ballScorePrefab == null)
        {
            Debug.LogWarning(
                "[FlushComboOverlay] Missing BallScore prefab.");
            yield break;
        }

        for (int i = 0; i < resolution.BaseItems.Count; i++)
        {
            SpawnBallScore(
                resolution.BaseItems[i],
                root);

            yield return new WaitForSeconds(
                baseItemDelay);
        }
    }

    private void SpawnBallScore(
        BaseScoreItem item,
        RectTransform root)
    {

        if (ballScorePrefab == null)
            return;

        if (root == null)
            return;

        BallScoreUI scoreUI =
            Instantiate(ballScorePrefab, root);

        scoreUI.gameObject.SetActive(true);

        Vector2 offset =
            Random.insideUnitCircle * scatterRadius;

        Color color =
            item.Points >= 0
                ? Color.white
                : Color.red;

        scoreUI.Play(
            item.Points,
            color,
            offset);
    }

    private RectTransform GetScoreRoot(
        FlushResolution resolution)
    {
        if (resolution == null)
            return null;

        if (resolution.BinSide == BinSide.Left)
            return leftBinScoreRoot;

        if (resolution.BinSide == BinSide.Right)
            return rightBinScoreRoot;

        return leftBinScoreRoot;
    }
}