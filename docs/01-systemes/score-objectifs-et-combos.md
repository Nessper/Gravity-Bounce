# Score, objectifs et combos

> **Périmètre** : score de niveau, progression, historiques, objectifs principal/secondaires, combos runtime et finaux.  
> **Statut** : architecture confirmée ; plusieurs correspondances de calcul restent incertaines.  
> **Date de vérification** : 2026-07-14.  
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

`ChainRuntimeState` et `TimingRuntimeState` conservent un état entre résolutions. L’absence de reset régulier retrouvé et sa conséquence non établie sont détaillées uniquement dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

Le `ComboDefinitionCatalog` fournit libellés, règles et présentation. Le statut des assets et scripts de score observés dans l’espace de travail est centralisé dans [Travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Objectif principal

La réussite principale compare la progression obtenue à la cible du niveau. L’évaluation est figée dans le résultat de fin avec l’état de coque/échec approprié. Le timer et les fins anticipées sont orchestrés par le flux de niveau, pas par l’UI.

## Objectifs secondaires

Les types retrouvés sont : nombre de balles, nombre de combos, maximum d’un compteur, maximum de balles perdues, avec possibilité de restriction à une phase. `LevelSecondaryObjectivesController` relie la définition du niveau au `SecondaryObjectivesManager`, qui produit les résultats utilisés pour les médailles/récompenses.

## Combos finaux

`FinalComboEvaluator` examine l’historique complet après le nettoyage final. `FinalComboConfig` définit les bonus finaux, puis le résultat les présente comme lignes distinctes. Les divergences d’identifiants et de sérialisation sont décrites uniquement dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Frontière de responsabilité

Cette page décrit la production du résultat. Sa mise en snapshot, les médailles, récompenses et l’ajout au run sont décrits uniquement dans [Fin de niveau, récompenses et reprise](fin-de-niveau-recompenses-et-reprise.md).
