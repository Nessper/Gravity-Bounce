using UnityEngine;
using UnityEngine.Events;
using VoidScrappers.Briefing;

/// <summary>
/// ModuleRuntimeStats
/// ------------------------------------------------------------
/// Service runtime global qui agrège les effets des modules équipés.
///
/// Important :
/// - Ce script ne persiste rien.
/// - Ce script ne gère pas l'équipement.
/// - Ce script expose seulement les bonus donnés par les modules équipés.
/// - Les états gameplay vivants restent dans des controllers dédiés.
/// </summary>
public class ModuleRuntimeStats : MonoBehaviour
{
    public static ModuleRuntimeStats Instance { get; private set; }

    [Header("Références")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Debug / Lecture seule - Passifs")]
    [SerializeField] private int briefingScanTier = 0;
    [SerializeField] private int hullMaxAdd = 0;
    [SerializeField] private int flushMinBallsAdd = 0;
    [SerializeField] private int sustainHullGainEndLevel = 0;
    [SerializeField] private int sustainMoneyGainEndLevel = 0;
    [SerializeField] private float levelDurationBonusSec = 0f;

    [Header("Debug / Lecture seule - Famille A")]
    [SerializeField] private int blackFilterChargesPerMission = 0;

    [Header("Debug / Lecture seule - Famille B")]
    [SerializeField] private int flushWhiteToBlueCount = 0;
    [SerializeField] private int flushWhiteToRedCount = 0;

    [Header("Debug / Lecture seule - Famille F")]
    [SerializeField] private int medalBronzeMoney = 0;
    [SerializeField] private int medalSilverMoney = 0;
    [SerializeField] private int medalGoldMoney = 0;

    [Header("Debug / Lecture seule - Famille I")]
    [SerializeField] private float comboPointsMultiplier = 1f;

    [Header("Debug / Lecture seule - Famille J")]
    [SerializeField] private int jComboTier = 0;

    [Header("Debug / Lecture seule - Famille K1")]
    [SerializeField] private float k1CooldownSec = 0f;

    [Header("Debug / Lecture seule - Contrôle drones K0")]
    [SerializeField] private bool dronesStartCharged = false;
    [SerializeField] private float droneCooldownMultiplier = 1f;

    [Header("Debug / Lecture seule - Famille K2")]
    [SerializeField] private int k2Tier = 0;
    [SerializeField] private float k2CooldownSec = 0f;

    public UnityEvent OnStatsRebuilt = new UnityEvent();

    public int BriefingScanTier => briefingScanTier;
    public int HullMaxAdd => hullMaxAdd;
    public int FlushMinBallsAdd => flushMinBallsAdd;
    public int SustainHullGainEndLevel => sustainHullGainEndLevel;
    public int SustainMoneyGainEndLevel => sustainMoneyGainEndLevel;
    public float LevelDurationBonusSec => levelDurationBonusSec;

    public int BlackFilterChargesPerMission => blackFilterChargesPerMission;

    public int FlushWhiteToBlueCount => flushWhiteToBlueCount;
    public int FlushWhiteToRedCount => flushWhiteToRedCount;

    public int MedalBronzeMoney => medalBronzeMoney;
    public int MedalSilverMoney => medalSilverMoney;
    public int MedalGoldMoney => medalGoldMoney;
    public float ComboPointsMultiplier => comboPointsMultiplier;
    public int JComboTier => jComboTier;
    public float K1CooldownSec => k1CooldownSec;
    public bool DronesStartCharged => dronesStartCharged;
    public float DroneCooldownMultiplier => droneCooldownMultiplier;
    public int K2Tier => k2Tier;
    public float K2CooldownSec => k2CooldownSec;

    public float GetEffectiveDroneCooldown(float baseCooldownSec)
    {
        return Mathf.Max(0f, baseCooldownSec) *
            Mathf.Clamp(droneCooldownMultiplier, 0.01f, 1f);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        SubscribeToRunState();
        Rebuild();
    }

    private void OnDisable()
    {
        UnsubscribeFromRunState();

        if (Instance == this)
            Instance = null;
    }

    public void Rebuild()
    {
        ResetAggregates();

        if (runSessionState == null)
        {
            Debug.LogWarning("[ModuleRuntimeStats] RunSessionState manquant.");
            OnStatsRebuilt.Invoke();
            return;
        }

        if (!ModuleCatalogService.EnsureLoaded())
        {
            Debug.LogWarning("[ModuleRuntimeStats] ModuleCatalog introuvable.");
            OnStatsRebuilt.Invoke();
            return;
        }

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(moduleId);
            if (mod == null)
                continue;

            AggregateOneModule(mod);
        }

        OnStatsRebuilt.Invoke();
    }

    private void ResetAggregates()
    {
        briefingScanTier = 0;
        hullMaxAdd = 0;
        flushMinBallsAdd = 0;
        sustainHullGainEndLevel = 0;
        sustainMoneyGainEndLevel = 0;
        levelDurationBonusSec = 0f;

        blackFilterChargesPerMission = 0;

        flushWhiteToBlueCount = 0;
        flushWhiteToRedCount = 0;

        medalBronzeMoney = 0;
        medalSilverMoney = 0;
        medalGoldMoney = 0;
        comboPointsMultiplier = 1f;
        jComboTier = 0;
        k1CooldownSec = 0f;
        dronesStartCharged = false;
        droneCooldownMultiplier = 1f;
        k2Tier = 0;
        k2CooldownSec = 0f;
    }

