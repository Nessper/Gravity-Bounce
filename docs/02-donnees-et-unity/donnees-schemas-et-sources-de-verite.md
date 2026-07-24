# Données, schémas et sources de vérité

> **Périmètre** : répartition des données entre JSON, ScriptableObjects, scènes, runtime et sauvegarde.  
> **Statut** : canonique pour la propriété des données ; schémas historiques signalés.  
> **Date de vérification** : 2026-07-23.
> **Principaux appuis** : classes `Data/Models`, services `Data/Services`, `GameSaveData`, `RunSessionState`, contenus `Assets/Resources` et `Assets/Project/Data`.

## Matrice de propriété

| Donnée | Source de définition | État runtime | Persistance |
|---|---|---|---|
| Mondes et ordre des nœuds | `WorldCatalog.json` | `RunPlan` / `RunSessionState` | sauvegarde de run |
| Niveaux, phases, objectifs, obstacles | JSON `Resources/Levels` | `LevelContext`, managers de niveau | résultat/snapshot seulement |
| Vaisseaux et slots | `ShipCatalog.json` | contexte de run et services d’équipement | sélection, slots et équipement |
| Modules | `ModuleCatalog.json` | catalog service, stats et services runtime | inventaire/équipement/offres |
| Règles d’offres | `modules_shop_rules.json` | contrôleurs de boutique | offres et rerolls |
| Balles | quatre SO + `BallDefinitionCatalog.asset` | `BallState`, spawner, score | non |
| Combos runtime | `ComboDefinitionCatalog.asset` + règles C# | états de combo | historique dans le résultat |
| Combos finaux | `FinalComboConfig_Default.asset` | évaluateur de fin | snapshot/résultat |
| Économie de médailles | `EconomyConfig_Default.asset` | bonus de fin | argent du run |
| Localisation | JSON par langue/domaine | `LocalizationManager` | langue fixée au Boot observé |
| Crédits | `CreditsCatalog.json` | `CreditsController` | non |
| Profil et run | valeurs initiales des catalogues/code | `RunSessionState` | `GameSaveData` via PlayerPrefs |
| Câblage et présentation | scènes/prefabs | MonoBehaviours | sérialisation Unity |

## Chargement des catalogues

Les services `LevelCatalogService`, `WorldCatalogService`, `ShipCatalogService` et `ModuleCatalogService` chargent des `TextAsset` via `Resources.Load`, désérialisent les JSON et résolvent les identifiants. Les ScriptableObjects sont référencés depuis le Boot, les scènes ou chargés depuis `Resources` selon le système.

Les chemins logiques sont centralisés dans [Identifiants, chemins Resources et API](../03-reference/identifiants-chemins-resources-et-api.md).

## Données calculées

Le `LevelContext` est une vue calculée : niveau choisi, vaisseau, durée ajustée, modules et mode debug. Un `BinSnapshot` fige un bac ; un `EndLevelSnapshot` fige une fin. Ces snapshots ne remplacent pas leurs catalogues sources.

L’analyse SCAN est également calculée : `ScanT1AnalysisBuilder` dérive son texte des phases, mixes, intervalles, spawns forcés, objectif principal et durée effective du contexte. `LevelData` ne porte plus de `ScanText` statique. Le plan construit par `BallSpawner` reste toutefois l’autorité runtime sur les files effectivement jouées.

## Schémas hybrides

Les données de niveau et `RunConfig` conservent des éléments de schémas antérieurs à côté des structures courantes. Leur classification se trouve dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md), tandis que l’inventaire exact des champs sans consommation retrouvée et la divergence DBG-L1 restent canoniques dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Règle d’interprétation

Une valeur JSON/SO indique une intention de contenu ; elle n’est dite « active » ici que si un consommateur runtime et un branchement ont été retrouvés. Une valeur sérialisée de scène/prefab peut encore être remplacée au démarrage. Les divergences connues sont listées une seule fois dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).
