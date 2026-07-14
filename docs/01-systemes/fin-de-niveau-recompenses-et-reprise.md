# Fin de niveau, récompenses et reprise

> **Périmètre** : arrêt d’une mission, évaluation, snapshot, cérémonie, commit, récompenses et récupération après interruption.  
> **Statut** : chemin courant confirmé statiquement ; animations et reprise non exécutées.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `LevelEndFlowController.cs`, `EndSequenceController.cs`, `LevelEvacuationController.cs`, classes `Gameplay/EndLevel`, overlays `ResultsCeremony` et `EndResult`, `RunRecoveryOnBoot.cs`.

## Déclencheurs

Une fin peut résulter du timer, de l’accomplissement/échec déterminé par le niveau, de l’épuisement de coque ou d’un abandon. Les contrôles et le spawn sont arrêtés avant la consolidation. La séquence d’évacuation laisse traiter les balles présentes, force un flush final et nettoie ce qui reste.

## Construction du résultat

Après nettoyage, les évaluateurs assemblent : résultat de l’objectif principal, objectifs secondaires, score détaillé, combos finaux, médailles et bonus de modules de fin. `EndLevelOutcomeBuilder` construit l’outcome ; un `EndLevelSnapshot` sérialisable le fige pour la présentation et la reprise.

Les modules de famille F peuvent produire de l’argent en fonction des médailles. Les autres détails de modules sont canoniques dans [Économie, boutique et modules](economie-boutique-et-modules.md).

## Préparation et cérémonie

Un `EndLevelToken` identifie la transaction. Token et snapshot sont persistés avant la cérémonie. `ResultsCeremonyOverlayController` déroule les lignes et totaux ; `EndResultOverlayController` présente ensuite l’issue et les actions disponibles. Ces écrans consomment le snapshot au lieu de recalculer le gameplay.

## Commit

Le commit associé au token applique une seule fois les effets au run : score cumulé, issue du nœud, récompenses, progression ou consommation de vie de contrat, puis effacement de l’état de fin en attente. Le meilleur score permanent est mis à jour au niveau approprié du parcours. Le token sert de garde contre un second commit après rechargement ou répétition d’action.

## Reprise

Si l’application est interrompue après préparation, la présence du snapshot/token permet de restaurer la présentation puis de terminer le commit. Si l’interruption survient pendant un niveau marqué actif sans snapshot final, le mécanisme de Boot traite l’état comme niveau abandonné selon les marqueurs présents.

La répartition de cette logique entre deux composants et l’incertitude sur leur ordre runtime sont consignées dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Générations coexistantes

Plusieurs générations d’écrans de fin restent dans le projet. Le chemin orchestré actuel utilise la cérémonie et `EndResult`; l’inventaire et la classification des générations antérieures sont centralisés dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).
