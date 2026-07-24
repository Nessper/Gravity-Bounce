# Documentation technique de 404

> **Périmètre** : index de la documentation d’architecture du projet Unity 404.  
> **Statut** : vérifié par analyse statique ; aucune exécution en Play Mode.  
> **Date de vérification** : 2026-07-23.
> **Principaux appuis** : `Assets/Project`, `Assets/Resources`, `ProjectSettings`, `Packages/manifest.json`.

Cette documentation décrit l’état observé du projet. Elle ne prescrit ni correction, ni refactorisation, ni évolution. Les mentions **Confirmé**, **Observation statique** et **Incertain** ont le sens défini dans le [guide de lecture](00-vue-ensemble/guide-de-lecture-et-glossaire.md).

## Parcours recommandés

- Découverte rapide : [architecture globale](00-vue-ensemble/architecture-globale.md), puis [flux runtime](00-vue-ensemble/flux-runtime.md).
- Travail sur une mécanique : ouvrir sa page dans [`01-systemes`](01-systemes/).
- Recherche d’une donnée ou d’un branchement Unity : consulter [`02-donnees-et-unity`](02-donnees-et-unity/).
- Recherche d’une classe, d’un identifiant ou d’un invariant : consulter [`03-reference`](03-reference/).
- Compréhension des couches historiques et des limites de l’analyse : consulter [`04-etat-du-projet`](04-etat-du-projet/).

## Documents

Les pages suivantes constituent l’ensemble de référence, regroupé par usage.

### Vue d’ensemble

- [Architecture globale](00-vue-ensemble/architecture-globale.md)
- [Flux runtime](00-vue-ensemble/flux-runtime.md)
- [Guide de lecture et glossaire](00-vue-ensemble/guide-de-lecture-et-glossaire.md)

### Systèmes

- [Sauvegarde, progression et intégrité](01-systemes/sauvegarde-progression-et-integrite.md)
- [Run, nœuds, vaisseaux et ressources](01-systemes/run-nodes-vaisseaux-et-ressources.md)
- [Économie, boutique et modules](01-systemes/economie-boutique-et-modules.md)
- [Niveaux, spawn, balles et physique](01-systemes/niveaux-spawn-balles-et-physique.md)
- [Paddle, bacs et flush](01-systemes/paddle-bacs-et-flush.md)
- [Score, objectifs et combos](01-systemes/score-objectifs-et-combos.md)
- [Fin de niveau, récompenses et reprise](01-systemes/fin-de-niveau-recompenses-et-reprise.md)
- [Tutoriel, pause, input et plateformes](01-systemes/tutoriel-pause-input-et-plateformes.md)
- [UI, HUD et overlays](01-systemes/ui-hud-et-overlays.md)
- [Audio, localisation et dialogues](01-systemes/audio-localisation-et-dialogues.md)
- [Caméra, environnement, FX et vidéo](01-systemes/camera-environnement-fx-et-video.md)
- [Analytics, crédits et services externes](01-systemes/analytics-credits-et-services-externes.md)
- [Debug et outils alpha](01-systemes/debug-et-outils-alpha.md)

### Données et Unity

- [Données, schémas et sources de vérité](02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md)
- [Équilibrage et configuration active](02-donnees-et-unity/equilibrage-et-configuration-active.md)
- [Scènes et câblage](02-donnees-et-unity/scenes-et-cablage.md)
- [Prefabs, ScriptableObjects et assets runtime](02-donnees-et-unity/prefabs-scriptableobjects-et-assets-runtime.md)
- [Réglages Unity, packages et compilation](02-donnees-et-unity/reglages-unity-packages-et-compilation.md)

### Référence et état

- [Index des classes, dépendances et événements](03-reference/index-classes-dependances-et-evenements.md)
- [Identifiants, chemins Resources et API](03-reference/identifiants-chemins-resources-et-api.md)
- [Invariants, cycles de vie et persistance](03-reference/invariants-cycles-de-vie-et-persistance.md)
- [Systèmes actifs, legacy et hybrides](04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md)
- [Incertitudes, contradictions et travaux en cours](04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md)
- [Maintenance documentaire](05-maintenance/maintenance-documentaire.md)

### Documents de conception complémentaires

- [Guide des modules](Guide_des_modules_404.docx) : fonctions et effets des modules.
- [Audit des builds de modules](Audit_builds_modules_404.docx) : archétypes early/late game et gameplay émergent, projetés sur cinq mondes et deux boutiques par monde.

## Limite de vérification

L’analyse couvre les sources, scènes sérialisées, prefabs, ScriptableObjects, fichiers `Resources`, réglages et packages présents. Unity n’a pas été lancé : les comportements dépendant de l’ordre exact des callbacks, de références injectées à l’exécution, d’animations ou de valeurs calculées en Play Mode restent signalés comme tels.
