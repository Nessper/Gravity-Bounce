using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Facade metier simplifiee pour le systeme Modules cote UI.
///
/// Responsabilites principales :
/// - acces au catalogue de modules
/// - lecture du statut owned via SaveManager
/// - achat d'un module depuis le shop
/// - gestion de l'offre shop persistante
/// - reroll du shop
/// - chargement et cache des icones
///
/// Important :
/// - les regles de composition d'offre ne vivent PAS ici
///   elles restent dans ModulesShopOfferRules
/// - les regles metier d'equipement ne vivent PAS ici
///   elles restent dans RunSessionState / RunModuleEquipmentService
/// - ce controller orchestre le shop et expose une facade simple a l'UI
///
/// Compatibilite temporaire :
/// - On conserve OnInventoryChanged pour ne pas casser les anciens scripts.
/// - Quand la migration sera terminee, cet event legacy pourra etre supprime.
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
    /// Acces public au RunSessionState utilise par ce hub.
    /// </summary>
    public RunSessionState RunSession => runSession;

    /// <summary>
    /// Event principal du nouveau systeme UI modules.
    /// </summary>
    public event Action OnModulesCollectionChanged;

    /// <summary>
    /// Event legacy conserve temporairement pour compatibilite
    /// avec les anciens scripts UI.
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// Emis quand l'equipement change.
    /// Conserve pour compatibilite avec le reste du projet.
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
    // PUBLIC REFRESH
    // ---------------------------------------------------------------------

    /// <summary>
    /// Permet de notifier manuellement les vues UI modules.
    /// Declenche a la fois le nouvel event et l'ancien event legacy.
    /// </summary>
    public void NotifyModulesCollectionChanged()
    {
        OnModulesCollectionChanged?.Invoke();
        OnInventoryChanged?.Invoke();
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
    /// Retourne un module a partir de son id.
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
    // OWNED
    // ---------------------------------------------------------------------

    /// <summary>
    /// Indique si un module est deja possede.
    /// </summary>
    public bool IsOwned(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        return SaveManager.Instance.HasOwnedModule(moduleId);
    }

    // ---------------------------------------------------------------------
    // EQUIPMENT STATE HELPERS
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne true si le module est actuellement equipe dans un slot.
    /// Conserve pour compatibilite.
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
    /// Nombre total de slots d'equipement.
    /// Conserve pour compatibilite.
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
    /// Indique si un slot est verrouille.
    /// Conserve pour compatibilite.
    /// </summary>
    public bool IsSlotLocked(int slotIndex)
    {
        if (runSession == null)
            return true;

        return runSession.IsEquipmentSlotLocked(slotIndex);
    }

    /// <summary>
    /// Retourne le module equipe dans un slot.
    /// Conserve pour compatibilite.
    /// </summary>
    public string GetEquippedModuleIdInSlot(int slotIndex)
    {
        if (runSession == null)
            return null;

        return runSession.GetEquippedModuleId(slotIndex);
    }

    /// <summary>
    /// Retourne la liste des slots ouverts.
    /// Conserve pour compatibilite.
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
    /// La validation metier reste dans RunSessionState.
    /// Conserve pour compatibilite.
    /// </summary>
    public bool TryEquipModuleInSlot(string moduleId, int slotIndex)
    {
        if (runSession == null)
            return false;

        return runSession.TryEquipModuleToSlot(moduleId, slotIndex);
    }

    /// <summary>
    /// Tente de desequiper un slot.
    /// Conserve pour compatibilite.
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
    /// Tente d'acheter un module.
    ///
    /// Regles :
    /// - uniquement en contexte shop
    /// - le module doit etre dans l'offre courante
    /// - le module ne doit pas deja etre possede
    /// - la money doit etre suffisante
    /// - si achat valide : ajout owned + retrait de l'offre
    /// </summary>
    public bool TryBuy(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (GetCurrentShopStage() == ShopStage.None)
            return false;

        if (IsOwned(moduleId))
            return false;

        if (!IsModuleCurrentlyInShopOffer(moduleId))
            return false;

        if (!TryGetModuleById(moduleId, out ModuleDefinition def) || def == null)
            return false;

        if (runSession == null)
        {
            Debug.LogError("[ModulesHubController] RunSessionState manquant.");
            return false;
        }

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
        {
            Debug.LogError("[ModulesHubController] SaveManager indisponible.");
            return false;
        }

        int cost = Mathf.Max(0, def.cost);

        bool spent = runSession.TrySpendMoney(cost);
        if (!spent)
            return false;

        bool added = SaveManager.Instance.TryAddOwnedModule(moduleId);
        if (!added)
        {
            Debug.LogError("[ModulesHubController] Achat incoherent: money depensee mais module non ajoute. moduleId=" + moduleId);

            // Etat incoherent.
            // On retire quand meme l'offre pour eviter les doubles clics ou doubles achats.
            RemoveFromShopOffer(moduleId);
            NotifyModulesCollectionChanged();
            return false;
        }

        spriteCacheByModuleId.Remove(moduleId);

        RemoveFromShopOffer(moduleId);
        NotifyModulesCollectionChanged();

        return true;
    }

    /// <summary>
    /// Retourne true si un module est actuellement present dans l'offre shop persistante.
    /// </summary>
    public bool IsModuleCurrentlyInShopOffer(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return false;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            if (string.Equals(run.shopOfferModuleIds[i], moduleId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // SHOP REROLL
    // ---------------------------------------------------------------------

    /// <summary>
    /// Retourne le nombre de rerolls deja effectues pour le shop courant.
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
    /// Tente un reroll du shop courant.
    ///
    /// Pour l'instant :
    /// - autorise uniquement en contexte shop
    /// - ne depense aucun cout
    /// - incremente le compteur de reroll
    /// - vide l'offre courante
    /// - persiste
    /// - notifie l'UI
    /// </summary>
    public bool TryRerollShop()
    {
        if (GetCurrentShopStage() == ShopStage.None)
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null)
            return false;

        run.shopRerollCount = Mathf.Max(0, run.shopRerollCount) + 1;

        if (run.shopOfferModuleIds != null)
        {
            for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
                run.shopOfferModuleIds[i] = null;
        }

        run.shopOfferInitialized = false;

        SaveManager.Instance.Save();
        NotifyModulesCollectionChanged();

        return true;
    }

    /// <summary>
    /// Ancienne API conservee temporairement pour compatibilite.
    /// Preferer TryRerollShop().
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

        run.shopOfferInitialized = false;

        SaveManager.Instance.Save();
        NotifyModulesCollectionChanged();
    }

    /// <summary>
    /// Ancienne API conservee temporairement pour compatibilite.
    /// Preferer TryRerollShop().
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
    /// Retourne le sprite d'icone d'un module depuis Resources avec cache.
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
    /// Vide le cache d'icones.
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

        ShopStage shopStage = GetCurrentShopStage();
        if (shopStage == ShopStage.None)
            return true;

        if (!TryGetAllModules(out List<ModuleDefinition> allModules) || allModules == null)
            return false;

        EnsureShopOfferDealt(allModules, shopStage);

        modules = BuildShopModulesFromOfferIds(allModules);
        return modules != null;
    }

    /// <summary>
    /// Retourne true si au moins un slot de l'offre persistante contient un module.
    /// Utile pour debug et verification d'etat.
    /// </summary>
    public bool HasAnyShopOfferPersisted()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null)
            return false;

        SaveManager.Instance.EnsureShopOfferArrays(ShopOfferCount);

        RunStateData run = SaveManager.Instance.GetRunState();
        if (run == null || run.shopOfferModuleIds == null)
            return false;

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(run.shopOfferModuleIds[i]))
                return true;
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // SHOP OFFER - INTERNAL
    // ---------------------------------------------------------------------

    /// <summary>
    /// Deal une nouvelle offre de shop si l'offre courante est vide.
    /// Ne fait rien :
    /// - hors contexte shop
    /// - si une offre existe deja
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
        if (run == null || run.shopOfferModuleIds == null)
            return;

        // Important :
        // on ne redeal PAS si une offre a deja ete generee
        // pour CE shop node precis.
        //
        // Cela permet :
        // - de conserver la meme offre apres quit/reload
        // - d eviter un redeal si le joueur a tout achete
        // - MAIS de generer une nouvelle offre au shop suivant
        bool sameShopNode =
            run.shopOfferInitialized &&
            run.shopOfferNodeIndex == run.currentNodeIndex;

        if (sameShopNode)
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

        for (int i = 0; i < run.shopOfferModuleIds.Length; i++)
            run.shopOfferModuleIds[i] = null;

        if (offer != null)
        {
            for (int i = 0; i < offer.Count && i < run.shopOfferModuleIds.Length; i++)
                run.shopOfferModuleIds[i] = offer[i].id;
        }

        run.shopOfferInitialized = true;
        run.shopOfferNodeIndex = run.currentNodeIndex;
        SaveManager.Instance.Save();
    }

    /// <summary>
    /// Reconstruit la liste des modules visibles a partir des ids persistes en save.
    ///
    /// Nettoie automatiquement :
    /// - les modules deja owned
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
                run.shopOfferModuleIds[i] = null;
                changed = true;
            }
        }

        if (changed)
            SaveManager.Instance.Save();

        return result;
    }

    /// <summary>
    /// Retire un module precis de l'offre courante.
    /// Utilise apres achat.
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
    /// - le plan n'est pas charge
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