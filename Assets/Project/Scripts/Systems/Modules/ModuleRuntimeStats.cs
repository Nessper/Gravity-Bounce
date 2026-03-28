// Chemin recommandé (projet Unity) : Scripts/Systems/Modules/ModuleRuntimeStats.cs

using UnityEngine;
using UnityEngine.Events;
using VoidScrappers.Briefing;

/// <summary>
/// ModuleRuntimeStats
/// ------------------------------------------------------------
/// Service runtime global qui agrège les effets des modules équipés.
///
/// Responsabilités :
/// - Lire les modules équipés dans RunSessionState.
/// - Charger leurs définitions via ModuleCatalogService.
/// - Agréger les effets runtime utiles pour le gameplay.
/// - Exposer un snapshot simple pour le reste du jeu.
/// - Se reconstruire quand l'équipement ou le ship changent.
///
/// Important :
/// - Ce script ne persiste rien.
/// - Ce script ne gère pas l'équipement.
/// - Ce script ne fait qu'agréger des effets à partir de l'état courant.
/// - Il vit une seule fois dans BootRoot et s'expose via Instance.
/// </summary>
public class ModuleRuntimeStats : MonoBehaviour
{
    public static ModuleRuntimeStats Instance { get; private set; }

    [Header("Références")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Debug / Lecture seule")]
    [SerializeField] private int briefingScanTier = 0;
    [SerializeField] private int hullMaxAdd = 0;
    [SerializeField] private int flushMinBallsAdd = 0;
    [SerializeField] private int sustainHullGainEndLevel = 0;
    [SerializeField] private int sustainMoneyGainEndLevel = 0;

    public UnityEvent OnStatsRebuilt = new UnityEvent();

    public int BriefingScanTier => briefingScanTier;
    public int HullMaxAdd => hullMaxAdd;
    public int FlushMinBallsAdd => flushMinBallsAdd;
    public int SustainHullGainEndLevel => sustainHullGainEndLevel;
    public int SustainMoneyGainEndLevel => sustainMoneyGainEndLevel;

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

    /// <summary>
    /// Recalcule tous les effets modules à partir des modules équipés.
    /// </summary>
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

    /// <summary>
    /// Remet toutes les stats agrégées à zéro avant reconstruction.
    /// </summary>
    private void ResetAggregates()
    {
        briefingScanTier = 0;
        hullMaxAdd = 0;
        flushMinBallsAdd = 0;
        sustainHullGainEndLevel = 0;
        sustainMoneyGainEndLevel = 0;
    }

    /// <summary>
    /// Agrège les effets d'un module unique.
    /// </summary>
    private void AggregateOneModule(ModuleDefinition mod)
    {
        if (mod == null)
            return;

        // SCAN
        briefingScanTier = Mathf.Max(briefingScanTier, Mathf.Max(0, mod.scanTierSet));

        // Bonus passifs déclaratifs
        hullMaxAdd += Mathf.Max(0, mod.hullMaxAdd);
        flushMinBallsAdd += Mathf.Max(0, mod.flushMinBallsAdd);

        // Sustain famille H
        if (string.Equals(mod.familyId, "H", System.StringComparison.Ordinal))
        {
            sustainHullGainEndLevel += Mathf.Max(0, mod.endLevelHullRepair);
            sustainMoneyGainEndLevel += Mathf.Max(0, mod.endLevelMoneyGain);
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

    /// <summary>
    /// Helper de compatibilité avec le système de briefing existant.
    /// </summary>
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

    /// <summary>
    /// Helper de compatibilité avec le flow end-level existant.
    /// </summary>
    public (int hullGain, int moneyGain) GetEndLevelSustainBonus()
    {
        return (sustainHullGainEndLevel, sustainMoneyGainEndLevel);
    }

    /// <summary>
    /// Retourne l'effet de fin de niveau de la famille C.
    ///
    /// Règle :
    /// - le score delta est toujours appliqué
    /// - le bonus HullMax ne doit être appliqué que si Hull plein,
    ///   décision prise par le flow appelant
    /// </summary>
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