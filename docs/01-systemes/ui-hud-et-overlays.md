# UI, HUD et overlays

> **Périmètre** : architecture des interfaces, écrans, HUD de mission, overlays, boutique et systèmes de vaisseau.  
> **Statut** : inventaire confirmé par scripts/scènes ; rendu et animations non exécutés.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : scripts sous `Assets/Project/Scripts/UI`, scènes de build et prefabs sous `Assets/Project/Prefabs`.

## Écrans hors mission

- `Title` : logo, vidéo et actions de départ.
- `ShipSelect` : choix visuel du vaisseau et transition vers le run.
- `RunHub` : carte de nœuds, statut du vaisseau, boutique, réparation et systèmes/modules.
- `CreditsScene` : lecture du catalogue de crédits et navigation externe associée.

Les contrôleurs de scène appellent le flux global ; les données métier restent détenues par le run, la boutique et les catalogues.

## HUD de mission

Le HUD expose l’identifiant de niveau, timer, score, progression, objectif, coque, vies de contrat, guides de paddle/bac et notifications de statistiques. Les binders (`ScoreBinder`, `HullBinder`, `ContractLivesBinder`) relient les managers aux vues spécialisées. Plusieurs barres segmentées et groupes de taille TMP harmonisent la présentation.

## Overlays du chemin actif

`MainUIController` coordonne principalement : briefing, compte à rebours, tutoriel, pause, combos, dégâts, évacuation, cérémonie de résultats, résultat final et transition de sortie. Les overlays de combo runtime et de flush consomment les événements du moteur de score ; ils ne calculent pas le résultat canonique.

Le détail du cycle de fin est dans [Fin de niveau](fin-de-niveau-recompenses-et-reprise.md). Les écrans historiques qui coexistent sont classés dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).

## UI de boutique et d’équipement

Les vues `RunHubModules*` affichent les offres, achats et rerolls. Les contrôleurs `ShipSystems*` affichent modules possédés, équipés et slots, puis transmettent les interactions aux services de run. `ShipStatusPanelUI` fournit une représentation partagée du vaisseau.

## UI de score en cours de raccordement

Au moment de l’analyse, une nouvelle chaîne de présentation du score coexistait avec la précédente. L’inventaire des composants, l’état des références et la limite d’interprétation sont centralisés dans [Travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Frontière

Les animations, sons de bouton et feedbacks ne sont pas sources de vérité métier. L’UI lit les snapshots, états ou événements et déclenche des commandes de navigation/achat/équipement. Les exceptions ou doublons historiques sont documentés comme tels, sans recommandation d’évolution.
