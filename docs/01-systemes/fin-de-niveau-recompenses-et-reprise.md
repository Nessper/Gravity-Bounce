# Fin de niveau, récompenses et reprise

> **Périmètre** : arrêt d’une mission, évaluation, snapshot, cérémonie, commit, récompenses et récupération après interruption.  
> **Statut** : chemin courant confirmé statiquement ; animations et reprise non exécutées.  
> **Date de vérification** : 2026-07-23.
> **Principaux appuis** : `LevelEndFlowController.cs`, `EndSequenceController.cs`, `LevelEvacuationController.cs`, classes `Gameplay/EndLevel`, overlays `ResultsCeremony` et `EndResult`, `RunRecoveryOnBoot.cs`.

## Déclencheurs

Une fin peut résulter du timer, de l’accomplissement/échec déterminé par le niveau, de l’épuisement de coque ou d’un abandon. Les contrôles et le spawn sont arrêtés avant la consolidation. La séquence d’évacuation laisse traiter les balles présentes, force un flush final et nettoie ce qui reste. Sur le chemin normal, elle attend aussi la fin des arrivées visuelles du score et l’animation du total cumulé avant l’outro du plateau et le masquage du HUD. Un délai maximal de sécurité empêche cette présentation de retenir indéfiniment la suite du flux.

Les chaînes ne sont pas remises à zéro au début de l’évacuation : elles restent actives pendant le flush final et ses impacts de score, puis le reset commun intervient avant l’outro. L’épuisement de coque est surveillé par `HullGameOverWatcher`, dont la référence à `RunSessionState` est câblée dans `Main`.

## Construction du résultat

Après nettoyage, les évaluateurs assemblent : résultat de l’objectif principal, objectifs secondaires, score détaillé, combos finaux, médailles et bonus de modules de fin. `EndLevelOutcomeBuilder` construit l’outcome ; un `EndLevelSnapshot` sérialisable le fige pour la présentation et la reprise.

Les modules de famille F peuvent produire de l’argent en fonction des médailles. Les autres détails de modules sont canoniques dans [Économie, boutique et modules](economie-boutique-et-modules.md).

## Préparation et cérémonie

Un `EndLevelToken` identifie la transaction. Token et snapshot sont persistés avant la cérémonie. `ResultsCeremonyOverlayController` déroule les lignes et totaux ; `EndResultOverlayController` présente ensuite l’issue et les actions disponibles. Ces écrans consomment le snapshot au lieu de recalculer le gameplay.

La cérémonie normale déclenche `MusicId.MainEndSequence` une seule fois via le contrôleur actif, avec les fondus configurés dans `Main`. Une défaite directe contourne cette cérémonie : un clignotement rouge de destruction se poursuit en temps non affecté par la pause pendant le délai, le fondu du fond et le fondu de l’overlay, puis s’arrête lorsque `EndResult` est effectivement révélé. Le contenu de cet overlay est préparé avant son apparition pour empêcher le flash d’un ancien état.

## Commit

Le commit associé au token applique une seule fois les effets au run : score cumulé, issue du nœud, récompenses, progression ou consommation de vie de contrat, puis effacement de l’état de fin en attente. Le meilleur score permanent est mis à jour au niveau approprié du parcours. Le token sert de garde contre un second commit après rechargement ou répétition d’action.

## Reprise

Si l’application est interrompue après préparation, la présence du snapshot/token permet de restaurer la présentation puis de terminer le commit. Si l’interruption survient pendant un niveau marqué actif sans snapshot final, le mécanisme de Boot traite l’état comme niveau abandonné selon les marqueurs présents.

La répartition de cette logique entre deux composants et l’incertitude sur leur ordre runtime sont consignées dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Générations coexistantes

Plusieurs générations d’écrans de fin restent dans le projet. Le chemin orchestré actuel utilise la cérémonie et `EndResult`; l’inventaire et la classification des générations antérieures sont centralisés dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).
