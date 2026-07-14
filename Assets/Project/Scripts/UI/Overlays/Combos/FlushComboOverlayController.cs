using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class ComboSourceBinding
{
    public string ComboId;
    public RectTransform SourceRoot;
    public Vector2 SpawnOffset;
    public Vector2 ImpulseDirection = Vector2.up;
}

public class FlushComboOverlayController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    [Header("Definitions")]
    [SerializeField] private BallDefinitionCatalog ballCatalog;

    [Header("Base Score UI")]
    [SerializeField] private BallScoreUI ballScorePrefab;
    [SerializeField] private RectTransform leftBinScoreRoot;
    [SerializeField] private RectTransform rightBinScoreRoot;

    [Header("Combo Score UI")]
    [SerializeField] private ComboScoreUI comboScorePrefab;

    [Header("Special Combo Sources")]
    [SerializeField] private ComboSourceBinding[] comboSourceBindings;

    [Header("Attraction")]
    [SerializeField] private ScoreAttractorUI scoreAttractor;
    [SerializeField] private ScoreFlushAbsorberUI flushAbsorber;
    [SerializeField] private GameplayScoreImpactUI gameplayScoreImpactUI;

    [Header("Timing")]
    [SerializeField] private float baseItemDelay = 0.04f;
    [SerializeField] private float delayBeforeCombos = 0.12f;
    [SerializeField] private float comboItemDelay = 0.18f;
    [FormerlySerializedAs("delayBeforeAttraction")]
    [SerializeField] private float floatDuration = 0.35f;

    [Header("Final Flush Guard")]
    [SerializeField] private float finalFlushMaxVisualDelay = 3.5f;

    [Header("Base Score Expulsion")]
    [SerializeField] private float baseScatterRadius = 10f;
    [SerializeField] private float baseImpulseMin = 35f;
    [SerializeField] private float baseImpulseMax = 70f;

    [Header("Combo Score Expulsion")]
    [SerializeField] private float comboScatterRadius = 6f;
    [SerializeField] private float comboImpulseMin = 20f;
    [SerializeField] private float comboImpulseMax = 45f;
    [SerializeField] private float comboBaseVerticalOffset = 36f;
    [SerializeField] private float comboVerticalSpacing = 34f;

    private readonly HashSet<GameObject> activePackets =
        new HashSet<GameObject>();

    private readonly HashSet<int> activeSequenceIds =
        new HashSet<int>();

    private int nextSequenceId;
    private bool isCancelling;

    private void OnDisable()
    {
        CancelAllAndSync();
    }

    public void Play(FlushResolution resolution)
    {
        if (resolution == null)
            return;

        int sequenceId = ++nextSequenceId;

        activeSequenceIds.Add(sequenceId);
        gameplayScoreImpactUI?.BeginVisualSequence();

        StartCoroutine(PlayRoutine(resolution, sequenceId));
    }

    public IEnumerator WaitForFinalPresentationComplete()
    {
        float deadline = finalFlushMaxVisualDelay > 0f
            ? Time.realtimeSinceStartup + finalFlushMaxVisualDelay
            : float.PositiveInfinity;

        while (activeSequenceIds.Count > 0 &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (activeSequenceIds.Count == 0)
            gameplayScoreImpactUI?.FinalizeImpactSessionForEndSequence();

        while (gameplayScoreImpactUI != null &&
               gameplayScoreImpactUI.HasPendingImpactSessionPresentation &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        bool timedOut =
            activeSequenceIds.Count > 0 ||
            (gameplayScoreImpactUI != null &&
             gameplayScoreImpactUI.HasPendingImpactSessionPresentation);

        if (timedOut)
        {
            CancelAllAndSync();
            yield break;
        }

        gameplayScoreImpactUI?.ForceResync();
    }

    public void CancelAllAndSync()
    {
        if (isCancelling)
            return;

        isCancelling = true;

        StopAllCoroutines();
        scoreAttractor?.CancelAll();

        foreach (GameObject packet in activePackets)
        {
            if (packet != null)
                Destroy(packet);
        }

        activePackets.Clear();
        activeSequenceIds.Clear();

        flushAbsorber?.ResetQueue(syncHud: false);
        gameplayScoreImpactUI?.CancelVisualSequencesAndResync();

        isCancelling = false;
    }

    private IEnumerator PlayRoutine(
        FlushResolution resolution,
        int sequenceId)
    {
        List<BallScoreUI> activeBallScores = new List<BallScoreUI>();
        List<ComboScoreUI> activeComboScores = new List<ComboScoreUI>();

        if (verboseLogs)
        {
            Debug.Log(
                "[FlushComboOverlay] Base=" + resolution.BaseTotal +
                " Combo=" + resolution.ComboTotal +
                " Final=" + resolution.FinalTotal
            );
        }

        yield return PlayBaseLayer(resolution, activeBallScores);

        if (resolution.HasCombos)
        {
            yield return new WaitForSeconds(delayBeforeCombos);
            yield return PlayComboLayer(resolution, activeComboScores);
        }

        if (floatDuration > 0f)
            yield return new WaitForSeconds(floatDuration);

        yield return AttractScoresRoutine(
            activeBallScores,
            activeComboScores
        );

        CompleteSequence(sequenceId);
    }

    private IEnumerator PlayBaseLayer(
        FlushResolution resolution,
        List<BallScoreUI> activeBallScores)
    {
        if (resolution.BaseItems == null)
            yield break;

        RectTransform root = GetScoreRoot(resolution);

        if (root == null || ballScorePrefab == null)
            yield break;

        for (int i = 0; i < resolution.BaseItems.Count; i++)
        {
            SpawnBallScore(
                resolution.BaseItems[i],
                root,
                resolution.BinSide,
                activeBallScores
            );

            if (baseItemDelay > 0f)
                yield return new WaitForSeconds(baseItemDelay);
        }
    }

    private IEnumerator PlayComboLayer(
        FlushResolution resolution,
        List<ComboScoreUI> activeComboScores)
    {
        if (resolution.ComboEvents == null || comboScorePrefab == null)
            yield break;

        int standardComboIndex = 0;

        for (int i = 0; i < resolution.ComboEvents.Count; i++)
        {
            ComboEvent combo = resolution.ComboEvents[i];
            ComboDefinition definition = GetComboDefinition(combo.Id);
            ComboSourceBinding binding = GetBinding(combo.Id);

            bool hasSpecialSource =
                binding != null &&
                binding.SourceRoot != null;

            int indexForOffset = standardComboIndex;

            if (!hasSpecialSource)
                standardComboIndex++;

            SpawnComboScore(
                combo,
                definition,
                resolution,
                indexForOffset,
                binding,
                activeComboScores
            );

            if (comboItemDelay > 0f)
                yield return new WaitForSeconds(comboItemDelay);
        }
    }

    private IEnumerator AttractScoresRoutine(
        List<BallScoreUI> activeBallScores,
        List<ComboScoreUI> activeComboScores)
    {
        if (scoreAttractor == null)
        {
            CleanupPackets(activeBallScores, activeComboScores);
            yield break;
        }

        yield return scoreAttractor.AbsorbScores(
            activeBallScores,
            activeComboScores,
            HandlePacketFinished
        );

        activeBallScores.Clear();
        activeComboScores.Clear();
    }

    private void SpawnBallScore(
        BaseScoreItem item,
        RectTransform root,
        BinSide side,
        List<BallScoreUI> activeBallScores)
    {
        if (ballScorePrefab == null || root == null)
            return;

        BallScoreUI scoreUI = Instantiate(ballScorePrefab, root);
        scoreUI.gameObject.SetActive(true);

        Vector2 offset =
            Random.insideUnitCircle * baseScatterRadius;

        Vector2 impulse =
            BuildBinExpulsionVelocity(
                side,
                baseImpulseMin,
                baseImpulseMax
            );

        Color color = GetBallScoreColor(item);

        scoreUI.Play(
            item.Points,
            color,
            offset,
            impulse
        );

        activeBallScores.Add(scoreUI);
        activePackets.Add(scoreUI.gameObject);
    }

    private void SpawnComboScore(
        ComboEvent combo,
        ComboDefinition definition,
        FlushResolution resolution,
        int index,
        ComboSourceBinding binding,
        List<ComboScoreUI> activeComboScores)
    {
        RectTransform root = GetComboRoot(combo, resolution);

        if (comboScorePrefab == null || root == null)
            return;

        ComboScoreUI scoreUI = Instantiate(comboScorePrefab, root);
        scoreUI.gameObject.SetActive(true);

        RectTransform scoreRect = scoreUI.transform as RectTransform;

        if (scoreRect != null)
        {
            scoreRect.position = root.position;
            scoreRect.localRotation = Quaternion.identity;
            scoreRect.localScale = Vector3.one;
        }

        bool hasSpecialSource =
            binding != null &&
            binding.SourceRoot != null;

        Vector2 offset;
        Vector2 impulse;

        if (hasSpecialSource)
        {
            offset =
                binding.SpawnOffset +
                Random.insideUnitCircle * comboScatterRadius;

            impulse =
                BuildDirectedExpulsionVelocity(
                    binding.ImpulseDirection,
                    comboImpulseMin,
                    comboImpulseMax
                );
        }
        else
        {
            offset =
                Vector2.up * (comboBaseVerticalOffset + index * comboVerticalSpacing) +
                Random.insideUnitCircle * comboScatterRadius;

            impulse =
                BuildBinExpulsionVelocity(
                    resolution.BinSide,
                    comboImpulseMin,
                    comboImpulseMax
                );
        }

        string displayName = combo.Id;
        Color color = Color.white;

        if (definition != null)
        {
            displayName = definition.Id;
            color = definition.UiColor;
        }

        scoreUI.Play(
            displayName,
            combo.Points,
            color,
            offset,
            impulse
        );

        activeComboScores.Add(scoreUI);
        activePackets.Add(scoreUI.gameObject);
    }

    private void CompleteSequence(int sequenceId)
    {
        if (!activeSequenceIds.Remove(sequenceId))
            return;

        gameplayScoreImpactUI?.EndVisualSequence();
    }

    private void HandlePacketFinished(GameObject packet)
    {
        if (packet != null)
            activePackets.Remove(packet);

        RemoveDestroyedPacketEntries();
    }

    private void CleanupPackets(
        List<BallScoreUI> ballScores,
        List<ComboScoreUI> comboScores)
    {
        if (ballScores != null)
        {
            for (int i = 0; i < ballScores.Count; i++)
                CleanupPacket(ballScores[i]);
        }

        if (comboScores != null)
        {
            for (int i = 0; i < comboScores.Count; i++)
                CleanupPacket(comboScores[i]);
        }
    }

    private void CleanupPacket(MonoBehaviour packetComponent)
    {
        if (packetComponent == null)
            return;

        GameObject packet = packetComponent.gameObject;
        activePackets.Remove(packet);
        Destroy(packet);
    }

    private void RemoveDestroyedPacketEntries()
    {
        activePackets.RemoveWhere(packet => packet == null);
    }

    private Color GetBallScoreColor(BaseScoreItem item)
    {
        if (ballCatalog != null &&
            ballCatalog.TryGet(item.BallId, out BallDefinition definition))
        {
            return definition.ScoreColor;
        }

        return item.Points >= 0
            ? Color.white
            : Color.red;
    }

    private ComboDefinition GetComboDefinition(string comboId)
    {
        if (ComboDefinitionProvider.Instance == null)
            return null;

        return ComboDefinitionProvider.Instance.Get(comboId);
    }

    private RectTransform GetScoreRoot(FlushResolution resolution)
    {
        if (resolution == null)
            return null;

        if (resolution.BinSide == BinSide.Left)
            return leftBinScoreRoot;

        if (resolution.BinSide == BinSide.Right)
            return rightBinScoreRoot;

        return leftBinScoreRoot;
    }

    private RectTransform GetComboRoot(
        ComboEvent combo,
        FlushResolution resolution)
    {
        ComboSourceBinding binding = GetBinding(combo.Id);

        if (binding != null && binding.SourceRoot != null)
            return binding.SourceRoot;

        return GetScoreRoot(resolution);
    }

    private ComboSourceBinding GetBinding(string comboId)
    {
        if (string.IsNullOrWhiteSpace(comboId))
            return null;

        if (comboSourceBindings == null)
            return null;

        for (int i = 0; i < comboSourceBindings.Length; i++)
        {
            ComboSourceBinding binding = comboSourceBindings[i];

            if (binding == null)
                continue;

            if (binding.ComboId == comboId)
                return binding;
        }

        return null;
    }

    private Vector2 BuildBinExpulsionVelocity(
        BinSide side,
        float minSpeed,
        float maxSpeed)
    {
        float horizontalSign = 0f;

        if (side == BinSide.Left)
            horizontalSign = 1f;
        else if (side == BinSide.Right)
            horizontalSign = -1f;

        Vector2 direction = new Vector2(
            horizontalSign * Random.Range(0.25f, 0.75f),
            Random.Range(0.65f, 1.15f)
        );

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        float speed = Random.Range(minSpeed, maxSpeed);

        return direction * speed;
    }

    private Vector2 BuildDirectedExpulsionVelocity(
        Vector2 direction,
        float minSpeed,
        float maxSpeed)
    {
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        float speed = Random.Range(minSpeed, maxSpeed);

        return direction * speed;
    }
}
