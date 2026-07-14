# Scènes et câblage

> **Périmètre** : scènes du build, rôle de chacune, services persistants et scènes hors build.  
> **Statut** : confirmé par `EditorBuildSettings` et sérialisation YAML ; aucun chargement exécuté.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `ProjectSettings/EditorBuildSettings.asset`, scènes sous `Assets/Project/Scenes`, `Assets/MainScene_tmp.unity`, `Assets/_Recovery/0.unity`.

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

Des écrans de fin de générations différentes sont encore sérialisés. Le fait qu’un GameObject soit actif dans le YAML ne suffit pas à établir qu’il reçoit le contrôle fonctionnel ; le chemin orchestré est décrit dans [Fin de niveau](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## Hors build

`Assets/MainScene_tmp.unity` et `Assets/_Recovery/0.unity` sont suivies mais absentes du build settings. Elles sont traitées comme scènes temporaires/de récupération, pas comme étapes runtime confirmées.

## État de travail

Les constats de câblage représentent l’état de travail observé à la date de vérification. L’inventaire des scènes déjà modifiées est centralisé dans [Incertitudes et travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md). Aucun fichier Unity n’a été modifié pour produire ces pages.
