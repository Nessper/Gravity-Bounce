# Systèmes actifs, legacy et hybrides

> **Périmètre** : classification des générations coexistantes et champs apparemment historiques.  
> **Statut** : observation statique, sans recommandation de suppression ou d’évolution.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : scènes, prefabs, scripts, namespaces, clés de sauvegarde et recherches de références.

## Identité du projet

L’identité actuelle visible est 404 (`404 - A Space Arcade Roguelite`). Elle coexiste avec `VoidScrappers` dans des namespaces, menus Editor, clés `VS_*` et identifiants Player historiques. Le projet est donc hybride sur le plan nominal, même lorsque le comportement concerné reste actif.

## Fin de niveau : trois générations

1. `VictoryUI`, `DefeatGameOverUI` et `LevelScoreSummaryUI` représentent une première famille.
2. `EndLevelUI` et `FinalPanelUI` représentent une seconde présentation ; leurs GameObjects restent présents/actifs dans Main observée.
3. `ResultsCeremonyOverlayController` puis `EndResultOverlayController` constituent le flux orchestré actuel avec snapshot/token.

`EndLevelScoreBuilder` existe mais aucun appel actif n’a été retrouvé. `EndLevelOutcomeBuilder` appartient au chemin courant. La documentation fonctionnelle décrit uniquement ce dernier tout en conservant ici la coexistence.

## Transitions

`NextTransitionController` représente une génération ancienne de transition, tandis que `MainExitTransitionController` est raccordé au chemin de sortie actuel.

## Sauvegarde et run

`SaveManager`, `RunSessionState` et les services de modules exposent des méthodes/fields hérités en plus du chemin planifié courant. `RunConfig.SelectedWorld`, `RunConfig.CurrentLevelIndex`, `nodesCleared` et `profileId` sont présents mais sans rôle complet retrouvé dans le parcours principal. La récupération d’abandon est répartie entre `SaveManager` et `RunRecoveryOnBoot`.

## Données de niveau

Le format actuel est fondé sur phases, quotas et `BallId`, tandis que des champs et un fichier debug reflètent un schéma antérieur. L’inventaire exact des champs sans consommation et la divergence DBG-L1 sont centralisés dans [Incertitudes et contradictions](incertitudes-contradictions-et-travaux-en-cours.md).

## Input et UI

L’ancien Input Manager fournit le chemin gameplay dominant, tandis que le nouveau Input System est installé et activé. Le détail des voies de contrôle appartient à [Tutoriel, pause, input et plateformes](../01-systemes/tutoriel-pause-input-et-plateformes.md), et leur interaction non établie au registre des [incertitudes](incertitudes-contradictions-et-travaux-en-cours.md).

L’UI de score utilise désormais le chemin V2 validé : paquets de balles/combos, attraction vers le HUD, absorption séquencée, impacts, odomètre mécanique et session visuelle d’accumulation. L’ancien chemin `ScoreBinder`/`ScoreUI` et son prefab inutilisé ont été retirés. `PlayerOld.prefab` reste présent sans usage courant retrouvé.

## Audio, localisation et debug

`TitleMusicPlayer` coexiste avec `AudioManager` persistant, sans instance de scène retrouvée pour le premier. La localisation mélange packs en/fr, langue Boot fixée à fr et textes directs. Plusieurs testeurs/loggers debug sont attachés à des objets actifs de Main.

## Scènes auxiliaires

Les anciennes scènes d’archive `MainScene_tmp.unity` et `_Recovery/0.unity` ont été retirées après confirmation qu’elles étaient hors build et non référencées.
