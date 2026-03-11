using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Facade "metier" simplifiee pour le systeme Modules (UI-side).
/// Responsabilites:
/// - Acces au catalog (ModuleCatalogService)
/// - Inventaire owned via SaveManager
/// - Achat (debit money via RunSessionState pour declencher OnMoneyChanged)
/// - Equipement (run-only) via RunSessionState
/// - Chargement d'icones via Resources (iconPath) avec cache
/// - Shop: offre de 3 modules "deal" une fois, consommee sans refill (V2)
///
/// IMPORTANT:
/// - Toutes les regles d'equipement (tiers, exclusivite famille, owned, slot lock) sont dans RunSessionState.
/// - Ce controller fait confiance a RunSessionState et ne duplique pas la logique.
/// </summary>
public class ModulesHubController : MonoBehaviour
{
    // ------------------------------------------------------------
    // Deps / Events
    // ------------------------------------------------------------

    [Header("Deps")]
    [SerializeField] private RunSessionState runSession;
    public RunSessionState RunSession => runSession;

    public event Action OnInventoryChanged;
    public event Action OnEquipmentChanged;

    // Cache sprites (evite des Resources.Load en boucle)
    private readonly Dictionary<string, Sprite> spriteCacheByModuleId = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> spriteCacheByPath = new Dictionary<string, Sprite>();

    private const int ShopOfferCount = 3;

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

    // ------------------------------------------------------------
    // Catalog
    // ------------------------------------------------------------

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

    public bool TryGetModuleById(string moduleId, out ModuleDefinition module)
    {
        module = null;

        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (!ModuleCatalogService.EnsureLoaded())
            return false;

        var list = ModuleCatalogService.Catalog.modules;
        if (list == null)
            return false;

        module = list.Find(m => m != null && string.Equals(m.id, moduleId, StringComparison.Ordinal));
        return module != null;
    }

    // ------------------------------------------------------------
    // Owned (inventory)
    // ------------------------------------------------------------

