using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class K1AntiBlackDroneController : MonoBehaviour
{
    private const string BlackBallId = "black";

    [Header("Dépendances")]
    [SerializeField] private LevelRunStateController runStateController;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private BinTrigger leftBin;
    [SerializeField] private BinTrigger rightBin;

    [Header("Sprites temporaires")]
    [SerializeField] private Sprite droneSprite;
    [SerializeField] private Sprite cooldownSprite;

    [Header("Trajectoire elliptique")]
    [SerializeField] private Vector3 ellipseCenter = Vector3.zero;
    [SerializeField] private Vector2 ellipseRadii = new Vector2(2.45f, 3f);
    [SerializeField] private float ellipseSpeed = 0.42f;
    [SerializeField] private Vector2 ellipseRadiusVariation = new Vector2(0.45f, 0.35f);
    [SerializeField] private Vector2 ellipseCenterDrift = new Vector2(0.35f, 0.2f);
    [SerializeField] private float ellipseMorphSpeed = 0.18f;
    [SerializeField, Range(0f, 0.5f)] private float ellipseSpeedVariation = 0.18f;
    [SerializeField] private float patrolMinY = -3f;
    [SerializeField] private float droneScale = 0.105f;

    [Header("Cooldown visuel")]
    [SerializeField] private float cooldownVisualScale = 1.6f;

    [Header("Verrouillage de cible")]
    [SerializeField] private float lockOnDelaySec = 0.15f;

    [Header("Laser temporaire")]
    [SerializeField] private float laserSpeed = 18f;
    [SerializeField] private float laserWidth = 0.055f;
    [SerializeField] private float maximumLaserTravelSec = 0.75f;
    [SerializeField] private float retryDelaySec = 0.15f;

    private GameObject visualRoot;
    private Image cooldownImage;
    private SpriteRenderer laserRenderer;
    private Coroutine lockOnCoroutine;
    private Coroutine shotCoroutine;
    private BallState committedTarget;
    private Sprite runtimeLaserSprite;

    private float ellipseAngle;
    private float patrolTime;
    private float cooldownDuration;
    private float cooldownRemaining;
    private float retryTimer;

    private bool moduleEquipped;
    private bool gameplayWasArmed;
    private bool charged;

    private void Awake()
    {
        CreateVisuals();
        RefreshModule();
    }

    private void OnEnable()
    {
        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.AddListener(RefreshModule);
    }

    private void OnDisable()
    {
        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.RemoveListener(RefreshModule);

        AbortShot();
    }

    private void OnDestroy()
    {
        if (runtimeLaserSprite != null)
            Destroy(runtimeLaserSprite);
    }

    private void Update()
    {
        if (!moduleEquipped)
            return;

        UpdateEllipseMotion();

        bool gameplayArmed =
            runStateController != null && runStateController.GameplayArmed;

        if (gameplayArmed && !gameplayWasArmed)
            StartMissionCooldown();
        else if (!gameplayArmed && gameplayWasArmed)
            StopMissionRuntime();

        gameplayWasArmed = gameplayArmed;

        if (!gameplayArmed ||
            lockOnCoroutine != null ||
            shotCoroutine != null)
        {
            UpdateCooldownVisual();
            return;
        }

        if (!charged)
        {
            cooldownRemaining = Mathf.Max(
                0f,
                cooldownRemaining - Time.deltaTime
            );

            if (cooldownRemaining <= 0f)
                charged = true;
        }

        if (retryTimer > 0f)
            retryTimer = Mathf.Max(0f, retryTimer - Time.deltaTime);

        if (charged && retryTimer <= 0f)
            TryFireAtNearestBlackBall();

        UpdateCooldownVisual();
    }

    private void RefreshModule()
    {
        bool wasEquipped = moduleEquipped;

        cooldownDuration = ModuleRuntimeStats.Instance != null
            ? Mathf.Max(0f, ModuleRuntimeStats.Instance.K1CooldownSec)
            : 0f;
        moduleEquipped = cooldownDuration > 0f;

        if (visualRoot != null)
            visualRoot.SetActive(moduleEquipped);

        if (!moduleEquipped)
        {
            gameplayWasArmed = false;
            StopMissionRuntime();
            return;
        }

        if (!wasEquipped)
        {
            charged = false;
            cooldownRemaining = cooldownDuration;
        }
    }

    private void StartMissionCooldown()
    {
        AbortShot();
        charged = false;
        cooldownRemaining = cooldownDuration;
        retryTimer = 0f;
    }

    private void StopMissionRuntime()
    {
        AbortShot();
        charged = false;
        cooldownRemaining = cooldownDuration;
        retryTimer = 0f;
    }

    private void UpdateEllipseMotion()
    {
        patrolTime += Time.deltaTime;

        float morphTime = patrolTime * Mathf.Max(0.01f, ellipseMorphSpeed);
        float speedMultiplier = 1f +
            Mathf.Sin(morphTime * 2.3f + 0.4f) * ellipseSpeedVariation;
        ellipseAngle += ellipseSpeed * speedMultiplier * Time.deltaTime;

        float radiusX = Mathf.Max(
            0.1f,
            ellipseRadii.x + ellipseRadiusVariation.x *
            (0.7f * Mathf.Sin(morphTime) +
             0.3f * Mathf.Sin(morphTime * 0.47f + 1.8f))
        );
        float radiusY = Mathf.Max(
            0.1f,
            ellipseRadii.y + ellipseRadiusVariation.y *
            (0.65f * Mathf.Sin(morphTime * 0.73f + 1.1f) +
             0.35f * Mathf.Sin(morphTime * 0.31f + 2.6f))
        );

        float centerX = ellipseCenter.x + ellipseCenterDrift.x *
            Mathf.Sin(morphTime * 0.41f + 0.7f);
        float wantedCenterY = ellipseCenter.y + ellipseCenterDrift.y *
            Mathf.Sin(morphTime * 0.37f + 2.1f);
        float centerY = Mathf.Max(wantedCenterY, patrolMinY + radiusY);

        transform.localPosition = new Vector3(
            centerX + Mathf.Cos(ellipseAngle) * radiusX,
            centerY + Mathf.Sin(ellipseAngle) * radiusY,
            ellipseCenter.z
        );
    }

    private void TryFireAtNearestBlackBall()
    {
        if (ballSpawner == null)
            return;

        if (!ballSpawner.TryGetNearestActiveDangerBall(
                BlackBallId,
                transform.position,
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
        Vector3 origin = transform.position;
        float elapsed = 0f;

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
            Vector3 destination = target.transform.position;
            destination.z = origin.z;
            Vector3 head = Vector3.Lerp(origin, destination, progress);

            DrawLaser(origin, head);
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
        charged = false;
        cooldownRemaining = cooldownDuration;
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

        PlayImpactParticles(particlePosition);
    }

    private bool IsTargetStillValid(BallState target)
    {
        return target != null &&
               target.gameObject.activeInHierarchy &&
               !target.collected &&
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

    private void CreateVisuals()
    {
        visualRoot = new GameObject("K1 Visual");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localScale = Vector3.one * droneScale;

        int gameplayLayer = LayerMask.NameToLayer("Gameplay");
        if (gameplayLayer >= 0)
            visualRoot.layer = gameplayLayer;

        SpriteRenderer droneRenderer =
            visualRoot.AddComponent<SpriteRenderer>();
        droneRenderer.sprite = droneSprite;
        droneRenderer.sortingOrder = 30;

        GameObject cooldownCanvasObject = new GameObject(
            "K1 Cooldown Canvas",
            typeof(RectTransform),
            typeof(Canvas)
        );
        cooldownCanvasObject.transform.SetParent(visualRoot.transform, false);

        if (gameplayLayer >= 0)
            cooldownCanvasObject.layer = gameplayLayer;

        RectTransform cooldownCanvasRect =
            cooldownCanvasObject.GetComponent<RectTransform>();
        Vector2 droneSize = droneSprite != null
            ? droneSprite.bounds.size
            : Vector2.one;
        cooldownCanvasRect.sizeDelta =
            droneSize * Mathf.Max(1f, cooldownVisualScale);

        Canvas cooldownCanvas = cooldownCanvasObject.GetComponent<Canvas>();
        cooldownCanvas.renderMode = RenderMode.WorldSpace;
        cooldownCanvas.overrideSorting = true;
        cooldownCanvas.sortingOrder = 31;

        GameObject cooldownBackgroundObject = new GameObject(
            "K1 Cooldown Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        cooldownBackgroundObject.transform.SetParent(
            cooldownCanvasObject.transform,
            false
        );

        if (gameplayLayer >= 0)
            cooldownBackgroundObject.layer = gameplayLayer;

        RectTransform cooldownBackgroundRect =
            cooldownBackgroundObject.GetComponent<RectTransform>();
        StretchToParent(cooldownBackgroundRect);

        Image cooldownBackgroundImage =
            cooldownBackgroundObject.GetComponent<Image>();
        cooldownBackgroundImage.sprite = cooldownSprite;
        cooldownBackgroundImage.type = Image.Type.Simple;
        cooldownBackgroundImage.preserveAspect = true;
        cooldownBackgroundImage.raycastTarget = false;
        cooldownBackgroundImage.color = new Color(1f, 1f, 1f, 0.18f);

        GameObject cooldownObject = new GameObject(
            "K1 Cooldown Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        cooldownObject.transform.SetParent(cooldownCanvasObject.transform, false);

        if (gameplayLayer >= 0)
            cooldownObject.layer = gameplayLayer;

        RectTransform cooldownRect =
            cooldownObject.GetComponent<RectTransform>();
        StretchToParent(cooldownRect);

        cooldownImage = cooldownObject.GetComponent<Image>();
        cooldownImage.sprite = cooldownSprite;
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Top;
        cooldownImage.fillClockwise = true;
        cooldownImage.fillAmount = 0f;
        cooldownImage.preserveAspect = true;
        cooldownImage.raycastTarget = false;

        GameObject laserObject = new GameObject("K1 Laser");
        laserObject.transform.SetParent(transform, true);

        if (gameplayLayer >= 0)
            laserObject.layer = gameplayLayer;

        laserRenderer = laserObject.AddComponent<SpriteRenderer>();
        runtimeLaserSprite = CreateLaserSprite();
        laserRenderer.sprite = runtimeLaserSprite;
        laserRenderer.color = Color.red;
        laserRenderer.sortingOrder = 29;
        laserRenderer.enabled = false;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private Sprite CreateLaserSprite()
    {
        Texture2D texture = Texture2D.whiteTexture;
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            1f
        );
    }

    private void DrawLaser(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;

        laserRenderer.transform.position = (start + end) * 0.5f;
        laserRenderer.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg
        );
        laserRenderer.transform.localScale =
            new Vector3(length, laserWidth, 1f);
    }

    private void UpdateCooldownVisual()
    {
        if (cooldownImage == null)
            return;

        if (charged)
        {
            cooldownImage.fillAmount = 1f;
            cooldownImage.color = new Color(1f, 1f, 1f, 0.9f);
            return;
        }

        float progress = cooldownDuration > 0f
            ? 1f - cooldownRemaining / cooldownDuration
            : 0f;

        cooldownImage.fillAmount = Mathf.Clamp01(progress);
        cooldownImage.color = new Color(1f, 1f, 1f, 0.9f);
    }

    private void PlayImpactParticles(Vector3 position)
    {
        GameObject particleObject = new GameObject("K1 Impact FX");
        particleObject.transform.position = position;

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 2.4f;
        main.startSize = 0.1f;
        main.startColor = new Color(1f, 0.12f, 0.08f, 1f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        particles.Emit(18);
        particles.Play();
    }
}
