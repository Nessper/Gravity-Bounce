using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Socle runtime commun a tous les drones de gameplay.
/// Gere l'equipement, le cooldown, la charge, les transitions de mission
/// et la presentation commune sans connaitre le comportement du drone.
/// </summary>
public abstract class DroneRuntimeControllerBase : MonoBehaviour
{
    [Header("Dependances communes")]
    [SerializeField] protected LevelRunStateController runStateController;

    [Header("Presentation commune")]
    [SerializeField] protected Sprite droneSprite;
    [SerializeField] protected Sprite cooldownSprite;
    [SerializeField] protected float droneScale = 0.105f;
    [SerializeField] protected float cooldownVisualScale = 1.6f;

    private GameObject visualRoot;
    private Image cooldownImage;

    private float cooldownDuration;
    private float cooldownRemaining;

    private bool moduleEquipped;
    private bool gameplayWasArmed;
    private bool charged;

    protected bool IsDroneCharged => charged;
    protected bool IsDroneGameplayArmed => gameplayWasArmed;
    protected float DroneCooldownDuration => cooldownDuration;
    protected Transform DroneVisualRoot =>
        visualRoot != null ? visualRoot.transform : null;

    protected abstract string DroneVisualName { get; }
    protected abstract bool HasDroneActionInProgress { get; }

    protected abstract float GetBaseCooldownSec(ModuleRuntimeStats stats);
    protected abstract void UpdateDroneMotion();
    protected abstract void TryBeginChargedAction();

    protected virtual void OnDroneVisualsCreated() { }
    protected virtual void OnDroneEnabled() { }
    protected virtual void OnDroneDisabled() { }
    protected virtual void OnDroneGameplayTick() { }
    protected virtual void OnDroneGameplayStarted() { }
    protected virtual void OnDroneRuntimeStopped() { }
    protected virtual bool UsesCustomChargePresentation => false;
    protected virtual void OnDroneChargePresentationUpdated(
        float normalizedProgress,
        bool isCharged) { }

    private void Awake()
    {
        CreateCommonVisuals();
        OnDroneVisualsCreated();
        RefreshModuleRuntime();
    }

    private void OnEnable()
    {
        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.AddListener(
                RefreshModuleRuntime
            );

        OnDroneEnabled();
    }

    private void OnDisable()
    {
        if (ModuleRuntimeStats.Instance != null)
            ModuleRuntimeStats.Instance.OnStatsRebuilt.RemoveListener(
                RefreshModuleRuntime
            );

        OnDroneDisabled();
        gameplayWasArmed = false;
        StopMissionRuntime();
    }

    private void Update()
    {
        if (!moduleEquipped)
            return;

        UpdateDroneMotion();

        bool gameplayArmed =
            runStateController != null && runStateController.GameplayArmed;

        if (gameplayArmed && !gameplayWasArmed)
            StartMissionRuntime();
        else if (!gameplayArmed && gameplayWasArmed)
            StopMissionRuntime();

        gameplayWasArmed = gameplayArmed;

        if (!gameplayArmed)
        {
            UpdateCooldownVisual();
            return;
        }

        OnDroneGameplayTick();

        if (!HasDroneActionInProgress && !charged)
        {
            cooldownRemaining = Mathf.Max(
                0f,
                cooldownRemaining - Time.deltaTime
            );

            if (cooldownRemaining <= 0f)
                charged = true;
        }

        if (charged && !HasDroneActionInProgress)
            TryBeginChargedAction();

        UpdateCooldownVisual();
    }

    protected bool TryConsumeDroneCharge()
    {
        if (!charged)
            return false;

        charged = false;
        cooldownRemaining = cooldownDuration;
        return true;
    }

