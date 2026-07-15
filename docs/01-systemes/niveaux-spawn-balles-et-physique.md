# Niveaux, spawn, balles et physique

> **Périmètre** : définition des missions, phases, génération des balles, pooling, obstacles et comportement physique.  
> **Statut** : confirmé par code/données ; hasard exact et physique non joués.  
> **Date de vérification** : 2026-07-15.  
> **Principaux appuis** : `LevelData.cs`, `LevelCatalogService.cs`, JSON `Resources/Levels`, `BallSpawner.cs`, `BallDefinition*`, `BallState.cs`, `BallPhysicsTuning.cs`, `ObstacleManager.cs`.

## Contenu de niveau

`LevelCatalog.json` indexe W1-L1 à W1-L6 et un niveau DBG-L1. Chaque fichier de niveau décrit l’objectif, la durée, les phases de spawn, les mélanges de balles, les objectifs secondaires, les obstacles et des références de narration/présentation. Le `LevelContext` runtime combine ces données avec le run et les réglages de modules.

Certaines propriétés de `LevelData` appartiennent à des schémas antérieurs. Leur classification est centralisée dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md) et leur absence de consommation retrouvée dans le [registre d’incertitudes](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Balles

Quatre `BallDefinition` sont actives : blanche, bleue, rouge et noire. Les trois couleurs positives contribuent à la progression et au score selon leur valeur ; la noire est négative/dangereuse et ne contribue pas à l’objectif de collecte. Les valeurs canoniques sont dans [Équilibrage](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

`BallState` porte l’identité/type courant, ce qui permet aux modules de transformer une balle avant la résolution finale. Les objets sont réutilisés par pooling et remis en circulation après collecte/nettoyage. `BallSpawner` maintient aussi le registre des billes actives pour permettre un ciblage ponctuel sans recherche globale. Le recyclage distingue une collecte, une perte et une neutralisation par K1 ; cette dernière ne compte ni comme collecte ni comme perte.

`BallState` porte également une exclusion temporaire réservée aux drones. K2 l’utilise pour suspendre collider et simulation pendant une saisie, sans recycler la bille ni modifier son identité. `BallSpawner.TryGetDroneReentryPosition` choisit une position libre dans la plage X normale et sous le plafond ; la bille y est relâchée avec sa physique restaurée. Cette téléportation est donc distincte du spawn et du pool.

## Génération

`BallSpawner` transforme chaque phase en quota et répartit les types selon le mélange demandé. La répartition des comptes est déterministe pour un quota donné ; l’emplacement de spawn utilise de l’aléatoire. Des spawns forcés existent pour les séquences contrôlées, notamment le tutoriel.

Des champs de spawn sans consommation retrouvée et une divergence de schéma propre à DBG-L1 existent. Leur inventaire et le comportement non établi sont centralisés dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Physique et plateau

Le mouvement repose sur la physique 2D Unity, complétée par une logique de rebond du paddle et des réglages `BallPhysicsTuning`. `BallCeilingGrace` traite une tolérance au plafond. Les bords, le vide, les bacs et le paddle utilisent des triggers/collisions dédiés.

`ObstacleManager` instancie/configure les placements de niveau. Un seul type de contenu d’obstacle, `Obstacle1`, a été retrouvé. Les champs de placement sans consommation retrouvée sont inventoriés dans le [registre d’incertitudes](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md). Les feedbacks visuels des collisions sont couverts dans [Caméra, environnement et FX](camera-environnement-fx-et-video.md).
