using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class K1AntiBlackDroneController : DroneRuntimeControllerBase
{
    private const string BlackBallId = "black";

    [Header("Dépendances K1")]
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;

    [Header("Presentation de charge K1")]
    [SerializeField] private Sprite k1UnchargedSprite;
    [SerializeField] private Sprite k1ChargedSprite;
    [SerializeField] private Sprite k1CooldownSprite;

    [Header("Patrouille verticale a gauche du mur")]
    [SerializeField] private Transform leftWall;
    [SerializeField] private float horizontalWallClearance = 0.25f;
    [SerializeField] private float fallbackPatrolX = -2.75f;
    [SerializeField] private float verticalPatrolMinY = -3f;
    [SerializeField] private float verticalPatrolMaxY = 3f;
    [SerializeField] private float verticalPatrolCycleSec = 6f;

    [Header("Verrouillage de cible")]
    [SerializeField] private float lockOnDelaySec = 0.15f;
    [SerializeField] private float maximumTargetY = 4f;

    [Header("Laser temporaire")]
    [SerializeField] private float laserSpeed = 18f;
    [SerializeField] private float laserWidth = 0.055f;
    [SerializeField] private float maximumLaserTravelSec = 0.75f;
    [SerializeField] private float retryDelaySec = 0.15f;

    [Header("Decharge visuelle avant tir")]
    [SerializeField] private Shader dischargeFlashShader;
    [SerializeField] private float dischargeFlashDuration = 0.12f;
    [SerializeField] private float dischargeFlashScale = 1.14f;
    [SerializeField, Range(0f, 1f)] private float dischargeFlashMinAlpha = 0.3f;

    [Header("Recul au tir")]
    [SerializeField] private float recoilDistance = 0.32f;
    [SerializeField] private float recoilDuration = 0.1f;

    private LineRenderer laserRenderer;
    private Image k1CooldownImage;
    private Image k1UnchargedImage;
    private Image k1ChargedImage;
    private Material dischargeFlashMaterial;
    private Coroutine lockOnCoroutine;
    private Coroutine shotCoroutine;
    private Coroutine recoilCoroutine;
    private BallState committedTarget;
    private Material runtimeLaserMaterial;

    private float verticalPatrolPhase;
    private float retryTimer;
    private bool dischargeFlashActive;
    private Vector3 recoilOffset;

    protected override string DroneVisualName => "K1";
    protected override bool UsesCustomChargePresentation => true;

    protected override bool HasDroneActionInProgress =>
        lockOnCoroutine != null || shotCoroutine != null;

    private void OnDestroy()
    {
        if (runtimeLaserMaterial != null)
            Destroy(runtimeLaserMaterial);

        if (dischargeFlashMaterial != null)
            Destroy(dischargeFlashMaterial);
    }

    protected override float GetBaseCooldownSec(ModuleRuntimeStats stats)
    {
        return stats != null ? Mathf.Max(0f, stats.K1CooldownSec) : 0f;
    }

    protected override void OnDroneGameplayTick()
    {
        if (retryTimer > 0f && !HasDroneActionInProgress)
            retryTimer = Mathf.Max(0f, retryTimer - Time.deltaTime);
    }

    protected override void OnDroneGameplayStarted()
    {
        retryTimer = 0f;
    }

    protected override void OnDroneRuntimeStopped()
    {
        AbortShot();
        StopRecoilAndReset();
        retryTimer = 0f;
    }

    protected override void UpdateDroneMotion()
    {
        float cycleDuration = Mathf.Max(0.01f, verticalPatrolCycleSec);
        verticalPatrolPhase = Mathf.Repeat(
            verticalPatrolPhase + Time.deltaTime / cycleDuration,
            1f
        );

        float minimumY = Mathf.Min(
            verticalPatrolMinY,
            verticalPatrolMaxY
        );
        float maximumY = Mathf.Max(
            verticalPatrolMinY,
            verticalPatrolMaxY
        );
        float normalizedY =
            0.5f + 0.5f * Mathf.Sin(verticalPatrolPhase * Mathf.PI * 2f);

        Vector3 patrolPosition = new Vector3(
            ResolvePatrolLocalX(),
            Mathf.Lerp(minimumY, maximumY, normalizedY),
            0f
        );
        transform.localPosition = patrolPosition + recoilOffset;
    }

    private float ResolvePatrolLocalX()
    {
        if (leftWall == null)
            return fallbackPatrolX;

        Collider wallCollider = leftWall.GetComponent<Collider>();
        float wallOutsideWorldX = wallCollider != null
            ? wallCollider.bounds.min.x
            : leftWall.position.x;
        float patrolWorldX = wallOutsideWorldX -
            Mathf.Max(0f, horizontalWallClearance);

        if (transform.parent == null)
            return patrolWorldX;

        Vector3 worldPoint = transform.position;
        worldPoint.x = patrolWorldX;
        return transform.parent.InverseTransformPoint(worldPoint).x;
    }

    protected override void TryBeginChargedAction()
    {
        if (retryTimer > 0f)
            return;

        if (ballSpawner == null)
            return;

        if (!ballSpawner.TryGetNearestActiveDangerBall(
                BlackBallId,
                transform.position,
                maximumTargetY,
                out BallState target))
        {
            return;
        }

        // La sélection par danger visuel écarte déjà normalement une noire
        // blanchie par A. Cette validation explicite garantit aussi la règle
        // métier en cas de désynchronisation momentanée de l'aperçu visuel.
        if (!IsTargetStillValid(target))
        {
            retryTimer = retryDelaySec;
            return;
        }

        lockOnCoroutine = StartCoroutine(
            LockOnAndFireRoutine(target)
        );
    }

    private IEnumerator LockOnAndFireRoutine(BallState target)
    {
        float delay = Mathf.Max(0f, lockOnDelaySec);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsTargetStillValid(target) ||
            !TryCalculateIntercept(
                target,
                out float travelSec))
        {
            FinishSilentLockOn();
            yield break;
        }

        // Point de non-retour : si le snapshot a deja verrouille la bille,
        // cette prise de possession echoue et aucun laser ne devient visible.
        // Sinon K1 retire immediatement la bille du bac logique et le notifie.
        if (!TryCommitTarget(target))
        {
            FinishSilentLockOn();
            yield break;
        }

        lockOnCoroutine = null;
        BeginDischargeFlash();
        shotCoroutine = StartCoroutine(
            FireLaserRoutine(
                target,
                travelSec)
        );
    }

    private bool TryCalculateIntercept(
        BallState target,
        out float travelSec)
    {
        travelSec = 0f;

        if (!IsTargetStillValid(target) || laserSpeed <= 0f)
            return false;

        Vector3 origin = transform.position;
        Vector3 relativePosition = target.transform.position - origin;
        Vector3 velocity = target.LinearVelocity;

        float a = Vector3.Dot(velocity, velocity) - laserSpeed * laserSpeed;
        float b = 2f * Vector3.Dot(relativePosition, velocity);
        float c = Vector3.Dot(relativePosition, relativePosition);

        if (!TrySolvePositiveTime(a, b, c, out travelSec))
            return false;

        if (travelSec > maximumLaserTravelSec)
            return false;

        return true;
    }

    private bool TrySolvePositiveTime(
        float a,
        float b,
        float c,
        out float time)
    {
        time = 0f;

        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f)
                return false;

            float linearTime = -c / b;
            if (linearTime <= 0f)
                return false;

            time = linearTime;
            return true;
        }

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return false;

        float sqrt = Mathf.Sqrt(discriminant);
        float first = (-b - sqrt) / (2f * a);
        float second = (-b + sqrt) / (2f * a);

        float best = float.PositiveInfinity;
        if (first > 0f)
            best = first;
        if (second > 0f)
            best = Mathf.Min(best, second);

        if (float.IsPositiveInfinity(best))
            return false;

        time = best;
        return true;
    }

    private IEnumerator FireLaserRoutine(
        BallState target,
        float travelSec)
    {
        yield return AnimateDischargeFlash();

        if (!IsCommittedTargetActive(target))
        {
            CompleteCommittedNeutralization();
            yield break;
        }

        Vector3 origin = transform.position;
        float elapsed = 0f;

        StartRecoil(target.transform.position - origin);
        laserRenderer.enabled = true;

        while (elapsed < travelSec)
        {
            if (!IsCommittedTargetActive(target))
            {
                CompleteCommittedNeutralization();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / travelSec);
            Vector3 currentOrigin = transform.position;
            Vector3 destination = target.transform.position;
            destination.z = currentOrigin.z;
            Vector3 head = Vector3.Lerp(
                currentOrigin,
                destination,
                progress
            );

            DrawLaser(currentOrigin, head);
            yield return null;
        }

        CompleteCommittedNeutralization();
    }

    private bool TryCommitTarget(BallState target)
    {
        if (!IsTargetStillValid(target))
            return false;

        // "collected" sert ici de verrou terminal partage : snapshot, void,
        // famille A et triggers de bac ne peuvent plus prendre cette bille.
        target.collected = true;

        if (!TryDetachFromBin(target))
        {
            target.collected = false;
            return false;
        }

        committedTarget = target;

        if (!TryConsumeDroneCharge())
        {
            committedTarget = null;
            target.collected = false;
            return false;
        }

        retryTimer = 0f;
        return true;
    }

    private bool IsCommittedTargetActive(BallState target)
    {
        return target != null &&
               target == committedTarget &&
               target.gameObject.activeInHierarchy;
    }

    private void CompleteCommittedNeutralization()
    {
        BallState target = committedTarget;
        committedTarget = null;
        shotCoroutine = null;
        EndDischargeFlash();

        if (laserRenderer != null)
            laserRenderer.enabled = false;

        if (target == null || !target.gameObject.activeInHierarchy)
            return;

        Vector3 particlePosition = target.transform.position;
        bool recycled = ballSpawner != null &&
            ballSpawner.Recycle(
                target.gameObject,
                BallRecycleReason.Neutralized
            );

        if (!recycled)
            return;

        target.PlayDestructionEffect(particlePosition);
    }

    private bool IsTargetStillValid(BallState target)
    {
        return target != null &&
               target.gameObject.activeInHierarchy &&
               !target.collected &&
               !target.IsTemporarilyExcludedFromGameplay &&
               target.transform.position.y <= maximumTargetY &&
               target.IsVisualDanger &&
               !IsReservedByFamilyA(target) &&
               string.Equals(
                   target.BallId,
                   BlackBallId,
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private bool IsReservedByFamilyA(BallState target)
    {
        return target != null &&
               BlackFilterRuntimeController.Instance != null &&
               BlackFilterRuntimeController.Instance.IsReserved(target);
    }

    private bool TryDetachFromBin(BallState target)
    {
        if (target == null || !target.inBin)
            return true;

        BinTrigger bin = target.currentSide == Side.Left
            ? leftBin
            : target.currentSide == Side.Right
                ? rightBin
                : null;

        return bin != null && bin.TryRemoveForNeutralization(target);
    }

    private void FinishSilentLockOn()
    {
        retryTimer = retryDelaySec;
        lockOnCoroutine = null;
    }

    private void AbortShot()
    {
        if (lockOnCoroutine != null)
        {
            StopCoroutine(lockOnCoroutine);
            lockOnCoroutine = null;
        }

        if (shotCoroutine != null)
        {
            StopCoroutine(shotCoroutine);
            shotCoroutine = null;
        }

        EndDischargeFlash();

        // Un laser deja visible est une promesse de destruction. Une fermeture
        // du gameplay ou une desactivation du module la resout immediatement.
        if (committedTarget != null)
        {
            CompleteCommittedNeutralization();
            return;
        }

        if (laserRenderer != null)
            laserRenderer.enabled = false;
    }

    protected override void OnDroneVisualsCreated()
    {
        CreateChargePresentation();

        int gameplayLayer = LayerMask.NameToLayer("Gameplay");
        GameObject laserObject = new GameObject("K1 Laser");
        laserObject.transform.SetParent(transform, true);

        if (gameplayLayer >= 0)
            laserObject.layer = gameplayLayer;

        laserRenderer = laserObject.AddComponent<LineRenderer>();
        laserRenderer.useWorldSpace = true;
        laserRenderer.positionCount = 2;
        laserRenderer.alignment = LineAlignment.View;
        laserRenderer.textureMode = LineTextureMode.Stretch;
        laserRenderer.numCapVertices = 0;
        laserRenderer.numCornerVertices = 0;
        laserRenderer.startColor = Color.red;
        laserRenderer.endColor = Color.red;
        laserRenderer.sortingOrder = 29;

        Shader laserShader = Shader.Find("Sprites/Default");
        if (laserShader != null)
        {
            runtimeLaserMaterial = new Material(laserShader)
            {
                name = "K1 Laser Runtime"
            };
            laserRenderer.material = runtimeLaserMaterial;
        }

        laserRenderer.enabled = false;
    }

    protected override void OnDroneChargePresentationUpdated(
        float normalizedProgress,
        bool isCharged)
    {
        if (dischargeFlashActive)
        {
            ShowChargedPresentationForDischarge();
            return;
        }

        if (k1CooldownImage != null)
        {
            k1CooldownImage.enabled = true;
            k1CooldownImage.fillAmount =
                Mathf.Clamp01(normalizedProgress);
        }

        if (k1UnchargedImage != null)
            k1UnchargedImage.enabled = !isCharged;

        if (k1ChargedImage != null)
            k1ChargedImage.enabled = isCharged;
    }

    private void BeginDischargeFlash()
    {
        dischargeFlashActive = true;

        if (k1ChargedImage != null && dischargeFlashMaterial != null)
            k1ChargedImage.material = dischargeFlashMaterial;

        ShowChargedPresentationForDischarge();
    }

    private IEnumerator AnimateDischargeFlash()
    {
        float duration = Mathf.Max(0f, dischargeFlashDuration);
        if (duration <= 0f)
        {
            EndDischargeFlash();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && dischargeFlashActive)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);

            if (k1ChargedImage != null)
            {
                k1ChargedImage.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(
                        1f,
                        Mathf.Max(1f, dischargeFlashScale),
                        pulse
                    );

                Color color = Color.white;
                color.a = Mathf.Lerp(
                    1f,
                    Mathf.Clamp01(dischargeFlashMinAlpha),
                    pulse
                );
                k1ChargedImage.color = color;
            }

            yield return null;
        }

        EndDischargeFlash();
    }

    private void ShowChargedPresentationForDischarge()
    {
        if (k1CooldownImage != null)
        {
            k1CooldownImage.enabled = true;
            k1CooldownImage.fillAmount = 0f;
        }

        if (k1UnchargedImage != null)
            k1UnchargedImage.enabled = true;

        if (k1ChargedImage != null)
            k1ChargedImage.enabled = true;
    }

    private void EndDischargeFlash()
    {
        dischargeFlashActive = false;

        if (k1ChargedImage != null)
        {
            k1ChargedImage.rectTransform.localScale = Vector3.one;
            k1ChargedImage.color = Color.white;
            k1ChargedImage.material = null;
        }

        // La charge a deja ete consommee au point de non-retour du tir.
        OnDroneChargePresentationUpdated(0f, false);
    }

    private void StartRecoil(Vector3 shotDirectionWorld)
    {
        shotDirectionWorld.z = 0f;
        if (shotDirectionWorld.sqrMagnitude <= 0.0001f)
            return;

        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
            recoilCoroutine = null;
        }

        Vector3 oppositeWorldDirection =
            -shotDirectionWorld.normalized;
        Vector3 oppositeLocalDirection = transform.parent != null
            ? transform.parent.InverseTransformDirection(
                oppositeWorldDirection
            )
            : oppositeWorldDirection;
        oppositeLocalDirection.z = 0f;

        if (oppositeLocalDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 targetOffset = oppositeLocalDirection.normalized *
            Mathf.Max(0f, recoilDistance);

        float duration = Mathf.Max(0f, recoilDuration);
        if (duration <= 0f)
        {
            recoilOffset = Vector3.zero;
            return;
        }

        recoilCoroutine = StartCoroutine(
            AnimateRecoil(targetOffset, duration)
        );
    }

    private IEnumerator AnimateRecoil(
        Vector3 targetOffset,
        float duration)
    {
        Vector3 startOffset = recoilOffset;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            recoilOffset = Vector3.LerpUnclamped(
                startOffset,
                targetOffset,
                easedProgress
            );
            yield return null;
        }

        recoilOffset = targetOffset;

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            recoilOffset = Vector3.LerpUnclamped(
                targetOffset,
                Vector3.zero,
                easedProgress
            );
            yield return null;
        }

        recoilOffset = Vector3.zero;
        recoilCoroutine = null;
    }

    private void StopRecoilAndReset()
    {
        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
            recoilCoroutine = null;
        }

        recoilOffset = Vector3.zero;
    }

    private void CreateChargePresentation()
    {
        Transform visualRoot = DroneVisualRoot;
        if (visualRoot == null)
            return;

        int gameplayLayer = LayerMask.NameToLayer("Gameplay");
        GameObject canvasObject = new GameObject(
            "K1 Charge Canvas",
            typeof(RectTransform),
            typeof(Canvas)
        );
        canvasObject.transform.SetParent(visualRoot, false);

        if (gameplayLayer >= 0)
            canvasObject.layer = gameplayLayer;

        Sprite referenceSprite = k1UnchargedSprite != null
            ? k1UnchargedSprite
            : k1ChargedSprite != null
                ? k1ChargedSprite
                : k1CooldownSprite;

        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = referenceSprite != null
            ? (Vector2)referenceSprite.bounds.size
            : Vector2.one;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31;

        // L'ordre des enfants est volontaire : le remplissage cyan reste
        // derriere le drone rouge, puis le sprite charge prend sa place.
        k1CooldownImage = CreateChargeImage(
            canvasObject.transform,
            "K1Cooldown",
            k1CooldownSprite,
            gameplayLayer
        );
        k1CooldownImage.type = Image.Type.Filled;
        k1CooldownImage.fillMethod = Image.FillMethod.Radial360;
        k1CooldownImage.fillOrigin = (int)Image.Origin360.Top;
        k1CooldownImage.fillClockwise = true;
        k1CooldownImage.fillAmount = 0f;

        k1UnchargedImage = CreateChargeImage(
            canvasObject.transform,
            "K1Uncharged",
            k1UnchargedSprite,
            gameplayLayer
        );
        k1UnchargedImage.enabled = true;

        k1ChargedImage = CreateChargeImage(
            canvasObject.transform,
            "K1Charged",
            k1ChargedSprite,
            gameplayLayer
        );
        k1ChargedImage.enabled = false;

        if (dischargeFlashShader != null)
        {
            dischargeFlashMaterial = new Material(dischargeFlashShader)
            {
                name = "K1 White Flash Runtime"
            };
        }
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

    private void DrawLaser(Vector3 start, Vector3 end)
    {
        if (laserRenderer == null)
            return;

        float width = Mathf.Max(0.001f, laserWidth);
        laserRenderer.startWidth = width;
        laserRenderer.endWidth = width;
        laserRenderer.SetPosition(0, start);
        laserRenderer.SetPosition(1, end);
    }

}