    private void RefreshModuleRuntime()
    {
        bool wasEquipped = moduleEquipped;
        float previousDuration = cooldownDuration;
        float previousRemaining = cooldownRemaining;

        ModuleRuntimeStats stats = ModuleRuntimeStats.Instance;
        float baseCooldown = Mathf.Max(0f, GetBaseCooldownSec(stats));

        cooldownDuration = stats != null
            ? stats.GetEffectiveDroneCooldown(baseCooldown)
            : baseCooldown;
        moduleEquipped = baseCooldown > 0f;

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
            return;
        }

        // Une modification transversale en debug conserve la progression
        // relative du cooldown au lieu de le reinitialiser.
        if (!charged && previousDuration > 0f)
        {
            float progress = 1f - previousRemaining / previousDuration;
            cooldownRemaining = cooldownDuration *
                (1f - Mathf.Clamp01(progress));
        }
    }

    private void StartMissionRuntime()
    {
        OnDroneRuntimeStopped();

        bool startsCharged =
            ModuleRuntimeStats.Instance != null &&
            ModuleRuntimeStats.Instance.DronesStartCharged;

        charged = startsCharged;
        cooldownRemaining = startsCharged ? 0f : cooldownDuration;

        OnDroneGameplayStarted();
    }

    private void StopMissionRuntime()
    {
        OnDroneRuntimeStopped();
        charged = false;
        cooldownRemaining = cooldownDuration;
    }

    private void CreateCommonVisuals()
    {
        visualRoot = new GameObject(DroneVisualName + " Visual");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localScale = Vector3.one * droneScale;

        int gameplayLayer = LayerMask.NameToLayer("Gameplay");
        if (gameplayLayer >= 0)
            visualRoot.layer = gameplayLayer;

        // Certains drones construisent leur propre presentation de charge.
        // Le socle conserve le timer et ne cree alors aucun anneau historique.
        if (UsesCustomChargePresentation)
            return;

        SpriteRenderer droneRenderer =
            visualRoot.AddComponent<SpriteRenderer>();
        droneRenderer.sprite = droneSprite;
        droneRenderer.sortingOrder = 30;

        GameObject cooldownCanvasObject = new GameObject(
            DroneVisualName + " Cooldown Canvas",
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
            DroneVisualName + " Cooldown Background",
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

        StretchToParent(
            cooldownBackgroundObject.GetComponent<RectTransform>()
        );

        Image cooldownBackgroundImage =
            cooldownBackgroundObject.GetComponent<Image>();
        cooldownBackgroundImage.sprite = cooldownSprite;
        cooldownBackgroundImage.type = Image.Type.Simple;
        cooldownBackgroundImage.preserveAspect = true;
        cooldownBackgroundImage.raycastTarget = false;
        cooldownBackgroundImage.color =
            new Color(1f, 1f, 1f, 0.18f);

        GameObject cooldownObject = new GameObject(
            DroneVisualName + " Cooldown Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        cooldownObject.transform.SetParent(cooldownCanvasObject.transform, false);

        if (gameplayLayer >= 0)
            cooldownObject.layer = gameplayLayer;

        StretchToParent(cooldownObject.GetComponent<RectTransform>());

        cooldownImage = cooldownObject.GetComponent<Image>();
        cooldownImage.sprite = cooldownSprite;
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Top;
        cooldownImage.fillClockwise = true;
        cooldownImage.fillAmount = 0f;
        cooldownImage.preserveAspect = true;
        cooldownImage.raycastTarget = false;
        cooldownImage.color = new Color(1f, 1f, 1f, 0.9f);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void UpdateCooldownVisual()
    {
        float progress = charged
            ? 1f
            : cooldownDuration > 0f
            ? 1f - cooldownRemaining / cooldownDuration
            : 0f;
        progress = Mathf.Clamp01(progress);

        if (cooldownImage != null)
            cooldownImage.fillAmount = progress;

        // La presentation ne mesure jamais le temps elle-meme : elle recoit
        // uniquement la progression normalisee du cooldown autoritaire.
        OnDroneChargePresentationUpdated(progress, charged);
    }
}
