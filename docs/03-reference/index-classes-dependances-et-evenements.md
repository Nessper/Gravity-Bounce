# Index des classes, dépendances et événements

> **Périmètre** : points d’entrée C# et relations utiles pour retrouver rapidement une responsabilité.  
> **Statut** : index sélectif et maintenable, non inventaire ligne à ligne des 266 scripts C# sous `Assets/Project/Scripts`.
> **Date de vérification** : 2026-07-15.  
> **Principaux appuis** : `Assets/Project/Scripts`, références sérialisées des scènes/prefabs.

## Bootstrap et navigation

| Point d’entrée | Responsabilité | Dépendances/consommateurs principaux |
|---|---|---|
| `Bootstrapper` / `BootRoot` | Installation et unicité du root global | managers persistants, scènes suivantes |
| `GameFlowController` | Navigation entre scènes | Title, ShipSelect, RunHub, Main, Credits |
| `SaveManager` | Chargement/écriture de `GameSaveData` | run, fin de niveau, récupération |
| `RunRecoveryOnBoot` | Audit des marqueurs de run au démarrage | sauvegarde, flow |
| `LocalizationManager` | Résolution des clés | toutes les vues localisées |
| `AudioManager` | Musique/SFX globaux | UI, niveau, narration |

## Catalogues et run

| Point d’entrée | Responsabilité |
|---|---|
| `WorldCatalogService` | Monde et tokens de nœuds. |
| `LevelCatalogService` | Index et désérialisation des niveaux. |
| `ShipCatalogService` | Définitions de vaisseaux. |
| `ModuleCatalogService` | Définitions de modules. |
| `RunSessionState` | Miroir runtime observable du run. |
| `RunSessionBootstrapper` | Synchronisation/initialisation de la session runtime depuis les services globaux. |
| `NewRunInitializer` | État initial et plan de run. |
| `RunPlanBuilder` | Transformation des tokens de monde en nœuds de run. |
| `RunNavigator` | Navigation et avancée dans le plan construit. |
| `RunHubController` | Routage du nœud courant dans le hub. |
| `RunModuleEquipmentService` | Inventaire, équipement et slots. |
| `ModuleRuntimeStats` | Agrégation des effets équipés, dont départ chargé et multiplicateur de cooldown transversal K0. |

## Niveau et résolution

| Point d’entrée | Responsabilité | Émet/alimente |
|---|---|---|
| `LevelBootstrapper` | Résout le contexte et câble Main | managers de niveau |
| `LevelManager` | Phase jouable et orchestration centrale | timer, spawner, contrôles |
| `LevelRunStateController` | Marqueurs de niveau en cours et liaison avec l’état de run | sauvegarde, fin/abandon |
| `LevelControlsController` | Activation et désactivation coordonnée des contrôles | paddle, bacs, séquences |
| `LevelIntroSequenceController` | Briefing/intro/dialogue/countdown | UI, audio, plateau |
| `BallSpawner` | Phases, quotas et pooling | balles runtime |
| `PlayerController` | Mouvement et rebond du paddle | physique/feedbacks |
| `BinTrigger` | Contenu vivant, ordre d’entrée et retrait notifié d’une bille | `BinCollector`, aperçus A/B, K1 |
| `BinCollector` | Snapshot et pipeline de résolution d’un bac | flush, visuels, score, coque |
| `BlackFilterRuntimeController` | Réservations réversibles de noires pour la famille A | aperçus de bac, snapshot |
| `K1AntiBlackDroneController` | Ronde, cooldown, acquisition et neutralisation garantie des noires | `BallSpawner`, `BinTrigger`, verrou `collected` |
| `DroneRuntimeControllerBase` | Socle commun d’équipement, charge, cooldown, K0 et frontières de mission | `ModuleRuntimeStats`, K1, K2 et futurs drones |
| `DroneInterceptionZone` | Détection événementielle des billes descendantes après la dernière collecte | `K2DroneInterceptorController` |
| `K2DroneInterceptorController` | Patrouille, interception, saisie et téléportation des couleurs autorisées par tier | `DroneInterceptionZone`, `BallState`, `BallSpawner`, paddle |
| `FlushResolutionEngine` | Transformations et total d’un flush | score, coque, combos |
| `ScoreManager` | Score, progression et historique | HUD, objectifs, fin |
| `HullSystem` | Coque courante/maximale | HUD, game over |
| `SecondaryObjectivesManager` | Suivi des secondaires | évaluation finale |
| `ComboResolver` | Règles de combos runtime | score et overlays |

## Fin et présentation

| Point d’entrée | Responsabilité |
|---|---|
| `LevelEvacuationController` | Délai d’évacuation et arrêt contrôlé. |
| `FinalBallCleanupService` / `BallsCleanupService` | Flush/nettoyage des balles restantes. |
| `FinalComboEvaluator` | Bonus fondés sur l’historique final. |
| `LevelEndModuleBonusController` / `ModuleEndLevelBonusProvider` | Calcul et exposition des bonus de modules applicables à la fin. |
| `EndLevelOutcomeBuilder` | Assemblage du résultat fonctionnel. |
| `LevelEndFlowController` / `EndSequenceController` | Séquence, snapshot, présentation et sortie. |
| `ResultsCeremonyOverlayController` | Déroulé des résultats. |
| `EndResultOverlayController` | Issue finale et action suivante. |
| `MainExitTransitionController` | Transition de sortie actuelle. |

## Événements et liaisons

Le projet utilise un mélange d’événements C#, d’UnityEvents sérialisés, d’appels directs et de binders. `RunSessionState`, `ScoreManager`, `HullSystem`, timer, bacs et résolveurs publient les changements consommés par le HUD et les feedbacks. Les binders réduisent le couplage de certaines vues, tandis que les contrôleurs d’overlay reçoivent aussi des dépendances directes depuis `MainUIController`/la scène.

Pour les cycles de vie attendus, voir [Invariants, cycles de vie et persistance](invariants-cycles-de-vie-et-persistance.md). Pour le statut des générations anciennes, voir [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).
