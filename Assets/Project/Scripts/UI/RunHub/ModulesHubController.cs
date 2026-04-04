using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Facade métier simplifiée pour le système Modules côté UI.
///
/// Responsabilités principales :
/// - Accès au catalogue de modules
/// - Lecture de l'inventaire owned via SaveManager
/// - Achat d'un module (débit money + ajout owned)
/// - Équipement run-only via RunSessionState
/// - Chargement et cache des icônes
/// - Gestion de l'offre du shop :
///   - deal initial de 3 modules
///   - consommation sans refill automatique
///   - reroll persistant
///
/// Important :
/// - Les règles métier d'équipement ne vivent PAS ici.
///   Elles restent dans RunSessionState / RunModuleEquipmentService.
/// - Les règles de composition d'offre ne vivent PAS ici.
///   Elles restent dans ModulesShopOfferRules.
/// - Ce controller orchestre et relie les systèmes, sans dupliquer la logique métier.
/// </summary>
public class ModulesHubController : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // CONSTANTES
    // ---------------------------------------------------------------------

    /// <summary>
    /// Taille fixe de l'offre shop.
    /// </summary>
    private const int ShopOfferCount = 3;

    // ---------------------------------------------------------------------
    // DEPENDENCIES / EVENTS
    // ---------------------------------------------------------------------

    [Header("Deps")]
    [SerializeField] private RunSessionState runSession;

    /// <summary>
    /// Accès public au RunSessionState utilisé par ce hub.
    /// </summary>
    public RunSessionState RunSession => runSession;

    /// <summary>
    /// Émis quand l'inventaire visible côté UI change
    /// (achat, reroll, deal vidé, etc.).
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// Émis quand l'équipement change.
    /// </summary>
    public event Action OnEquipmentChanged;

    // ---------------------------------------------------------------------
    // ICON CACHE
    // ---------------------------------------------------------------------

    /// <summary>
    /// Cache par moduleId.
    /// </summary>
    private readonly Dictionary<string, Sprite> spriteCacheByModuleId = new Dictionary<string, Sprite>();

    /// <summary>
    /// Cache par chemin Resources.
    /// </summary>
    private readonly Dictionary<string, Sprite> spriteCacheByPath = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ---------------------------------------------------------------------

    private void OnEnable()
    {
        if (runSession != null)
            runSession.OnEquipmentChanged.AddListener(HandleEquipmentChanged);
    }

    private void OnDisable()
    {
        if (runSession != null)
            runSession.OnEquipmentChanged.RemoveListener(HandleEquipmentChanged);
    }

    private void HandleEquipmentChanged()
    {
        OnEquipmentChanged?.Invoke();
    }

    // ---------------------------------------------------------------------
    // CATALOG ACCESS
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne tous les modules du catalogue.
    /// </summary>
    public bool TryGetAllModules(out List<ModuleDefinition> modules)
    {
        modules = null;

        if (!ModuleCatalogService.EnsureLoaded())
            return false;

        if (ModuleCatalogService.Catalog == null || ModuleCatalogService.Catalog.modules == null)
            return false;

        modules = ModuleCatalogService.Catalog.modules;
        return true;
    }

    /// <summary>
    /// Retourne un module à partir de son id.
    /// </summary>
    public bool TryGetModuleById(string moduleId, out ModuleDefinition module)
    {
        module = null;

        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (!ModuleCatalogService.EnsureLoaded())
            return false;

        List<ModuleDefinition> list = ModuleCatalogService.Catalog.modules;
        if (list == null)
            return false;

        module = list.Find(m => m != null && string.Equals(m.id, moduleId, StringComparison.Ordinal));
        return module != null;
    }

    // ---------------------------------------------------------------------
    // OWNED / INVENTORY
    // ---------------------------------------------------------------------

    /// <summary>
    /// Indique si un module est déjà possédé.
    /// </summary>
    public bool IsOwned(string moduleId)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        return SaveManager.Instance.HasOwnedModule(moduleId);
    }

    // ---------------------------------------------------------------------
    // EQUIPMENT STATE HELPERS
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne true si le module est actuellement équipé dans un slot.
    /// </summary>
    public bool IsEquipped(string moduleId)
    {
        if (runSession == null || string.IsNullOrEmpty(moduleId))
            return false;

        int count = runSession.EquipmentSlotCount;
        for (int i = 0; i < count; i++)
        {
            if (runSession.GetEquippedModuleId(i) == moduleId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Nombre total de slots d'équipement.
    /// </summary>
    public int EquipmentSlotCount
    {
        get
        {
            if (runSession == null)
                return 0;

            return runSession.EquipmentSlotCount;
        }
    }

    /// <summary>
    /// Indique si un slot est verrouillé.
    /// </summary>
    public bool IsSlotLocked(int slotIndex)
    {
        if (runSession == null)
            return true;

        return runSession.IsEquipmentSlotLocked(slotIndex);
    }

    /// <summary>
    /// Retourne le module équipé dans un slot.
    /// </summary>
    public string GetEquippedModuleIdInSlot(int slotIndex)
    {
        if (runSession == null)
            return null;

        return runSession.GetEquippedModuleId(slotIndex);
    }

    /// <summary>
    /// Retourne la liste des slots ouverts.
    /// Aucune règle d'équipement n'est appliquée ici.
    /// </summary>
    public List<int> GetOpenSlots()
    {
        List<int> result = new List<int>(6);

        if (runSession == null)
            return result;

        int count = runSession.EquipmentSlotCount;
        for (int i = 0; i < count; i++)
        {
            if (!runSession.IsEquipmentSlotLocked(i))
                result.Add(i);
        }

        return result;
    }

    /// <summary>
    /// Tente d'équiper un module dans un slot.
    /// La validation métier reste dans RunSessionState.
    /// </summary>
    public bool TryEquipModuleInSlot(string moduleId, int slotIndex)
    {
        if (runSession == null)
            return false;

        return runSession.TryEquipModuleToSlot(moduleId, slotIndex);
    }

    /// <summary>
    /// Tente de déséquiper un slot.
    /// </summary>
    public bool TryUnequipSlot(int slotIndex)
    {
        if (runSession == null)
            return false;

        return runSession.UnequipSlot(slotIndex);
    }

    // ---------------------------------------------------------------------
    // SHOP BUY
    // ---------------------------------------------------------------------

    /// <summary>
    /// Tente d'acheter un module :
    /// - vérifie qu'il n'est pas déjà owned
    /// - dépense la money
    /// - ajoute le module à l'inventaire owned
    /// - retire le module de l'offre courante
    /// </summary>
    public bool TryBuy(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (IsOwned(moduleId))
            return false;

        if (!TryGetModuleById(moduleId, out ModuleDefinition def) || def == null)
            return false;

        if (runSession == null)
        {
            Debug.LogError("[ModulesHubController] RunSessionState manquant (money event impossible).");
            return false;
        }

        int cost = Mathf.Max(0, def.cost);

        bool spent = runSession.TrySpendMoney(cost);
        if (!spent)
            return false;

        bool added = SaveManager.Instance.TryAddOwnedModule(moduleId);
        if (!added)
        {
            Debug.LogWarning("[ModulesHubController] Achat incohérent: money dépensée mais module non ajouté. " + moduleId);

            // Cohérence UX :
            // si l'achat est considéré comme passé, on retire quand même l'offre.
            RemoveFromShopOffer(moduleId);
            OnInventoryChanged?.Invoke();
            return true;
        }

        spriteCacheByModuleId.Remove(moduleId);

        // Une fois acheté, le module disparaît de l'offre courante.
        RemoveFromShopOffer(moduleId);
        OnInventoryChanged?.Invoke();

        return true;
    }

    // ---------------------------------------------------------------------
    // SHOP REROLL
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne le nombre de rerolls déjà effectués pour le shop courant.
    /// </summary>
    public int GetShopRerollCount()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return 0;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return 0;

        return Mathf.Max(0, run.shopRerollCount);
    }

    /// <summary>
    /// Vide l'offre courante de shop pour forcer un nouveau deal au prochain refresh.
    /// </summary>
    public void ForceRerollShopOfferAndPersist()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = null;

        SaveManager.Instance.Save();

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Incrémente et persiste le compteur de rerolls.
    /// </summary>
    public void IncrementShopRerollCountAndPersist()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        run.shopRerollCount = Mathf.Max(0, run.shopRerollCount) + 1;
        SaveManager.Instance.Save();
    }

    // ---------------------------------------------------------------------
    // ICONS
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne le sprite d'icône d'un module depuis Resources avec cache.
    /// </summary>
    public Sprite GetModuleIconSprite(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return null;

        if (spriteCacheByModuleId.TryGetValue(moduleId, out Sprite cachedByModuleId) && cachedByModuleId != null)
            return cachedByModuleId;

        if (!TryGetModuleById(moduleId, out ModuleDefinition def) || def == null)
            return null;

        if (string.IsNullOrEmpty(def.iconPath))
            return null;

        if (spriteCacheByPath.TryGetValue(def.iconPath, out Sprite cachedByPath) && cachedByPath != null)
        {
            spriteCacheByModuleId[moduleId] = cachedByPath;
            return cachedByPath;
        }

        Sprite loaded = Resources.Load<Sprite>(def.iconPath);
        if (loaded == null)
        {
            Debug.LogWarning("[ModulesHubController] Sprite introuvable dans Resources: " + def.iconPath + " (moduleId=" + moduleId + ")");
            return null;
        }

        spriteCacheByPath[def.iconPath] = loaded;
        spriteCacheByModuleId[moduleId] = loaded;
        return loaded;
    }

    /// <summary>
    /// Vide le cache d'icônes.
    /// </summary>
    public void ClearIconCache()
    {
        spriteCacheByModuleId.Clear();
        spriteCacheByPath.Clear();
    }

    // ---------------------------------------------------------------------
    // SHOP OFFER - PUBLIC API
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne les modules visibles dans le shop courant.
    ///
    /// Comportement :
    /// - si le node courant n'est pas un shop => renvoie une liste vide
    /// - si aucune offre n'existe => deal l'offre et persiste
    /// - si une offre existe => renvoie ce qu'il en reste
    /// </summary>
    public bool TryGetShopVisibleModules(out List<ModuleDefinition> modules)
    {
        modules = new List<ModuleDefinition>();

        // Hors contexte shop : pas d'erreur, pas de deal, liste vide.
        ShopStage shopStage = GetCurrentShopStage();
        if (shopStage == ShopStage.None)
            return true;

        if (!TryGetAllModules(out List<ModuleDefinition> allModules) || allModules == null)
            return false;

        EnsureShopOfferDealt(allModules, shopStage);

        modules = BuildShopModulesFromOfferIds(allModules);
        return modules != null;
    }

    // ---------------------------------------------------------------------
    // SHOP OFFER - INTERNAL
    // ---------------------------------------------------------------------

    /// <summary>
    /// Deal une nouvelle offre de shop si l'offre courante est vide.
    /// Ne fait rien :
    /// - hors contexte shop
    /// - si une offre existe déjà
    /// - si la save n'est pas disponible
    /// </summary>
    private void EnsureShopOfferDealt(List<ModuleDefinition> allModules, ShopStage shopStage)
    {
        if (shopStage == ShopStage.None)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        // Si au moins un slot contient déjà un id, on considère que l'offre existe.
        bool offerIsEmpty = true;
        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(run.shopOfferModuleIds[i]))
            {
                offerIsEmpty = false;
                break;
            }
        }

        if (!offerIsEmpty)
            return;

        string worldId = run.worldId;
        int rerollCount = Mathf.Max(0, run.shopRerollCount);

        List<ModuleDefinition> offer = ModulesShopOfferRules.BuildOffer(
            allModules,
            IsOwned,
            worldId,
            shopStage,
            rerollCount,
            ShopOfferCount);

        if (offer == null || offer.Count == 0)
            return;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = null;

        for (int i = 0; i < offer.Count && i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = offer[i].id;

        SaveManager.Instance.Save();
    }

    /// <summary>
    /// Reconstruit la liste des modules visibles à partir des ids persistés en save.
    ///
    /// Nettoie automatiquement :
    /// - les modules déjà owned
    /// - les ids devenus invalides
    /// </summary>
    private List<ModuleDefinition> BuildShopModulesFromOfferIds(List<ModuleDefinition> allModules)
    {
        List<ModuleDefinition> result = new List<ModuleDefinition>(ShopOfferCount);

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return result;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return result;

        bool changed = false;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            string moduleId = run.shopOfferModuleIds[i];
            if (string.IsNullOrEmpty(moduleId))
                continue;

            // Si le module est maintenant owned, on purge l'offre.
            if (IsOwned(moduleId))
            {
                run.shopOfferModuleIds[i] = null;
                changed = true;
                continue;
            }

            ModuleDefinition def = allModules.Find(m => m != null && string.Equals(m.id, moduleId, StringComparison.Ordinal));
            if (def != null)
            {
                result.Add(def);
            }
            else
            {
                // Sécurité : si le module n'existe plus dans le catalogue, on purge le slot.
                run.shopOfferModuleIds[i] = null;
                changed = true;
            }
        }

        if (changed)
            SaveManager.Instance.Save();

        return result;
    }

    /// <summary>
    /// Retire un module précis de l'offre courante.
    /// Utilisé après achat.
    /// </summary>
    private void RemoveFromShopOffer(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return;

        bool changed = false;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            if (!string.Equals(run.shopOfferModuleIds[i], moduleId, StringComparison.Ordinal))
                continue;

            run.shopOfferModuleIds[i] = null;
            changed = true;
        }

        if (changed)
            SaveManager.Instance.Save();
    }

    // ---------------------------------------------------------------------
    // SHOP CONTEXT
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne le ShopStage du node courant.
    ///
    /// Retourne None si :
    /// - le RunSessionState n'est pas disponible
    /// - le plan n'est pas chargé
    /// - le node courant n'est pas un shop
    /// </summary>
    private ShopStage GetCurrentShopStage()
    {
        if (runSession == null)
            return ShopStage.None;

        if (!runSession.EnsurePlanLoaded())
            return ShopStage.None;

        RunPlan plan = runSession.CurrentRunPlan;
        if (plan == null)
            return ShopStage.None;

        RunNode node = plan.CurrentPlayableNode;
        if (node == null)
            return ShopStage.None;

        if (node.type != RunNodeType.Shop)
            return ShopStage.None;

        return node.shopStage;
    }
}