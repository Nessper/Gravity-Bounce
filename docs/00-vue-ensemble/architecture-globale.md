# Architecture globale

> **Périmètre** : composants majeurs, responsabilités et relations entre scènes, services persistants, état de run et gameplay.  
> **Statut** : vérifié par analyse statique ; synthèse, détails délégués aux pages système.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : scènes de build, `Bootstrapper`, `GameFlowController`, `SaveManager`, `RunSessionState`, `LevelBootstrapper`, catalogues `Resources`.

## Forme générale

404 est un projet Unity monolithique sans assembly definition propre au gameplay. Son runtime combine :

1. une scène `Boot` qui installe des services persistants ;
2. des scènes d’écran (`Title`, `ShipSelect`, `RunHub`, `CreditsScene`) ;
3. une scène de jeu principale (`Main`) configurée à partir du nœud courant ;
4. une scène `DebugLauncher` capable d’injecter un contexte de test ;
5. des catalogues JSON chargés depuis `Resources` et des ScriptableObjects de configuration ;
6. une sauvegarde JSON stockée dans `PlayerPrefs` et un `RunSessionState` servant de miroir runtime.

## Couches observées

| Couche | Responsabilité dominante | Point d’entrée |
|---|---|---|
| Bootstrap persistant | Installation des managers, réglages et état global | `Boot.unity`, `Bootstrapper`, `BootRoot` |
| Navigation | Chargement des scènes et transitions de parcours | `GameFlowController` |
| Persistance | Profil, run, snapshot et token de fin | `SaveManager`, `GameSaveData` |
| Session de run | État observable en mémoire et plan de nœuds | `RunSessionState`, `RunPlan` |
| Catalogues | Résolution des mondes, niveaux, vaisseaux et modules | services `*CatalogService` |
| Niveau | Assemblage du contexte et orchestration temporelle | `LevelBootstrapper`, `LevelManager`, contrôleurs de séquence |
| Résolution de jeu | Collecte, flush, score, combos, objectifs, coque | `BinCollector`, `FlushResolutionEngine`, `ScoreManager` |
| Présentation | HUD, overlays, feedbacks, audio et narration | scripts sous `UI`, `FX`, `Audio`, `Narration` |

## Propriété de l’état

La page canonique [Données, schémas et sources de vérité](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md) détaille les propriétaires. En résumé :

- `SaveManager` possède la représentation persistée.
- `RunSessionState.asset` est le miroir runtime et émet des événements de changement.
- les catalogues définissent le contenu statique ; les scènes/prefabs portent le câblage et certaines valeurs sérialisées ;
- les managers de niveau détiennent l’état éphémère d’une mission ;
- le snapshot de fin fige le résultat avant sa présentation et son commit.

## Dépendances structurantes

`Main` suppose normalement que `Boot` a déjà installé les services et qu’un run/nœud est sélectionné. `MainStandaloneInstaller` et `MainDebugStarterV3` fournissent des chemins alternatifs pour l’exécution isolée ou de debug. Cette coexistence est documentée comme architecture hybride dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).

L’UI observe ou reçoit les états des managers, mais plusieurs générations d’écrans de fin restent sérialisées. Le chemin de fin considéré actif passe par la cérémonie de résultats et l’overlay de résultat ; voir [Fin de niveau, récompenses et reprise](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## Frontières externes

Les seules sorties réseau retrouvées concernent l’analytics alpha et l’ouverture de formulaires/liens externes. La sauvegarde, le score et l’économie restent locaux. Aucun backend d’autorité de jeu n’a été retrouvé.
