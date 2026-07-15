using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class K2DroneInterceptorController : DroneRuntimeControllerBase
{
    private const string WhiteBallId = "white";
    private const string BlueBallId = "blue";
    private const string RedBallId = "red";

    [Header("Detection K2")]
    [SerializeField] private DroneInterceptionZone interceptionZone;
    [SerializeField] private PlayerController paddle;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Presentation de charge K2")]
    [SerializeField] private Sprite k2UnchargedSprite;
    [SerializeField] private Sprite k2ChargedSprite;
    [SerializeField] private Sprite k2CooldownSprite;

    [Header("Patrouille horizontale")]
    [SerializeField] private float patrolY = -4.35f;
    [SerializeField] private float fallbackPatrolRange = 2.2f;
    [SerializeField] private float patrolExtraRange = 1f;
    [SerializeField] private float patrolSpeed = 1.35f;
    [SerializeField] private float patrolVerticalReturnSpeed = 4f;

    [Header("Interception")]
    [SerializeField] private float interceptionSpeed = 12f;
    [SerializeField] private float droneOffsetAboveBall = 0.3f;
    [SerializeField] private float captureSnapDuration = 0.1f;
    [SerializeField] private float holdDuration = 0.12f;

    [Header("Teleportation")]
    [SerializeField] private float teleportFlashLeadDuration = 0.1f;
    [SerializeField] private float teleportFlashDuration = 0.22f;
    [SerializeField] private float reentryRevealDelay = 0.12f;
    [SerializeField] private float reentryFlashDuration = 0.42f;
    [SerializeField] private float teleportFlashScale = 0.7f;
    [SerializeField] private Color teleportFlashColor = Color.white;
    [SerializeField] private Shader teleportFlashShader;

    private Coroutine interceptionCoroutine;
    private BallState capturedTarget;
    private float patrolDirection = 1f;
    private bool capturedTargetVisualHidden;
    private Image k2CooldownImage;
    private Image k2UnchargedImage;
    private Image k2ChargedImage;
    private Sprite runtimeTeleportFlashSprite;
    private Material runtimeTeleportFlashMaterial;

    protected override string DroneVisualName => "K2";
    protected override bool UsesCustomChargePresentation => true;

    protected override bool HasDroneActionInProgress =>
        interceptionCoroutine != null || capturedTarget != null;

    private void OnDestroy()
    {
        if (runtimeTeleportFlashSprite != null)
            Destroy(runtimeTeleportFlashSprite);

        if (runtimeTeleportFlashMaterial != null)
            Destroy(runtimeTeleportFlashMaterial);
    }

    protected override float GetBaseCooldownSec(ModuleRuntimeStats stats)
    {
        return stats != null
            ? Mathf.Max(0f, stats.K2CooldownSec)
            : 0f;
    }

    protected override void OnDroneVisualsCreated()
    {
        CreateChargePresentation();
    }

    protected override void OnDroneChargePresentationUpdated(
        float normalizedProgress,
        bool isCharged)
    {
        if (k2CooldownImage != null)
        {
            k2CooldownImage.enabled = true;
            k2CooldownImage.fillAmount = Mathf.Clamp01(normalizedProgress);
        }

        if (k2UnchargedImage != null)
            k2UnchargedImage.enabled = !isCharged;

        if (k2ChargedImage != null)
            k2ChargedImage.enabled = isCharged;
    }

    protected override void OnDroneEnabled()
    {
        if (interceptionZone != null)
            interceptionZone.OnCandidateEntered += HandleCandidateEntered;
    }

    protected override void OnDroneDisabled()
    {
        if (interceptionZone != null)
            interceptionZone.OnCandidateEntered -= HandleCandidateEntered;
    }

    protected override void UpdateDroneMotion()
    {
        if (HasDroneActionInProgress)
            return;

        Vector3 localPosition = transform.localPosition;
        float basePatrolRange = paddle != null
            ? Mathf.Max(0f, paddle.XRange)
            : Mathf.Max(0f, fallbackPatrolRange);
        float patrolRange = basePatrolRange +
            Mathf.Max(0f, patrolExtraRange);
        float targetX = patrolDirection > 0f
            ? patrolRange
            : -patrolRange;

        localPosition.x = Mathf.MoveTowards(
            localPosition.x,
            targetX,
            Mathf.Max(0f, patrolSpeed) * Time.deltaTime
        );
        localPosition.y = Mathf.MoveTowards(
            localPosition.y,
            patrolY,
            Mathf.Max(0f, patrolVerticalReturnSpeed) * Time.deltaTime
        );

        transform.localPosition = localPosition;

        if (Mathf.Abs(localPosition.x - targetX) <= 0.001f)
            patrolDirection *= -1f;
    }

    // K2 est strictement evenementiel : une charge pleine reste stockee tant
    // qu'aucune bille admissible ne traverse la zone d'interception.
    protected override void TryBeginChargedAction() { }

    protected override void OnDroneRuntimeStopped()
    {
        if (interceptionCoroutine != null)
        {
            StopCoroutine(interceptionCoroutine);
            interceptionCoroutine = null;
        }

        ReleaseCapturedTargetWithoutLaunch();
    }

    private void HandleCandidateEntered(BallState candidate)
    {
        if (!IsDroneGameplayArmed ||
            !IsDroneCharged ||
            HasDroneActionInProgress ||
            !IsEligible(candidate))
        {
            return;
        }

        // Reservation atomique : la bille coupe ici collider et simulation.
        // Void, bacs, K1 et autres drones ne peuvent plus la prendre.
        if (!candidate.TryReserveForDrone(this))
            return;

        if (!candidate.TryMarkCapturedByDrone(this))
        {
            candidate.TryCancelDroneExclusion(this);
            return;
        }

        if (!TryConsumeDroneCharge())
        {
            candidate.TryCancelDroneExclusion(this);
            return;
        }

        capturedTarget = candidate;
        capturedTargetVisualHidden = false;
        interceptionCoroutine = StartCoroutine(
            InterceptionRoutine(candidate)
        );
    }

    private bool IsEligible(BallState candidate)
    {
        if (candidate == null ||
            !candidate.gameObject.activeInHierarchy ||
            candidate.collected ||
            candidate.inBin ||
            candidate.isTutorialBall ||
            candidate.IsTemporarilyExcludedFromGameplay)
        {
            return false;
        }

        int tier = ModuleRuntimeStats.Instance != null
            ? Mathf.Max(0, ModuleRuntimeStats.Instance.K2Tier)
            : 0;

        if (string.Equals(
                candidate.BallId,
                WhiteBallId,
                StringComparison.OrdinalIgnoreCase))
        {
            return tier >= 1;
        }

        if (string.Equals(
                candidate.BallId,
                BlueBallId,
                StringComparison.OrdinalIgnoreCase))
        {
            return tier >= 2;
        }

        if (string.Equals(
                candidate.BallId,
                RedBallId,
                StringComparison.OrdinalIgnoreCase))
        {
            return tier >= 3;
        }

        return false;
    }

    private IEnumerator InterceptionRoutine(BallState target)
    {
        float ballZ = target.transform.position.z;

        while (IsCapturedTargetValid(target))
        {
            Vector3 wantedDronePosition = target.transform.position +
                Vector3.up * droneOffsetAboveBall;
            wantedDronePosition.z = transform.position.z;

            transform.position = Vector3.MoveTowards(
                transform.position,
                wantedDronePosition,
                Mathf.Max(0.01f, interceptionSpeed) * Time.deltaTime
            );

            if ((transform.position - wantedDronePosition).sqrMagnitude <=
                0.0009f)
            {
                break;
            }

            yield return null;
        }

        if (!IsCapturedTargetValid(target))
        {
            FinishInterruptedCapture();
            yield break;
        }

        Vector3 holdPosition = transform.position;
        holdPosition.z = ballZ;

        yield return MoveCapturedBall(
            target,
            target.transform.position,
            holdPosition,
            captureSnapDuration
        );

        if (!IsCapturedTargetValid(target))
        {
            FinishInterruptedCapture();
            yield break;
        }

        float holdRemaining = Mathf.Max(0f, holdDuration);
        while (holdRemaining > 0f && IsCapturedTargetValid(target))
        {
            holdRemaining -= Time.deltaTime;
            target.transform.position = holdPosition;
            yield return null;
        }

        if (!IsCapturedTargetValid(target))
        {
            FinishInterruptedCapture();
            yield break;
        }

        PlayTeleportFlash(transform.position);

        float flashLeadRemaining = Mathf.Max(0f, teleportFlashLeadDuration);
        while (flashLeadRemaining > 0f && IsCapturedTargetValid(target))
        {
            flashLeadRemaining -= Time.deltaTime;
            target.transform.position = holdPosition;
            yield return null;
        }

        if (!IsCapturedTargetValid(target))
        {
            FinishInterruptedCapture();
            yield break;
        }

        target.SetDroneTeleportVisualVisible(false);
        capturedTargetVisualHidden = true;

        if (ballSpawner == null)
        {
            FinishInterruptedCapture();
            yield break;
        }

        Vector3 reentryPosition = default;
        while (IsCapturedTargetValid(target) &&
               !ballSpawner.TryGetDroneReentryPosition(
                   target,
                   out reentryPosition))
        {
            target.transform.position = holdPosition;
            yield return null;
        }

        if (!IsCapturedTargetValid(target))
        {
            FinishInterruptedCapture();
            yield break;
        }

        target.transform.position = reentryPosition;
        target.ClearTrailHistory();

        // Le flash d'arrivee s'ouvre avant la materialisation et reste visible
        // un court instant apres, pour donner du poids a la teleportation.
        PlayTeleportFlash(reentryPosition, reentryFlashDuration);

        float revealDelayRemaining = Mathf.Max(0f, reentryRevealDelay);
        while (revealDelayRemaining > 0f && IsCapturedTargetValid(target))
        {
            revealDelayRemaining -= Time.deltaTime;
            target.transform.position = reentryPosition;
            yield return null;
        }

        if (!IsCapturedTargetValid(target))
        {
            FinishInterruptedCapture();
            yield break;
        }

        target.SetDroneTeleportVisualVisible(true);
        capturedTargetVisualHidden = false;
        target.TryReleaseFromDrone(this, Vector3.zero);
        capturedTarget = null;
        interceptionCoroutine = null;
    }

    private IEnumerator MoveCapturedBall(
        BallState target,
        Vector3 from,
        Vector3 to,
        float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            if (IsCapturedTargetValid(target))
                target.transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < safeDuration && IsCapturedTargetValid(target))
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            target.transform.position = Vector3.Lerp(from, to, progress);
            yield return null;
        }
    }

    private bool IsCapturedTargetValid(BallState target)
    {
        return target != null &&
               target == capturedTarget &&
               target.gameObject.activeInHierarchy &&
               target.DroneExclusionState ==
                   DroneBallExclusionState.Captured &&
               target.IsOwnedByDrone(this);
    }

    private void FinishInterruptedCapture()
    {
        ReleaseCapturedTargetWithoutLaunch();
        interceptionCoroutine = null;
    }

    private void ReleaseCapturedTargetWithoutLaunch()
    {
        BallState target = capturedTarget;
        capturedTarget = null;

        if (target == null || !target.gameObject.activeInHierarchy)
            return;

        if (capturedTargetVisualHidden)
        {
            target.SetDroneTeleportVisualVisible(true);
            capturedTargetVisualHidden = false;
        }

        target.TryCancelDroneExclusion(this);
    }

    private void PlayTeleportFlash(
        Vector3 position,
        float durationOverride = -1f)
    {
        if (runtimeTeleportFlashSprite == null ||
            runtimeTeleportFlashMaterial == null)
        {
            return;
        }

        GameObject flashObject = new GameObject("K2 Teleport Flash");
        flashObject.layer = gameObject.layer;
        flashObject.transform.position = position;

        SpriteRenderer flashRenderer =
            flashObject.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = runtimeTeleportFlashSprite;
        flashRenderer.sharedMaterial = runtimeTeleportFlashMaterial;
        flashRenderer.sortingLayerName = "CleanGameplay";
        flashRenderer.sortingOrder = 125;

        DroneTeleportFlashEffect effect =
            flashObject.AddComponent<DroneTeleportFlashEffect>();
        effect.Initialize(
            flashRenderer,
            teleportFlashColor,
            durationOverride >= 0f
                ? durationOverride
                : teleportFlashDuration,
            teleportFlashScale
        );
    }

    private void CreateChargePresentation()
    {
        Transform visualRoot = DroneVisualRoot;
        if (visualRoot == null)
            return;

        int gameplayLayer = LayerMask.NameToLayer("Gameplay");
        GameObject canvasObject = new GameObject(
            "K2 Charge Canvas",
            typeof(RectTransform),
            typeof(Canvas)
        );
        canvasObject.transform.SetParent(visualRoot, false);

        if (gameplayLayer >= 0)
            canvasObject.layer = gameplayLayer;

        Sprite referenceSprite = k2UnchargedSprite != null
            ? k2UnchargedSprite
            : k2ChargedSprite != null
                ? k2ChargedSprite
                : k2CooldownSprite;

        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = referenceSprite != null
            ? (Vector2)referenceSprite.bounds.size
            : Vector2.one;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31;

        // Le remplissage cyan reste derriere le drone rouge pendant toute
        // la recharge. Le sprite charge ne prend sa place qu'a cent pour cent.
        k2CooldownImage = CreateChargeImage(
            canvasObject.transform,
            "K2Cooldown",
            k2CooldownSprite,
            gameplayLayer
        );
        k2CooldownImage.type = Image.Type.Filled;
        k2CooldownImage.fillMethod = Image.FillMethod.Radial360;
        k2CooldownImage.fillOrigin = (int)Image.Origin360.Top;
        k2CooldownImage.fillClockwise = true;
        k2CooldownImage.fillAmount = 0f;

        k2UnchargedImage = CreateChargeImage(
            canvasObject.transform,
            "K2Uncharged",
            k2UnchargedSprite,
            gameplayLayer
        );
        k2UnchargedImage.enabled = true;

        k2ChargedImage = CreateChargeImage(
            canvasObject.transform,
            "K2Charged",
            k2ChargedSprite,
            gameplayLayer
        );
        k2ChargedImage.enabled = false;

        CreateTeleportFlashResources();
    }

    private void CreateTeleportFlashResources()
    {
        if (teleportFlashShader == null)
            return;

        Texture2D texture = Texture2D.whiteTexture;
        runtimeTeleportFlashSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width
        );
        runtimeTeleportFlashSprite.name = "K2 Teleport Flash Runtime";

        runtimeTeleportFlashMaterial = new Material(teleportFlashShader)
        {
            name = "K2 Teleport Flash Runtime"
        };
    }

    private static Image CreateChargeImage(
        Transform parent,
        string objectName,
        Sprite sprite,
        int gameplayLayer)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.transform.SetParent(parent, false);

        if (gameplayLayer >= 0)
            imageObject.layer = gameplayLayer;

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }
}
