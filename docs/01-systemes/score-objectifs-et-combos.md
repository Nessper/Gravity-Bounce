# Score, objectifs et combos

> **Périmètre** : score de niveau, progression, historiques, objectifs principal/secondaires, combos runtime et finaux.
> **Statut** : architecture confirmée ; combos existants normalisés et familles I/J intégrées.
> **Date de vérification** : 2026-07-23.
> **Principaux appuis** : `ScoreManager.cs`, `FlushResolution*`, règles sous `Gameplay/Combos`, `SecondaryObjectivesManager.cs`, `LevelResultEvaluator.cs`, `FinalComboEvaluator.cs`, catalogues de combos.

## État suivi

`ScoreManager` maintient le score, le nombre collecté contribuant à la progression, les pertes, les snapshots/historiques de bacs et les déclenchements de combos. Les balles noires sont exclues de la progression principale. Le score du niveau est ensuite intégré au score de run lors du commit de fin.

Les valeurs unitaires des balles et paramètres de combo sont regroupés dans [Équilibrage](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## Résolution runtime

`ComboResolver` applique des règles spécialisées à un flush :

- couleur : `WhiteStreak`, `BlueRush`, `RedStorm` ;
- timing : `FastFlush` ;
- chaînes par couleur ;
- volume : `Super`, `Ultra`, `Monster`.
- compositions mixtes du module J : `J_MIX_41`, `J_MIX_32`, `J_MIX_221`.

`ChainRuntimeState` et `TimingRuntimeState` conservent un état entre résolutions. `FlushResolutionEngine.ResetRuntimeState()` constitue l’API commune de remise à zéro : elle réinitialise le résolveur et demande au `MainUIController` d’effacer la présentation des chaînes.

Cette API est appelée à l’initialisation, à la fin du tutoriel, lors d’un retry depuis la pause, lors d’un arrêt dur du gameplay et lors de la fin normale. Dans ce dernier cas, le reset intervient seulement après le flush final et l’achèvement de ses impacts de combo/score ; l’UI de chaînes reste donc visible pendant leur résolution, puis disparaît avant l’outro du plateau. Une remise à zéro défensive suit aussi la capture du résultat.

Chaque ligne de chaîne connaît le `MaxLevel` de sa définition. Son niveau monte par paliers, sa barre bleue représente la progression à l’intérieur du palier courant et reste pleine au niveau maximal. Le remplissage rejoint sa nouvelle cible par interpolation sur `0,16 s`, ce qui évite les sauts visuels sans modifier l’état métier.

Le `ComboDefinitionCatalog` fournit seuils, bonus, clés de localisation et présentation. Les noms des treize combos sont localisés en français et en anglais dans le pack dédié `Resources/Localization/combos`, avec fallback sur l’identifiant. La scène `Boot` charge explicitement ce pack avec `ui`, `ships` et `modules`. Les combos Volume utilisent `PercentOfPositivePoints`; les seuils Color et Volume sont lus dans le catalogue avec garde de repli identique aux anciennes valeurs.

La famille J est inactive avec `JComboTier = 0`. Ses tiers sont cumulatifs : T1 active 4+1, T2 ajoute 3+2, T3 ajoute le tricolore 2+2+1. Plusieurs occurrences peuvent partager le même `DefinitionId`; leur `OccurrenceKey` canonique et leurs rôles/couleurs restent propres à l’occurrence. Le score, l’affichage et les compteurs travaillent par occurrence, tandis que la diversité travaille sur les IDs distincts.

La famille I ne participe pas à la détection. Une fois toutes les règles résolues, `FlushResolutionEngine` transmet le multiplicateur runtime unique à la résolution avant tout score, historique, objectif ou affichage. Chaque événement conserve ses `BasePoints`; ses `Points` finaux sont arrondis individuellement avec `Mathf.RoundToInt`, puis `ComboTotal` est recalculé. L’application est idempotente et ne modifie ni `DefinitionId`, ni `OccurrenceKey`, ni les rôles ou compteurs.

## Objectif principal

La réussite principale compare la progression obtenue à la cible du niveau. L’évaluation est figée dans le résultat de fin avec l’état de coque/échec approprié. Le timer et les fins anticipées sont orchestrés par le flux de niveau, pas par l’UI.

## Objectifs secondaires

Les types retrouvés sont : nombre de balles, nombre de combos, maximum d’un compteur, maximum de balles perdues, avec possibilité de restriction à une phase. `ComboCount` compte chaque occurrence pour un ID ciblé et additionne toutes les occurrences avec `TargetId = Any`. `LevelSecondaryObjectivesController` relie la définition du niveau au `SecondaryObjectivesManager`, qui produit les résultats utilisés pour les médailles/récompenses.

## Combos finaux

`FinalComboEvaluator` examine l’historique complet après le nettoyage final. `FinalComboConfig` définit les bonus finaux, puis le résultat les présente comme lignes distinctes. Les chaînes utilisent les IDs runtime canoniques de `ComboIds`; la diversité compte les `DefinitionId` distincts, jamais les clés d’occurrence.

## Frontière de responsabilité

Cette page décrit la production du résultat. Sa mise en snapshot, les médailles, récompenses et l’ajout au run sont décrits uniquement dans [Fin de niveau, récompenses et reprise](fin-de-niveau-recompenses-et-reprise.md).
