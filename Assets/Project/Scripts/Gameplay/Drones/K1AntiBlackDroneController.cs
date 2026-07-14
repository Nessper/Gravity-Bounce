using System.Collections;
using UnityEngine;

public sealed class K1AntiBlackDroneController : MonoBehaviour
{
    private const string BlackBallId = "black";

    [Header("Dépendances")]
    [SerializeField] private LevelRunStateController runStateController;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Sprites temporaires")]
    [SerializeField] private Sprite droneSprite;
    [SerializeField] private Sprite cooldownSprite;

    [Header("Trajectoire elliptique")]
    [SerializeField] private Vector3 ellipseCenter = new Vector3(0f, 0f, -0.15f);
    [SerializeField] private Vector2 ellipseRadii = new Vector2(2.45f, 4.1f);
    [SerializeField] private float ellipseSpeed = 0.42f;
    [SerializeField] private float droneScale = 0.42f;

    [Header("Laser temporaire")]
    [SerializeField] private float laserSpeed = 18f;
    [SerializeField] private float laserWidth = 0.055f;
    [SerializeField] private float maximumLaserTravelSec = 0.75f;
    [SerializeField] private float impactRadius = 0.38f;
    [SerializeField] private float retryDelaySec = 0.15f;

    private GameObject visualRoot;
    private SpriteRenderer cooldownRenderer;
    private SpriteRenderer laserRenderer;
    private Coroutine shotCoroutine;
    private Sprite runtimeLaserSprite;

    private float ellipseAngle;
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

        if (!gameplayArmed || shotCoroutine != null)
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
        ellipseAngle += ellipseSpeed * Time.deltaTime;

        transform.position = ellipseCenter + new Vector3(
            Mathf.Cos(ellipseAngle) * ellipseRadii.x,
            Mathf.Sin(ellipseAngle) * ellipseRadii.y,
            0f
        );
    }

    private void TryFireAtNearestBlackBall()
    {
        if (ballSpawner == null)
            return;

        if (!ballSpawner.TryGetNearestActiveBall(
                BlackBallId,
                transform.position,
                out BallState target))
        {
            return;
        }

        if (!TryCalculateIntercept(target, out Vector3 impactPoint, out float travelSec))
        {
            retryTimer = retryDelaySec;
            return;
        }

        shotCoroutine = StartCoroutine(
            FireLaserRoutine(target, impactPoint, travelSec)
        );
    }

    private bool TryCalculateIntercept(
        BallState target,
        out Vector3 impactPoint,
        out float travelSec)
    {
        impactPoint = Vector3.zero;
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

        impactPoint = target.transform.position + velocity * travelSec;
        impactPoint.z = origin.z;

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
        Vector3 impactPoint,
        float travelSec)
    {
        Vector3 origin = transform.position;
        float elapsed = 0f;

        laserRenderer.enabled = true;

        while (elapsed < travelSec)
        {
            if (!IsTargetStillValid(target))
            {
                FinishMissedShot();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / travelSec);
            Vector3 head = Vector3.Lerp(origin, impactPoint, progress);

            DrawLaser(origin, head);
            yield return null;
        }

        bool impactIsValid =
            IsTargetStillValid(target) &&
            Vector3.Distance(target.transform.position, impactPoint) <= impactRadius;

        laserRenderer.enabled = false;

        if (!impactIsValid)
        {
            FinishMissedShot();
            yield break;
        }

        Vector3 particlePosition = target.transform.position;
        bool recycled = ballSpawner != null &&
            ballSpawner.Recycle(
                target.gameObject,
                BallRecycleReason.Neutralized
            );

        if (!recycled)
        {
            FinishMissedShot();
            yield break;
        }

        PlayImpactParticles(particlePosition);

        charged = false;
        cooldownRemaining = cooldownDuration;
        retryTimer = 0f;
        shotCoroutine = null;
    }

    private bool IsTargetStillValid(BallState target)
    {
        return target != null &&
               target.gameObject.activeInHierarchy &&
               !target.collected &&
               !target.inBin &&
               string.Equals(
                   target.BallId,
                   BlackBallId,
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private void FinishMissedShot()
    {
        if (laserRenderer != null)
            laserRenderer.enabled = false;

        retryTimer = retryDelaySec;
        shotCoroutine = null;
    }

    private void AbortShot()
    {
        if (shotCoroutine != null)
        {
            StopCoroutine(shotCoroutine);
            shotCoroutine = null;
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

        GameObject cooldownObject = new GameObject("K1 Cooldown");
        cooldownObject.transform.SetParent(visualRoot.transform, false);
        cooldownObject.transform.localScale = Vector3.one * 1.15f;

        if (gameplayLayer >= 0)
            cooldownObject.layer = gameplayLayer;

        cooldownRenderer = cooldownObject.AddComponent<SpriteRenderer>();
        cooldownRenderer.sprite = cooldownSprite;
        cooldownRenderer.sortingOrder = 31;

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
        if (cooldownRenderer == null)
            return;

        if (charged)
        {
            cooldownRenderer.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            return;
        }

        float progress = cooldownDuration > 0f
            ? 1f - cooldownRemaining / cooldownDuration
            : 0f;

        cooldownRenderer.color = new Color(
            1f,
            1f,
            1f,
            Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01(progress))
        );
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