    public bool IsOwned(string moduleId)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        return SaveManager.Instance.HasOwnedModule(moduleId);
    }

    // ------------------------------------------------------------
    // Equip state helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Retourne true si le module est actuellement equipe dans un slot.
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

    // ------------------------------------------------------------
    // Achat
    // ------------------------------------------------------------

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
            Debug.LogWarning("[ModulesHubController] Achat incoherent: money depensee mais module non ajoute. " + moduleId);

            // On retire quand meme de l'offre si present (cohérence UX: acheté = disparaît)
            RemoveFromShopOffer(moduleId);

            OnInventoryChanged?.Invoke();
            return true;
        }

        spriteCacheByModuleId.Remove(moduleId);

        // NEW V2: une fois acheté, on retire de l'offre (sans refill)
        RemoveFromShopOffer(moduleId);

        OnInventoryChanged?.Invoke();
        return true;
    }

    // ------------------------------------------------------------
    // REROLL MODULES
    // ------------------------------------------------------------

    public int GetShopRerollCount()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return 0;

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return 0;

        return Mathf.Max(0, run.shopRerollCount);
    }

    public void ForceRerollShopOfferAndPersist()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return;

        //  on vide les slots (au lieu de mettre le tableau à null)
        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = null;

        SaveManager.Instance.Save();

        // refresh UI
        OnInventoryChanged?.Invoke();
    }

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

    // ------------------------------------------------------------
    // Equipment (run-only)
    // ------------------------------------------------------------

    public int EquipmentSlotCount
    {
        get
        {
            if (runSession == null)
                return 0;
            return runSession.EquipmentSlotCount;
        }
    }

    public bool IsSlotLocked(int slotIndex)
    {
        if (runSession == null)
            return true;

        return runSession.IsEquipmentSlotLocked(slotIndex);
    }

    public string GetEquippedModuleIdInSlot(int slotIndex)
    {
        if (runSession == null)
            return null;

        return runSession.GetEquippedModuleId(slotIndex);
    }

    /// <summary>
    /// Retourne la liste des slots ouverts (non locked).
    /// IMPORTANT:
    /// - Aucune regle d'equipement n'est appliquee ici.
    /// - L'equipement final passe toujours par RunSessionState.TryEquipModuleToSlot.
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
    /// Tente d'equiper un module dans un slot.
    /// IMPORTANT:
    /// - Aucune verification ici.
    /// - RunSessionState est la source de verite (tiers, owned, famille, slot locked).
    /// </summary>
    public bool TryEquipModuleInSlot(string moduleId, int slotIndex)
    {
        if (runSession == null)
            return false;

        return runSession.TryEquipModuleToSlot(moduleId, slotIndex);
    }

    /// <summary>
    /// Desequipe le module present dans le slot.
    /// </summary>
    public bool TryUnequipSlot(int slotIndex)
    {
        if (runSession == null)
            return false;

        return runSession.UnequipSlot(slotIndex);
    }

    // ------------------------------------------------------------
    // Icons (Resources via iconPath)
    // ------------------------------------------------------------

    public Sprite GetModuleIconSprite(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return null;

        if (spriteCacheByModuleId.TryGetValue(moduleId, out Sprite cached) && cached != null)
            return cached;

        if (!TryGetModuleById(moduleId, out ModuleDefinition def) || def == null)
            return null;

        if (string.IsNullOrEmpty(def.iconPath))
            return null;

        if (spriteCacheByPath.TryGetValue(def.iconPath, out Sprite byPath) && byPath != null)
        {
            spriteCacheByModuleId[moduleId] = byPath;
            return byPath;
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

    public void ClearIconCache()
    {
        spriteCacheByModuleId.Clear();
        spriteCacheByPath.Clear();
    }

    // ------------------------------------------------------------
    // SHOP (V2): 3 modules dealés une fois, consommés sans refill
    // ------------------------------------------------------------

    /// <summary>
    /// Retourne les modules à afficher dans le shop:
    /// - si aucune offre n'existe => "deal" 3 modules et persiste
    /// - si offre existe => renvoie ce qu'il reste (2/1/0), sans refill
    /// </summary>
    public bool TryGetShopVisibleModules(out List<ModuleDefinition> modules)
    {
        
        modules = null;

        if (!TryGetAllModules(out List<ModuleDefinition> all) || all == null)
            return false;

        EnsureShopOfferDealt(all);

        modules = BuildShopModulesFromOfferIds(all);
        return modules != null;
       
    }

    private void EnsureShopOfferDealt(List<ModuleDefinition> allModules)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return;

        // Offre vide si les 3 slots sont null/empty
        bool empty = true;
        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(run.shopOfferModuleIds[i]))
            {
                empty = false;
                break;
            }
        }

        if (!empty)
            return;

        // Source de vérité pour les règles "monde"
        string worldId = run.worldId;

        // NEW: le hub demande, les rules décident
        int rr = Mathf.Max(0, run.shopRerollCount);
        List<ModuleDefinition> offer = ModulesShopOfferRules.BuildOffer(allModules, IsOwned, worldId, rr, ShopOfferCount);

        if (offer == null || offer.Count == 0)
            return;

        // Persistance des IDs (slots 0..2)
        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = null;

        for (int i = 0; i < offer.Count && i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = offer[i].id;

        SaveManager.Instance.Save();
        Debug.Log("[SHOP] DEAL -> " + string.Join(",", run.shopOfferModuleIds));
    }

    private List<ModuleDefinition> BuildShopModulesFromOfferIds(List<ModuleDefinition> allModules)
    {
        var result = new List<ModuleDefinition>(ShopOfferCount);

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return result;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return result;

        bool changed = false;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            string id = run.shopOfferModuleIds[i];
            if (string.IsNullOrEmpty(id))
                continue;

            // Si entre temps déjà owned => on le retire de l'offre (cohérence)
            if (IsOwned(id))
            {
                run.shopOfferModuleIds[i] = null;
                changed = true;
                continue;
            }

            ModuleDefinition def = allModules.Find(m => m != null && string.Equals(m.id, id, StringComparison.Ordinal));
            if (def != null)
                result.Add(def);
            else
            {
                // Module introuvable => purge slot (sécurité)
                run.shopOfferModuleIds[i] = null;
                changed = true;
            }
        }

        if (changed)
            SaveManager.Instance.Save();

        return result;
    }

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
            if (string.Equals(run.shopOfferModuleIds[i], moduleId, StringComparison.Ordinal))
            {
                run.shopOfferModuleIds[i] = null;
                changed = true;
            }
        }

        if (changed)
            SaveManager.Instance.Save();
    }
}