    private void AggregateOneModule(ModuleDefinition mod)
    {
        if (mod == null)
            return;

        briefingScanTier = Mathf.Max(briefingScanTier, Mathf.Max(0, mod.scanTierSet));

        hullMaxAdd += Mathf.Max(0, mod.hullMaxAdd);
        flushMinBallsAdd += Mathf.Max(0, mod.flushMinBallsAdd);
        levelDurationBonusSec += Mathf.Max(0f, mod.levelDurationBonusSec);

        if (string.Equals(mod.familyId, "H", System.StringComparison.Ordinal))
        {
            sustainHullGainEndLevel += Mathf.Max(0, mod.endLevelHullRepair);
            sustainMoneyGainEndLevel += Mathf.Max(0, mod.endLevelMoneyGain);
        }

        blackFilterChargesPerMission += Mathf.Max(0, mod.blackFilterChargesPerMission);

        flushWhiteToBlueCount += Mathf.Max(0, mod.flushWhiteToBlueCount);
        flushWhiteToRedCount += Mathf.Max(0, mod.flushWhiteToRedCount);

        medalBronzeMoney += Mathf.Max(0, mod.medalBronzeMoney);
        medalSilverMoney += Mathf.Max(0, mod.medalSilverMoney);
        medalGoldMoney += Mathf.Max(0, mod.medalGoldMoney);

        if (mod.comboPointsMultiplierSet > 0f)
        {
            comboPointsMultiplier = Mathf.Max(
                comboPointsMultiplier,
                mod.comboPointsMultiplierSet);
        }

        jComboTier = Mathf.Max(jComboTier, Mathf.Max(0, mod.jComboTierSet));

        dronesStartCharged |= mod.dronesStartCharged;

        if (mod.droneCooldownMultiplier > 0f)
        {
            droneCooldownMultiplier = Mathf.Min(
                droneCooldownMultiplier,
                mod.droneCooldownMultiplier
            );
        }

        if (mod.k1CooldownSec > 0f &&
            (k1CooldownSec <= 0f || mod.k1CooldownSec < k1CooldownSec))
        {
            k1CooldownSec = mod.k1CooldownSec;
        }

        k2Tier = Mathf.Max(k2Tier, Mathf.Max(0, mod.k2TierSet));

        if (mod.k2CooldownSec > 0f &&
            (k2CooldownSec <= 0f || mod.k2CooldownSec < k2CooldownSec))
        {
            k2CooldownSec = mod.k2CooldownSec;
        }
    }

    private void SubscribeToRunState()
    {
        if (runSessionState == null)
            return;

        runSessionState.OnEquipmentChanged.AddListener(Rebuild);
        runSessionState.OnShipChanged.AddListener(OnShipChanged);
    }

    private void UnsubscribeFromRunState()
    {
        if (runSessionState == null)
            return;

        runSessionState.OnEquipmentChanged.RemoveListener(Rebuild);
        runSessionState.OnShipChanged.RemoveListener(OnShipChanged);
    }

    private void OnShipChanged(string newShipId)
    {
        Rebuild();
    }

    public BriefingTier GetEffectiveBriefingTier()
    {
        switch (briefingScanTier)
        {
            case 3:
                return BriefingTier.T3;
            case 2:
                return BriefingTier.T2;
            case 1:
                return BriefingTier.T1;
            default:
                return BriefingTier.T0;
        }
    }

    public (int hullGain, int moneyGain) GetEndLevelSustainBonus()
    {
        return (sustainHullGainEndLevel, sustainMoneyGainEndLevel);
    }

    public (int fullHullHullMaxAdd, int scoreDelta) GetEndLevelCoreGrowthEffect()
    {
        if (runSessionState == null)
            return (0, 0);

        if (!ModuleCatalogService.EnsureLoaded())
            return (0, 0);

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(moduleId);
            if (mod == null)
                continue;

            if (!string.Equals(mod.familyId, "C", System.StringComparison.Ordinal))
                continue;

            return (
                Mathf.Max(0, mod.endLevelFullHullHullMaxAdd),
                mod.endLevelScoreDelta
            );
        }

        return (0, 0);
    }

    public ModuleDefinition GetEndLevelCoreGrowthModule()
    {
        if (runSessionState == null)
            return null;

        if (!ModuleCatalogService.EnsureLoaded())
            return null;

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(moduleId);
            if (mod == null)
                continue;

            if (!string.Equals(mod.familyId, "C", System.StringComparison.Ordinal))
                continue;

            return mod;
        }

        return null;
    }

    public ModuleDefinition GetEndLevelSustainModule()
    {
        if (runSessionState == null)
            return null;

        if (!ModuleCatalogService.EnsureLoaded())
            return null;

        for (int i = 0; i < runSessionState.EquipmentSlotCount; i++)
        {
            string moduleId = runSessionState.GetEquippedModuleId(i);
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleDefinition mod = ModuleCatalogService.GetById(moduleId);
            if (mod == null)
                continue;

            if (!string.Equals(mod.familyId, "H", System.StringComparison.Ordinal))
                continue;

            return mod;
        }

        return null;
    }
}
