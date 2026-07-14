# Flux runtime

> **Périmètre** : séquences principales depuis le démarrage jusqu’à la fin d’un run et cycle interne d’un niveau.  
> **Statut** : flux confirmé par le code et le build settings ; détails d’ordre fin parfois statiques.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `EditorBuildSettings.asset`, `GameFlowController`, `NewRunInitializer`, `RunHubController`, `LevelManager`, `LevelEndFlowController`, `EndSequenceController`.

## Parcours de scènes

```text
Boot → Title → ShipSelect → RunHub → Main
                              ↑       │
                              └───────┘  entre les nœuds
                                      └→ CreditsScene en fin de parcours prévue

DebugLauncher → Main  (chemin de test)
```

Les sept scènes ci-dessus sont activées dans le build. `MainScene_tmp.unity` et `_Recovery/0.unity` existent hors build et ne font pas partie du parcours confirmé.

## Démarrage normal

1. `Boot` crée ou conserve le root persistant et initialise sauvegarde, audio, localisation, analytics, catalogues/configurations runtime et récupération de run.
2. `Title` expose le départ du parcours.
3. `ShipSelect` choisit un identifiant de vaisseau.
4. `NewRunInitializer` initialise les ressources de run et son plan.
5. `RunHub` représente le nœud courant ; une boutique reste dans le hub, un niveau charge `Main`.
6. Après un niveau, le résultat validé fait progresser le nœud ou consomme une vie de contrat selon l’issue.

Les valeurs initiales et le plan W1 sont centralisés dans [Run, nœuds, vaisseaux et ressources](../01-systemes/run-nodes-vaisseaux-et-ressources.md).

## Cycle d’un niveau

Le chemin actif observé est :

1. résolution du `LevelContext` depuis le run ou l’injection debug ;
2. briefing ;
3. introduction visuelle, dialogue et assemblage du plateau ;
4. compte à rebours, avec tutoriel conditionnel sur W1-L1 ;
5. démarrage du timer et du spawner ;
6. collecte, flushs, score, objectifs et dégâts pendant le jeu ;
7. arrêt du spawn à l’échéance ou déclenchement d’une fin anticipée ;
8. évacuation, flush final et nettoyage des balles restantes ;
9. évaluation de l’objectif principal, des secondaires, des combos finaux et bonus de modules ;
10. construction et persistance d’un snapshot/token de fin ;
11. cérémonie de résultats, puis overlay de résultat ;
12. commit idempotent et transition vers le hub ou la destination suivante.

Les détails sont répartis entre [Niveaux, spawn, balles et physique](../01-systemes/niveaux-spawn-balles-et-physique.md), [Score, objectifs et combos](../01-systemes/score-objectifs-et-combos.md) et [Fin de niveau, récompenses et reprise](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## Reprise après interruption

Lorsqu’un niveau est marqué en cours ou qu’une fin est en attente, le démarrage examine l’état sauvegardé. Un niveau interrompu peut être traité comme abandonné ; une cérémonie déjà préparée peut être reconstruite à partir du snapshot sans recalculer le résultat. L’ordre exact entre les deux composants de récupération au Boot est une [incertitude documentée](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).
