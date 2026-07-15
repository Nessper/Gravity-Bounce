# Scènes et câblage

> **Périmètre** : scènes du build, rôle de chacune, services persistants et scènes hors build.  
> **Statut** : confirmé par `EditorBuildSettings` et sérialisation YAML ; aucun chargement exécuté.  
> **Date de vérification** : 2026-07-15.  
> **Principaux appuis** : `ProjectSettings/EditorBuildSettings.asset`, scènes sous `Assets/Project/Scenes`.

## Scènes du build

| Index | Scène | Rôle observé |
|---:|---|---|
| 0 | `Boot` | Root persistant et initialisation globale. |
| 1 | `Title` | Écran titre et entrée du parcours. |
| 2 | `ShipSelect` | Sélection du vaisseau et création de run. |
| 3 | `RunHub` | Carte de run, boutique, réparation et équipement. |
| 4 | `Main` | Mission jouable et séquences de résultats. |
| 5 | `DebugLauncher` | Injection de contexte de test avant Main. |
| 6 | `CreditsScene` | Crédits et liens. |

Le flux canonique est détaillé dans [Flux runtime](../00-vue-ensemble/flux-runtime.md).

## BootRoot persistant

La scène Boot câble un root conservé entre scènes. Les composants/services retrouvés comprennent `Bootstrapper`, `GameFlowController`, `SaveManager`, `AudioManager`, `LocalizationManager`, `AlphaAnalytics`, `RunRecoveryOnBoot`, statistiques et équipement de modules, `PlatformTuning`, `BallPhysicsTuning`, `ComboDefinitionProvider` et le prefab/configuration de run.

Les écrans suivants dépendent normalement de ce root. Des installateurs alternatifs existent pour Main isolée/debug ; voir [Debug et outils alpha](../01-systemes/debug-et-outils-alpha.md).

## Main

`Main` regroupe le plateau, les managers de niveau, le spawner, le paddle, les bacs, le HUD, les overlays, l’audio de niveau, les feedbacks et les testeurs debug. Le `LevelBootstrapper` résout les dépendances à partir du contexte courant et lance l’orchestration.

Les drones de gameplay appartiennent au monde et non aux Game Systems. Le câblage actif place `DronesRoot` sous `WorldRoot/BoardRoot`, au même niveau architectural que les autres racines du plateau. `K1 Anti-Black Drone` et `K2 Drone Interceptor` en sont des enfants sur le layer `Gameplay` et le même plan Z.

K1 référence le spawner, les deux `BinTrigger` et le mur gauche. K2 référence le spawner, le paddle et la `Drone Interception Zone`, trigger séparé situé après la zone de collecte et avant le Void. Les deux contrôleurs construisent à l’exécution leurs trois images de charge world-space (`cooldown`, `uncharged`, `charged`) à partir de sprites sérialisés. K1 crée aussi son laser/flash de décharge ; K2 crée ses flashes de départ et d’arrivée. La logique de cooldown et l’application transversale de K0 restent dans `DroneRuntimeControllerBase`/`ModuleRuntimeStats`.

Des écrans de fin de générations différentes sont encore sérialisés. Le fait qu’un GameObject soit actif dans le YAML ne suffit pas à établir qu’il reçoit le contrôle fonctionnel ; le chemin orchestré est décrit dans [Fin de niveau](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## Hors build

Les anciennes copies `Assets/MainScene_tmp.unity` et `Assets/_Recovery/0.unity` ont été supprimées après vérification de leur absence des Build Settings et de toute référence runtime. Aucun parcours du jeu ne dépendait de ces archives.

## État de travail

Les constats de câblage représentent l’état de travail observé à la date de vérification. L’inventaire des scènes déjà modifiées est centralisé dans [Incertitudes et travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md). Aucun fichier Unity n’a été modifié pour produire ces pages.
