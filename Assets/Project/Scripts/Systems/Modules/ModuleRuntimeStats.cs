using UnityEngine;
using UnityEngine.Events;
using VoidScrappers.Briefing;

/// <summary>
/// ModuleRuntimeStats
///
/// Responsabilites :
/// - Lire les modules equipes dans RunSessionState.
/// - Charger leurs definitions via ModuleCatalogService.
/// - Agreger les effets runtime utiles pour le gameplay.
/// - Exposer un snapshot simple pour le reste du jeu.
/// - Se reconstruire quand l'equipement ou le ship changent.
///
/// Important :
/// - Ce script ne persiste rien.
/// - Ce script ne gere pas l'equipement.
/// - Ce script ne fait qu'agreger des effets a partir de l'etat courant.
/// </summary>
public class ModuleRuntimeStats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunSessionState runSessionState;

    [Header("Debug / Read Only")]
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

    private void OnEnable()
    {
        SubscribeToRunState();
        Rebuild();
    }

    private void OnDisable()
    {
        UnsubscribeFromRunState();
    }

    /// <summary>
    /// Recalcule tous les effets modules a partir des modules equipes.
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

    private void ResetAggregates()
    {
        briefingScanTier = 0;
        hullMaxAdd = 0;
        flushMinBallsAdd = 0;
        sustainHullGainEndLevel = 0;
        sustainMoneyGainEndLevel = 0;
    }

    private void AggregateOneModule(ModuleDefinition mod)
    {
        if (mod == null)
            return;

        briefingScanTier = Mathf.Max(briefingScanTier, Mathf.Max(0, mod.scanTierSet));
        hullMaxAdd += Mathf.Max(0, mod.hullMaxAdd);
        flushMinBallsAdd += Mathf.Max(0, mod.flushMinBallsAdd);

        if (string.Equals(mod.familyId, "H", System.StringComparison.Ordinal))
        {
            switch (Mathf.Max(1, mod.tier))
            {
                case 1:
                    sustainHullGainEndLevel += 1;
                    break;

                case 2:
                    sustainHullGainEndLevel += 1;
                    sustainMoneyGainEndLevel += 1;
                    break;

                case 3:
                    sustainHullGainEndLevel += 2;
                    sustainMoneyGainEndLevel += 1;
                    break;
            }
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
    /// Helper de compatibilite avec le systeme de briefing existant.
    /// </summary>
    public BriefingTier GetEffectiveBriefingTier()
    {
        switch (briefingScanTier)
        {
            case 3: return BriefingTier.T3;
            case 2: return BriefingTier.T2;
            case 1: return BriefingTier.T1;
            default: return BriefingTier.T0;
        }
    }

    /// <summary>
    /// Helper de compatibilite avec le flow end-level existant.
    /// </summary>
    public (int hullGain, int moneyGain) GetEndLevelSustainBonus()
    {
        return (sustainHullGainEndLevel, sustainMoneyGainEndLevel);
    }
}